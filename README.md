# TradeWindsLoggerProvider

We are using this as our logging mechanism for our web app (along with the default Microsoft logging providers such as to Application Insights). It works well for us. But there are sophisticated alternatives out there that might be better for you.

Here is why we ended up with this. We started with Serilog but it had (may be fixed now) a serious bug where it crashed our entire web app when we added the Azure BLOB sink. We spent 3 days trying to figure out what the issue was, and then gave up. (I reported the issue and one of the contributors replied that Blazor was not a supported system for their sink.)

So I created this. If you face the same problem, or for some other reason, need a basic simple LoggerProvider, here it is. It writes to File and Blob. It is in use and is fast and has very little overhead.

### Mapping configuration to your LoggerProvider

The magic is the `ProviderAlias`:
```csharp
[ProviderAlias("Blob")]
public class BlobLoggerProvider : LoggerProviderBase
```

### Unit Tests

I have no idea how to write unit tests for a LoggerProvider as it's all part of the ASP.NET system. If you do, please add that to this project.

We did test this thoroughly. But the testing was as part of our web app, including stepping through in places with the debugger.

### License

This is under the MIT license. If you find this very useful I ask (not a requirement) that you consider reading my book [I DON’T KNOW WHAT I’M DOING!: How a Programmer Became a Successful Startup CEO](https://a.co/d/bEpDlJR).

And if you like it, please review it on Amazon and/or GoodReads. The number of legitimate reviews helps a lot. Much appreciated.