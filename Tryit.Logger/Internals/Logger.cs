using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tryit.Logger.Internals;

/// <summary>
/// Provides functionality for logging messages at various severity levels.
/// </summary>
/// <remarks>The <see cref="Logger"/> class is designed to log messages with different levels of severity, such as
/// Debug, Info, Warn, Error, Fatal, and Trace. It supports formatted messages and allows developers to specify the log
/// level for each message. This class is typically used to record application events, diagnostic information, and
/// errors for troubleshooting and monitoring purposes.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
internal partial class Logger : ILogger
{
    /// <summary>
    /// Represents the host name associated with the current instance.
    /// </summary>
    /// <remarks>This field is marked as private and is not directly accessible. It is used internally to
    /// store the host name value for operations that require it.</remarks>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly string hostName;

    /// <summary>
    /// Logs a debug-level message with the specified format and parameters.
    /// </summary>
    /// <remarks>This method writes a debug-level log entry. Use it to record detailed diagnostic information
    /// that is  useful during development or troubleshooting. The actual output depends on the configuration of the
    /// underlying logging system.</remarks>
    /// <param name="format">A composite format string that specifies the message to log. The format string can include placeholders for the
    /// values in <paramref name="parameters"/> using standard .NET composite formatting.</param>
    /// <param name="parameters">An array of objects to format and include in the log message. These values are substituted into the  <paramref
    /// name="format"/> string at the corresponding placeholders.</param>
    public void Debug(string format, params object[] parameters)
    {
        this.WriteLogger(hostName, format, parameters, LoggerLevel.Debug);
    }

    /// <summary>
    /// Logs an error message with the specified format and parameters.
    /// </summary>
    /// <remarks>This method writes the formatted error message to the logger with an error severity level.  Ensure
    /// that <paramref name="format"/> is not null or empty to avoid unexpected behavior.</remarks>
    /// <param name="format">A composite format string that specifies the format of the error message.  This string can include placeholders,
    /// which are replaced by the values in <paramref name="parameters"/>.</param>
    /// <param name="parameters">An array of objects to format and include in the error message.  These values are inserted into the placeholders
    /// defined in <paramref name="format"/>.</param>
    public void Error(string format, params object[] parameters)
    {
        this.WriteLogger(hostName, format, parameters, LoggerLevel.Error);
    }

    /// <summary>
    /// Logs a message at the "Fatal" level.
    /// </summary>
    /// <remarks>Use this method to log critical errors or failures that require immediate attention and may cause the
    /// application to terminate. The logged message is written with the "Fatal" severity level.</remarks>
    /// <param name="format">A composite format string that specifies the message to log. The format string can include placeholders for the
    /// values in <paramref name="parameters"/>.</param>
    /// <param name="parameters">An array of objects to format and include in the log message. These values are substituted into the  <paramref
    /// name="format"/> string.</param>
    public void Fatal(string format, params object[] parameters)
    {
        this.WriteLogger(hostName, format, parameters, LoggerLevel.Fatal);
    }

    /// <summary>
    /// Logs an informational message with the specified format and parameters.
    /// </summary>
    /// <remarks>This method writes a log entry at the informational level. Use it to log general application events
    /// or messages that do not indicate an error or warning.</remarks>
    /// <param name="format">A composite format string that contains text intermixed with format items.  Format items correspond to objects in
    /// the <paramref name="parameters"/> array.</param>
    /// <param name="parameters">An array of objects to format and include in the log message. Can be empty if no format items are used.</param>
    public void Info(string format, params object[] parameters)
    {
        this.WriteLogger(hostName, format, parameters, LoggerLevel.Info);
    }

    /// <summary>
    /// Writes a trace-level log message using the specified format and parameters.
    /// </summary>
    /// <remarks>This method formats the log message using the provided <paramref name="format"/> string and
    /// <paramref name="parameters"/>,  and writes it to the logger with a trace-level severity. Use this method to log
    /// detailed diagnostic information.</remarks>
    /// <param name="format">A composite format string that specifies the content of the log message. Cannot be null or empty.</param>
    /// <param name="parameters">An array of objects to format and include in the log message. Can be empty if no parameters are required.</param>
    public void Trace(string format, params object[] parameters)
    {
        this.WriteLogger(hostName, format, parameters, LoggerLevel.Trace);
    }

    /// <summary>
    /// Logs a warning message with the specified format and parameters.
    /// </summary>
    /// <remarks>This method writes a log entry at the warning level, which typically indicates a potential issue or
    /// unexpected behavior  that does not prevent the application from continuing to function.</remarks>
    /// <param name="format">A composite format string that contains text intermixed with format items.  Format items are replaced by the string
    /// representation of corresponding objects in <paramref name="parameters"/>.</param>
    /// <param name="parameters">An array of objects to format and include in the warning message.  If no parameters are provided, the format string
    /// is logged as-is.</param>
    public void Warn(string format, params object[] parameters)
    {
        this.WriteLogger(hostName, format, parameters, LoggerLevel.Warn);
    }

    /// <summary>
    /// Logs a message with the specified log level and formatted content.
    /// </summary>
    /// <remarks>This method writes the log message to the underlying logger using the specified log level.
    /// Ensure that <paramref name="format"/> is a valid composite format string to avoid runtime errors.</remarks>
    /// <param name="loggerLevel">The severity level of the log message.</param>
    /// <param name="format">A composite format string that specifies the format of the log message.  This string can include placeholders
    /// for the values in <paramref name="parameters"/>.</param>
    /// <param name="parameters">An array of objects to format and include in the log message.  These values are inserted into the placeholders
    /// defined in <paramref name="format"/>.</param>
    public void Log(LoggerLevel loggerLevel, string format, params object[] parameters)
    {
        this.WriteLogger(hostName, format, parameters, loggerLevel);
    }
}
