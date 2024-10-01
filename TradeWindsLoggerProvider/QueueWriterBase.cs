
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

using System.Collections.Concurrent;

namespace TradeWindsLoggerProvider
{

	public abstract class QueueWriterBase : IDisposable
	{
		private readonly ConcurrentQueue<string> _queue = new();
		private readonly ManualResetEventSlim _newItemEventSlim = new(false);
		private readonly CancellationTokenSource _cancellationTokenSource = new();
		private Thread? _workerThread;

		private readonly LoggerOptionsBase _options;
		private int _queueSize;
		// keep the System.Threading as there's also a System.Timers.Timer
		private Timer? _flushTimer;

		protected QueueWriterBase(LoggerOptionsBase options)
		{
			_options = options;
			_flushTimer = null;
		}

		public void Start()
		{
			_workerThread = new Thread(ProcessQueue)
			{
				IsBackground = true,
				Priority = ThreadPriority.BelowNormal
			};
			_workerThread.Start();

			// if FlushIntervalTimeSpan is set, then we signal _newItemEventSlim on that interval.
			// we don't bother restarting the timer when ProcessQueue() runs because no big deal
			// if it writes more often.
			if (_options.FlushIntervalTimeSpan != null)
				_flushTimer = new Timer(_ => _newItemEventSlim.Set(), null,
					_options.FlushIntervalTimeSpan.Value, _options.FlushIntervalTimeSpan.Value);
		}

		/*
		 * The Write/Flush is not async because this is all in a background thread and there's no win
		 * to creating tasks. We want to run everything in this thread.
		 */
		public abstract void WriteLine(List<string> lines);

		/// <summary>
		/// Flushes the output to disk. This is called when the queue is flushed. This does not return until
		/// after the flush is complete.
		/// </summary>
		public virtual void Flush()
		{
		}

		public virtual void Close()
		{
		}

		/// <summary>
		/// Write a string to the file. This is thread safe and adds the string to be written
		/// by the background thread - in order added.
		/// </summary>
		/// <param name="line">The line of text to write.</param>
		public void Enqueue(string line)
		{
			_queue.Enqueue(line);

			// process if the queue is large enough
			if (_options.FlushSize != null)
			{
				_queueSize += line.Length;
				if (_queueSize >= _options.FlushSize)
				{
					// we zero it here so that another call to Enqueue doesn't add to it before we process
					_queueSize = 0;
					_newItemEventSlim.Set();
					return;
				}
			}

			// if neither is set, then we process on every line.
			if (_options.FlushSize == null && _flushTimer == null)
				_newItemEventSlim.Set();
		}

		/// <summary>
		/// If the logging output is buffered, then this flushes the buffer. It does not return until
		/// the buffer has written all of its contents.
		/// </summary>
		public void FlushQueue()
		{
			_newItemEventSlim.Set();
			// wait for _newItemEventSlim to be reset
			for (var delayMilliseconds = 0; delayMilliseconds < 500; delayMilliseconds *= 2)
			{
				if (!_newItemEventSlim.IsSet)
					break;
				Thread.Sleep(delayMilliseconds);
			}
		}

		private void ProcessQueue()
		{
			try
			{
				while (!_cancellationTokenSource.IsCancellationRequested)
				{
					_newItemEventSlim.Wait(_cancellationTokenSource.Token);

					var lines = new List<string>();
					while (_queue.TryDequeue(out var message))
						lines.Add(message);

					// reset again here so start over no matter how we got here.
					_queueSize = 0;

					WriteLine(lines);

					Flush();

					_newItemEventSlim.Reset();
				}
			}
			catch (OperationCanceledException)
			{
				// ignore
			}
			catch (Exception ex)
			{
				System.Diagnostics.Trace.WriteLine("Error in QueueWriterBase.ProcessQueue: " + ex.Message);
				_queue.Enqueue($"***** QueueWriterBase.ProcessQueue threw {ex.Message}");
				throw;
			}
		}

		/// <inheritdoc />
		public void Dispose()
		{
			_cancellationTokenSource.Cancel();

			var lines = new List<string>();
			while (_queue.TryDequeue(out var message))
				lines.Add(message);
			WriteLine(lines);

			Close();

			GC.SuppressFinalize(this);
		}
	}
}