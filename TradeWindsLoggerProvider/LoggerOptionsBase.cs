
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

	public class LoggerOptionsBase
	{
		/// <summary>
		/// This is set when the FlushIntervalMinutes is set.
		/// </summary>
		private TimeSpan? _flushIntervalTimeSpan;

		/// <summary>
		/// The value for FlushIntervalMinutes. Use the backing field so we can set _flushInterval.
		/// </summary>
		private int? _flushInterval;

		/// <summary>
		/// The path, including filename, to the log file. Can have datetime formatting characters in it.
		/// </summary>
		public string? Path { get; set; }

		/// <summary>
		/// How often a new log file is created. Can be Hour, Day, or Never.
		/// </summary>
		public string? Interval { get; set; }

		/// <summary>
		/// The formatting of each message in the log file. Can have datetime formatting characters in it.
		/// Also has {level} and {message} for the log level and message.
		/// </summary>
		public string Formatting { get; set; }

		/// <summary>
		/// The number of backups to keep. This is based on Interval so if Interval is Day and this is 10,
		/// then it will save the last 10 days of logs. If the Path has datetime formatting characters in it
		/// that are more frequent than the Interval, then this will not work correctly.
		/// </summary>
		public int? NumberOfBackups { get; set; }

		/// <summary>
		/// How long to wait until flushing the log file to disk. If null, then it's flushed on every write.
		/// This is the number of minutes to wait.
		/// </summary>
		public int? FlushInterval
		{
			get => _flushInterval;
			set
			{
				if (value == null)
					_flushIntervalTimeSpan = null;
				else
					_flushIntervalTimeSpan = new TimeSpan(0, value.Value, 0);
				_flushInterval = value;
			}
		}

		/// <summary>
		/// How long to wait until flushing the log file to disk. If null, then it's flushed on every write.
		/// Pass in the format hh:mm:ss.
		/// </summary>
		public TimeSpan? FlushIntervalTimeSpan => _flushIntervalTimeSpan;

		/// <summary>
		/// How large the queue can get before it's flushed to disk. If null, then it's flushed on every write.
		/// </summary>
		public int? FlushSize { get; set; }

		public LoggerOptionsBase()
		{
			Formatting = "{level} {HH:mm:ss.fff} [{machine}]-[{scope.username}] {class} - {message} {exception}";
			Interval = "Day";
		}
	}
}