
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

namespace TradeWindsLoggerProvider
{
	/// <summary>
	/// Writes strings to a text file in a background thread.
	/// </summary>
	public class FileQueueWriter : QueueWriterBase
	{
		private readonly StreamWriter _streamWriter;

		/// <summary>
		/// Create the object. This will startup a background thread to write the strings
		/// to the file.
		/// </summary>
		/// <param name="filePath">The full pathname of the file to write to.</param>
		/// <param name="options">The options for this logger.</param>
		public FileQueueWriter(string filePath, FileLoggerOptions options) : base(options)
		{
			filePath = Path.GetFullPath(filePath);
			var file = new FileInfo(filePath);
			file.Directory?.Create();

			// get a file we can write to. If it exists we then append.
			int index = 1;
			var availableFileName = filePath;
			while (true)
			{
				try
				{
					_streamWriter = new StreamWriter(availableFileName, true);
					return;
				}
				catch (IOException)
				{
					availableFileName = CreateFileName(filePath, ref index);
				}
			}
		}

		private static string CreateFileName(string filePath, ref int index)
		{
			var availableFileName = Path.Combine(Path.GetDirectoryName(filePath) ?? "",
				$"{Path.GetFileNameWithoutExtension(filePath)}({index}){Path.GetExtension(filePath)}");
			index++;
			return availableFileName;
		}

		/// <inheritdoc />
		public override void WriteLine(List<string> messages)
		{
			try
			{
				if (messages.Count == 0)
					return;
				foreach (var message in messages)
					_streamWriter.WriteLine(message);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"FileQueueWriter.WriteLine() threw exception {ex}");
			}
		}

		/// <inheritdoc />
		public override void Flush()
		{
			_streamWriter.Flush();
		}

		/// <inheritdoc />
		public override void Close()
		{
			_streamWriter.Dispose();
		}
	}
}