
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

using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeWindsCommon.Extensions;

namespace TradeWindsLoggerProvider
{
	/// <summary>
	/// An ILoggerProvider that writes to an Azure BLOB.
	/// </summary>
	[ProviderAlias("Blob")]
	public class BlobLoggerProvider : LoggerProviderBase
	{
		protected BlobLoggerOptions BlobOptions;

		/// <inheritdoc />
		protected override LoggerOptionsBase Options => BlobOptions;

		public BlobLoggerProvider(IOptionsMonitor<BlobLoggerOptions> options) : this(options.CurrentValue)
		{
			SettingsChangeToken = options.OnChange(opt => { BlobOptions = opt; });
		}

		public BlobLoggerProvider(BlobLoggerOptions blobOptions)
		{
			BlobOptions = blobOptions;
			Interval = GetInterval(BlobOptions.Interval);

			Start();
		}

		/// <inheritdoc />
		protected override QueueWriterBase CreateQueueWriter()
		{
			var path = BlobOptions.Path ?? "log/{yyyy-MM-dd}.txt";
			path = path.FormatWithDate(DateTimeKind.Utc);
			return new BlobQueueWriter(BlobOptions.AzureStorage, path, BlobOptions);
		}

		/// <inheritdoc />
		protected override void Cleanup()
		{

			try
			{
				var intervalMode = GetInterval(BlobOptions.Interval);
				if (intervalMode == IntervalMode.Never)
					return;
				if (string.IsNullOrEmpty(BlobOptions.Path))
					return;
				var index = BlobOptions.Path.IndexOf('{');
				if (index < 0)
					return;
				var numberOfBackups = BlobOptions.NumberOfBackups ?? 14;
				if (numberOfBackups <= 1)
					return;

				var folder = BlobOptions.Path.Substring(0, index);

				var serviceClient = new BlobServiceClient(BlobOptions.AzureStorage);
				var (containerName, blobName) = folder.SplitBlobFilename();
				var container = serviceClient.GetBlobContainerClient(containerName);

				if (!container.Exists())
					return;

				// get all log files (faster than querying 1 by 1 to see if it exists)
				var listBlobs = container.GetBlobs(prefix: blobName);
				var listFilenames = new List<string>();
				foreach (var blob in listBlobs)
					listFilenames.Add($"{containerName}/{blob.Name}");

				var intervalSpan = GetIntervalTimeSpan(intervalMode)!.Value;
				var fileDate = DateTime.UtcNow.Date - intervalSpan * numberOfBackups;

				// after 14 intervals with no match, we're done
				var listCleanupFiles = new List<string>();
				for (var remainingMisses = 14; remainingMisses > 0;)
				{
					var filename = BlobOptions.Path.FormatWithDate(fileDate);
					if (listFilenames.Contains(filename))
					{
						var (cName, fName) = filename.SplitBlobFilename();
						listCleanupFiles.Add(fName);
					}
					else
						remainingMisses--;

					fileDate -= intervalSpan;
				}

				foreach (var filename in listCleanupFiles)
				{
					var blobClient = container.GetBlobClient(filename);
					blobClient.DeleteIfExists();
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Trace.WriteLine("Error in BlobLogger.Cleanup: " + ex.Message);
				Enqueue($"***** BlobLogger.Cleanup threw {ex.Message}");
				throw;
			}
		}
	}
}