
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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeWindsCommon.Extensions;

namespace TradeWindsLoggerProvider
{
	/// <summary>
	/// An ILoggerProvider that writes to a text file.
	/// </summary>
	[ProviderAlias("File")]
	public class FileLoggerProvider : LoggerProviderBase
	{
		protected FileLoggerOptions FileOptions;

		/// <inheritdoc />
		protected override LoggerOptionsBase Options => FileOptions;

		public FileLoggerProvider(IOptionsMonitor<FileLoggerOptions> options) : this(options.CurrentValue)
		{
			SettingsChangeToken = options.OnChange(opt => { FileOptions = opt; });
		}

		public FileLoggerProvider(FileLoggerOptions fileOptions)
		{

			FileOptions = fileOptions;
			Interval = GetInterval(fileOptions.Interval);

			Start();
		}

		/// <inheritdoc />
		protected override QueueWriterBase CreateQueueWriter()
		{
			var filenamePattern = FileOptions.Path ?? "log-{yyyy-MM-dd}.txt";
			var logPath = filenamePattern.FormatWithDate(DateTimeKind.Utc);
			logPath = Path.GetFullPath(logPath);
			return new FileQueueWriter(logPath, FileOptions);
		}

		/// <inheritdoc />
		protected override void Cleanup()
		{
			// does nothing
		}
	}
}
