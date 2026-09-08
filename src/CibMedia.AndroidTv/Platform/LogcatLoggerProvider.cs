using Microsoft.Extensions.Logging;
using AndroidLog = Android.Util.Log;

namespace CibMedia.AndroidTv.Platform;

// Everything Core and the resolvers log lands under one tag: adb logcat -s CibMedia.
public sealed class LogcatLoggerProvider : ILoggerProvider
{
    private const string Tag = "CibMedia";

    public ILogger CreateLogger(string categoryName)
    {
        return new LogcatLogger(categoryName);
    }

    public void Dispose()
    {
    }

    private sealed class LogcatLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel is not LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            var line = $"{category}: {formatter(state, exception)}";

            if (exception is not null) line = $"{line}{Environment.NewLine}{exception}";

            switch (logLevel)
            {
                case LogLevel.Trace or LogLevel.Debug:
                    AndroidLog.Debug(Tag, line);
                    break;
                case LogLevel.Information:
                    AndroidLog.Info(Tag, line);
                    break;
                case LogLevel.Warning:
                    AndroidLog.Warn(Tag, line);
                    break;
                default:
                    AndroidLog.Error(Tag, line);
                    break;
            }
        }
    }
}
