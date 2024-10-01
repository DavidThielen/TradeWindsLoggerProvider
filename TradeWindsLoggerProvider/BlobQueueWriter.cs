
// Copyright (c) 2024 Trade Winds Studios (David Thielen)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using TradeWindsExtensions;

namespace TradeWindsLoggerProvider
{

	/// <summary>
	/// Writes strings to a BLOB file in a background thread.
	/// </summary>
	public class BlobQueueWriter : QueueWriterBase
	{
		private BlobServiceClient ServiceClient { get; set; }
		private string ContainerName { get; set; }
		private string BlobName { get; set; }
		private BlobContainerClient Container { get; set; }
		private AppendBlobClient AppendClient { get; set; }

		/// <summary>
		/// Create the object. This will prepare to write to BLOB storage
		/// </summary>
		/// <param name="azureBlobConnectionString">The Azure connection string.</param>
		/// <param name="path">The full pathname of the BLOB to write to. Includes the container.</param>
		/// <param name="options">The options for this logger.</param>
		public BlobQueueWriter(string? azureBlobConnectionString, string path, BlobLoggerOptions options) : base(options)
		{
			ServiceClient = new BlobServiceClient(azureBlobConnectionString);
			(ContainerName, BlobName) = path.SplitBlobFilename();
			Container = ServiceClient.GetBlobContainerClient(ContainerName);

			// the ILoggerProvider starts everything with a constructor call - so no async
			Container.CreateIfNotExists();

			AppendClient = Container.GetAppendBlobClient(BlobName);

			// create the blob if it does not exist
			if (!AppendClient.Exists())
				AppendClient.CreateIfNotExists();
		}

		private static readonly byte[] CrLf = "\r\n"u8.ToArray();
		private readonly MemoryStream _streamCombinedMessage = new MemoryStream(1024 * 8);
		private int _numThreads = 0;

		/// <inheritdoc />
		public override void WriteLine(List<string> messages)
		{

			if (messages.Count == 0)
				return;

			try
			{
				// should not have simultaneous calls. But if we do, use the old way for the others.
				var num = Interlocked.Increment(ref _numThreads);
				if (num > 1)
				{
					OldWriteLine(messages);
					return;
				}

				// populate with the full message
				_streamCombinedMessage.SetLength(0);
				foreach (var message in messages)
				{
					var bytes = Encoding.UTF8.GetBytes(message);
					_streamCombinedMessage.Write(bytes, 0, bytes.Length);
					_streamCombinedMessage.Write(CrLf);
				}
				_streamCombinedMessage.Position = 0;

				// write in blocks to the blob
				var maxBlockSize = AppendClient.AppendBlobMaxAppendBlockBytes;
				var offset = 0;
				var bytesLeft = _streamCombinedMessage.Length;
				while (bytesLeft > 0)
				{
					var blockSize = (int)Math.Min(bytesLeft, maxBlockSize);
					using (var streamBuffer = new MemoryStream(_streamCombinedMessage.GetBuffer(), offset, blockSize))
					{
						AppendClient.AppendBlock(streamBuffer);
					}

					offset += blockSize;
					bytesLeft -= blockSize;
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"BlobQueueWriter.WriteLine() threw exception {ex}");
			}
			finally
			{
				Interlocked.Decrement(ref _numThreads);
			}
		}

		// old way of doing this - wasteful of memory. Used as fallback if multiple threads call WriteLine
		private void OldWriteLine(List<string> messages)
		{

			using (var streamFullMessage = new MemoryStream())
			{
				// get the full message
				foreach (var message in messages)
				{
					var bytes = Encoding.UTF8.GetBytes(message);
					streamFullMessage.Write(bytes, 0, bytes.Length);
					streamFullMessage.Write(CrLf);
				}

				streamFullMessage.Position = 0;

				// reduce it down to the max size we can write
				var maxBlockSize = AppendClient.AppendBlobMaxAppendBlockBytes;
				var bytesLeft = streamFullMessage.Length;
				var buffer = new byte[maxBlockSize];
				while (bytesLeft > 0)
				{
					var blockSize = (int)Math.Min(bytesLeft, maxBlockSize);
					var bytesRead = streamFullMessage.Read(buffer, 0, blockSize);
					using (var streamBuffer = new MemoryStream(buffer, 0, bytesRead))
					{
						AppendClient.AppendBlock(streamBuffer);
					}

					bytesLeft -= bytesRead;
				}
			}
		}
	}
}