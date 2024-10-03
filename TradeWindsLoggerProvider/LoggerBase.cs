
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
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TradeWindsCommon.Extensions;

namespace TradeWindsLoggerProvider
{
	/// <summary>
	/// An ILogger that writes to a text file or BLOB.
	/// </summary>
	public class LoggerBase : ILogger
	{
		private string Name { get; }
		public LoggerOptionsBase Options { get; }
		private readonly LoggerProviderBase _provider;

		public LoggerBase(string name, LoggerProviderBase provider, LoggerOptionsBase options)
		{
			Name = name;
			Options = options;
			_provider = provider;
		}

		/// <inheritdoc />
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull
		{
			return null;
		}

		/// <inheritdoc />
		public bool IsEnabled(LogLevel logLevel) => true;

		// use this to replace the {message} and {exception} placeholders until after the date
		private static readonly string MessagePlaceHolder = "@*&$bbb%#^!~`";
		private static readonly string ExceptionPlaceHolder = "@*$aaa#^!~`";

		/// <inheritdoc />
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, 
			Func<TState, Exception?, string> formatter)
		{

			try
			{
				// get the formatter message, place it into the Formatting pattern.
				var message = formatter(state, null);

				// substitute in the placeholders
				var formattedMessage = Options.Formatting.Replace("{message}", MessagePlaceHolder);
				formattedMessage = formattedMessage.Replace("{exception}", ExceptionPlaceHolder);

				string exceptionMessage;
				if (exception == null)
					exceptionMessage = string.Empty;
				else
				{
					var sb = new StringBuilder();
					while (exception != null)
					{
						sb.Append(exception.GetType().Name)
							.Append(": ")
							.AppendLine(exception.Message)
							.AppendLine(exception.StackTrace);
						exception = exception.InnerException;
					}
					exceptionMessage = sb.ToString().Trim();
				}

				// replace the scope pattern items.
				if (_provider.ExternalScopeProvider != null &&
					Options.Formatting.IndexOf("{scope.", StringComparison.Ordinal) >= 0)
				{
					_provider.ExternalScopeProvider.ForEachScope((value, _) =>
						{
							if (value is not IEnumerable<KeyValuePair<string, object>> props)
								return;
							foreach (var pair in props)
							{
								var key = "{scope." + pair.Key + "}";
								formattedMessage = formattedMessage.Replace(key, pair.Value?.ToString());
							}
						},
						state);
				}

				// replace any scoped items we don't have
				formattedMessage = Regex.Replace(formattedMessage, "{scope\\..*?}", string.Empty);

				// replace the pattern items.
				formattedMessage = formattedMessage.Replace("{name}", Name[(Name.LastIndexOf('.') + 1)..]);
				formattedMessage = formattedMessage.Replace("{class}", Name);
				formattedMessage = formattedMessage.Replace("{machine}", Environment.MachineName);
				formattedMessage = formattedMessage.Replace("{event}", Convert.ToString(eventId.Id));
				formattedMessage = formattedMessage.Replace("{level}", logLevel.ToString());
				formattedMessage = formattedMessage.FormatWithDate(DateTimeKind.Utc);

				// and now the message and exception
				formattedMessage = formattedMessage.Replace(MessagePlaceHolder, message);
				formattedMessage = formattedMessage.Replace(ExceptionPlaceHolder, exceptionMessage);

				_provider.Enqueue(formattedMessage.Trim());
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex.ToString());
			}
		}
	}
}
