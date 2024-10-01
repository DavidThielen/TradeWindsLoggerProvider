
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

namespace TradeWindsLoggerProvider;

public abstract class LoggerProviderBase : ILoggerProvider, ISupportExternalScope
{
	/// <summary>
	/// How often to create a new log file.
	/// </summary>
	protected enum IntervalMode
	{
		/// <summary>
		/// Create a new file on the top of the minute.
		/// </summary>
		Minute,
		/// <summary>
		/// Create a new file on the top of the hour.
		/// </summary>
		Hour,
		/// <summary>
		/// Create a new file at midnight.
		/// </summary>
		Day,
		/// <summary>
		/// Never create a new file.
		/// </summary>
		Never
	}

	// https://stackoverflow.com/questions/76592214/c-sharp-how-to-use-isupportexternalscope-with-customer-ilogger-iloggerprovider-a
	protected internal IExternalScopeProvider? ExternalScopeProvider;

	void ISupportExternalScope.SetScopeProvider(IExternalScopeProvider? externalScopeProvider) =>
		ExternalScopeProvider = externalScopeProvider;

	protected IDisposable? SettingsChangeToken;
	protected IntervalMode? Interval;

	protected abstract LoggerOptionsBase Options { get; }

	/// <summary>
	/// This performs the writing to disk in a background thread.
	/// </summary>
	private QueueWriterBase? _queueWriter;

	/// <summary>
	/// This fires when it's time to rename the output log file. It does this by closing
	/// down the existing StringQueueWriter and creating a new one.
	/// </summary>
	private Timer? _timerQueueWriter;

	protected void Start()
	{
		// create the writer & timer - they don't exist yet.
		CreateWriter();
		CreateTimer();
	}

	protected abstract QueueWriterBase CreateQueueWriter();

	protected abstract void Cleanup();

	private async Task RenewTimer()
	{
		if (_timerQueueWriter != null)
			await _timerQueueWriter.DisposeAsync();

		CreateTimer();
	}

	// we create a new file to write to, and then shut down the existing one.
	private async void ChangeFilename(object? _)
	{

		_queueWriter?.FlushQueue();
		// save the existing writer to close down end of this method
		// this way there's always a writer running for calls to Enqueue().
		var existingWriter = _queueWriter;

		CreateWriter();

		// close down the existing one.
		existingWriter?.Dispose();

		// and set to call this again on the next day/hour.
		await RenewTimer();
	}

	// create the new writer
	private void CreateWriter()
	{

		_queueWriter = CreateQueueWriter();
		_queueWriter.Start();
	}

	// set the timer to fire on the next interval.
	private void CreateTimer()
	{
		if (Interval == IntervalMode.Never)
			return;

		// get the interval to midnight or the next hour
		DateTime nextChange;
		var utcNow = DateTime.UtcNow;
		switch (Interval)
		{
			case IntervalMode.Day:
				nextChange = utcNow.Date.AddDays(1);
				break;
			case IntervalMode.Hour:
				var nextHour = utcNow.AddHours(1);
				nextChange = nextHour.Date.AddHours(nextHour.Hour);
				break;
			case IntervalMode.Minute:
				// round up to the next full minute
				var nextMinute = utcNow.AddMinutes(1);
				if (nextMinute.Second >= 30)
					nextMinute = nextMinute.AddMinutes(1);
				nextChange = nextMinute.Date.AddHours(nextMinute.Hour).AddMinutes(nextMinute.Minute);
				break;
			default:
				throw new ArgumentException("Invalid interval for LoggerProvider: " + Interval);
		}

		var next = nextChange - utcNow;

		// set the timer to fire when we hit midnight or the next hour
		_timerQueueWriter = new Timer(ChangeFilename, null, next, Timeout.InfiniteTimeSpan);

		// get rid of old files.
		Cleanup();
	}

	/// <inheritdoc />
	public ILogger CreateLogger(string categoryName)
	{
		return new LoggerBase(categoryName, this, Options);
	}

	/// <summary>
	/// Write a string to the file. This is thread safe and adds the string to be written
	/// by the background thread - in order added.
	/// </summary>
	/// <param name="line">The line of text to write.</param>
	public void Enqueue(string line)
	{
		_queueWriter?.Enqueue(line);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Enqueue("***** Shutting down *****");
		_queueWriter?.FlushQueue();
		SettingsChangeToken?.Dispose();
		SettingsChangeToken = null;
		_timerQueueWriter?.Dispose();
		_timerQueueWriter = null;
		GC.SuppressFinalize(this);
	}

	protected IntervalMode GetInterval(string? interval)
	{
		var intervalLower = interval?.ToLowerInvariant();
		return intervalLower switch
		{
			"minute" => IntervalMode.Minute,
			"hour" => IntervalMode.Hour,
			"day" => IntervalMode.Day,
			"never" => IntervalMode.Never,
			_ => throw new Exception("Invalid interval for LoggerProvider: " + interval)
		};
	}

	protected TimeSpan? GetIntervalTimeSpan(IntervalMode mode)
	{
		return mode switch
		{
			IntervalMode.Minute => new TimeSpan(0, 0, 1, 0),
			IntervalMode.Hour => new TimeSpan(0, 1, 0, 0),
			IntervalMode.Day => new TimeSpan(1, 0, 0, 0),
			IntervalMode.Never => null,
			_ => throw new Exception("Invalid interval for LoggerProvider: " + mode)
		};
	}
}