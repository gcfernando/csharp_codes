using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;

// Developer ::> Gehan Fernando
// Date      ::> 15-Sep-2025

namespace LogFunction;

/// <summary>
/// A centralized static logger helper that provides strongly-typed
/// and consistent logging methods across different log levels.
/// </summary>
public static class ExLogger
{
    #region Predefined Delegates for Performance
    // Using LoggerMessage.Define<T>() provides compiled delegates
    // for structured logging, which is faster than calling ILogger.Log()
    // with string formatting on every call.

    private static readonly Action<ILogger, string, Exception> _logTraceDelegate =
        LoggerMessage.Define<string>(
            LogLevel.Trace,
            new EventId(0, "TraceEvent"),
            "{Message}");

    private static readonly Action<ILogger, string, Exception> _logDebugDelegate =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1, "DebugEvent"),
            "{Message}");

    private static readonly Action<ILogger, string, Exception> _logInformationDelegate =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, "InformationEvent"),
            "{Message}");

    private static readonly Action<ILogger, string, Exception> _logWarningDelegate =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3, "WarningEvent"),
            "{Message}");

    private static readonly Action<ILogger, string, Exception> _logErrorDelegate =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(4, "ErrorEvent"),
            "{Message}");

    private static readonly Action<ILogger, string, Exception> _logCriticalDelegate =
        LoggerMessage.Define<string>(
            LogLevel.Critical,
            new EventId(5, "CriticalEvent"),
            "{Message}");
    #endregion

    #region Log Action Dictionary
    /// <summary>
    /// A lookup table that maps <see cref="LogLevel"/> to the
    /// appropriate event ID and logging delegate.
    /// This avoids switch/if checks at runtime.
    /// </summary>
    private static readonly ConcurrentDictionary<LogLevel, (EventId EventId, Action<ILogger, string, Exception> Delegate)> _logActions =
        new(
            [
                new KeyValuePair<LogLevel, (EventId, Action<ILogger, string, Exception>)>(LogLevel.Trace, (new EventId(0, "TraceEvent"), _logTraceDelegate)),
                new KeyValuePair<LogLevel, (EventId, Action<ILogger, string, Exception>)>(LogLevel.Debug, (new EventId(1, "DebugEvent"), _logDebugDelegate)),
                new KeyValuePair<LogLevel, (EventId, Action<ILogger, string, Exception>)>(LogLevel.Information, (new EventId(2, "InformationEvent"), _logInformationDelegate)),
                new KeyValuePair<LogLevel, (EventId, Action<ILogger, string, Exception>)>(LogLevel.Warning, (new EventId(3, "WarningEvent"), _logWarningDelegate)),
                new KeyValuePair<LogLevel, (EventId, Action<ILogger, string, Exception>)>(LogLevel.Error, (new EventId(4, "ErrorEvent"), _logErrorDelegate)),
                new KeyValuePair<LogLevel, (EventId, Action<ILogger, string, Exception>)>(LogLevel.Critical, (new EventId(5, "CriticalEvent"), _logCriticalDelegate))
            ]);
    #endregion

    #region Generic Log Method
    /// <summary>
    /// Logs a message with the specified log level and exception.
    /// Automatically chooses between structured logging with arguments
    /// or predefined delegates for performance.
    /// </summary>
    public static void Log(ILogger logger, LogLevel logLevel, string message, Exception exception, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(message);

        // Skip if the log level is disabled
        if (!logger.IsEnabled(logLevel))
        {
            return;
        }

        // Use the optimized delegate if available
        if (_logActions.TryGetValue(logLevel, out var logActionInfo))
        {
            if (args?.Length > 0)
            {
                // Structured logging with arguments
                logger.Log(logLevel, logActionInfo.EventId, exception, message, args);
            }
            else
            {
                // Predefined delegate logging
                logActionInfo.Delegate(logger, message, exception);
            }
        }
        else
        {
            // Fallback: generic log call
            logger.Log(logLevel, default, exception, "{Message}", message);
        }
    }

    /// <summary>
    /// Overload for logging without exceptions.
    /// </summary>
    public static void Log(ILogger logger, LogLevel logLevel, string message, params object[] args) =>
        Log(logger, logLevel, message, null, args);
    #endregion

    #region Convenience Methods for Log Levels
    public static void LogTrace(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Trace, message, args);

    public static void LogDebug(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Debug, message, args);

    public static void LogInformation(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Information, message, args);

    public static void LogWarning(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Warning, message, args);

    public static void LogError(ILogger logger, string message, Exception exception, params object[] args) =>
        Log(logger, LogLevel.Error, message, exception, args);

    public static void LogError(ILogger logger, string message, params object[] args) =>
        Log(logger, LogLevel.Error, message, null, args);

    public static void LogCritical(ILogger logger, string message, Exception exception, params object[] args) =>
        Log(logger, LogLevel.Critical, message, exception, args);
    #endregion

    #region Exception Logging
    /// <summary>
    /// Logs a formatted error message for exceptions at <see cref="LogLevel.Error"/>.
    /// This ensures consistent structured error messages.
    /// </summary>
    public static void LogException(ILogger logger, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(ex);

        if (!logger.IsEnabled(LogLevel.Error))
        {
            return;
        }

        const string errorTitle = "Internal System Error";
        var errorMessage = FormatExceptionMessage(ex, errorTitle);

        // Pass null as exception here to avoid duplicate logging
        _logErrorDelegate(logger, errorMessage.Trim(), null);
    }

    /// <summary>
    /// Builds a detailed exception message with title, exception message,
    /// inner exception, stack trace, and source.
    /// </summary>
    private static string FormatExceptionMessage(Exception ex, string title)
    {
        var sb = new StringBuilder()
            .Append("Title          : ").AppendLine(title)
            .Append("Exception      : ").AppendLine(ex.Message?.Trim() ?? "N/A")
            .AppendLine() // <-- extra blank line
            .Append("Inner Exception: ").AppendLine(ex.InnerException?.Message?.Trim() ?? "N/A")
            .Append("Stack Trace    : ").AppendLine(ex.StackTrace?.Trim() ?? "N/A")
            .AppendLine() // <-- extra blank line
            .Append("Source         : ").AppendLine(ex.Source ?? "N/A");

        return sb.ToString();
    }
    #endregion
}