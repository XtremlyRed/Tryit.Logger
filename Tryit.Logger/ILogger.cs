using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tryit.Logger;

/// <summary>
/// Defines a contract for logging messages at various levels of severity.
/// </summary>
/// <remarks>The <see cref="ILogger"/> interface provides methods for logging messages at different levels,  such
/// as Trace, Debug, Info, Warn, Error, and Fatal. Each method accepts a message format string  and optional parameters
/// to format the message. This interface is typically implemented by logging  frameworks or custom logging solutions to
/// provide consistent logging functionality across an application.</remarks>
public interface ILogger
{
    /// <summary>
    /// Writes a formatted trace message to the application's trace listeners.
    /// </summary>
    /// <remarks>This method is typically used for debugging or logging purposes.  Ensure that the <paramref
    /// name="format"/> string is not null or empty to avoid runtime errors.</remarks>
    /// <param name="format">A composite format string that specifies the format of the trace message.  The format string can include
    /// placeholders, which are replaced by the values of the <paramref name="parameters"/> array.</param>
    /// <param name="parameters">An array of objects to format and include in the trace message.  Each object corresponds to a placeholder in the
    /// <paramref name="format"/> string.</param>
    void Trace(string format, params object[] parameters);

    /// <summary>
    /// Logs a debug message with the specified format and parameters.
    /// </summary>
    /// <remarks>This method formats the message using the specified <paramref name="format"/> string and logs
    /// it at the debug level.  Ensure that the <paramref name="format"/> string is not null or empty to avoid
    /// unexpected behavior.</remarks>
    /// <param name="format">A composite format string that contains text intermixed with format items, which correspond to objects in the
    /// <paramref name="parameters"/> array.</param>
    /// <param name="parameters">An array of objects to format and include in the debug message. Can be empty if no format items are used in
    /// <paramref name="format"/>.</param>
    void Debug(string format, params object[] parameters);

    /// <summary>
    /// Logs an informational message with optional formatting parameters.
    /// </summary>
    /// <remarks>This method is typically used to log non-critical information that may assist in debugging or
    /// monitoring application behavior. Ensure that the <paramref name="format"/> string and <paramref name="parameters"/>
    /// align to avoid runtime formatting errors.</remarks>
    /// <param name="format">A composite format string that specifies the message to log. The format string can include placeholders in the form
    /// of curly braces (e.g., "{0}") to be replaced by the corresponding elements in <paramref name="parameters"/>.</param>
    /// <param name="parameters">An array of objects to format and include in the message. Each object corresponds to a placeholder in the <paramref
    /// name="format"/> string. This parameter can be omitted if no placeholders are used.</param>
    void Info(string format, params object[] parameters);

    /// <summary>
    /// Logs a warning message with the specified format and parameters.
    /// </summary>
    /// <remarks>This method is typically used to log non-critical issues or potential problems that do not require
    /// immediate attention. Ensure that the <paramref name="format"/> string and <paramref name="parameters"/> align to
    /// avoid formatting errors.</remarks>
    /// <param name="format">A composite format string that specifies the format of the warning message.  The format string can include
    /// placeholders, which are replaced by the values in <paramref name="parameters"/>.</param>
    /// <param name="parameters">An array of objects to format and include in the warning message.  Each object corresponds to a placeholder in the
    /// <paramref name="format"/> string.</param>
    void Warn(string format, params object[] parameters);

    /// <summary>
    /// Logs an error message with the specified format and parameters.
    /// </summary>
    /// <remarks>This method is typically used to log error-level messages in a structured format.  Ensure that the
    /// <paramref name="format"/> string and <paramref name="parameters"/> align to avoid formatting errors.</remarks>
    /// <param name="format">A composite format string that specifies the format of the error message.  This string can include placeholders,
    /// which are replaced by the values in <paramref name="parameters"/>.</param>
    /// <param name="parameters">An array of objects to format and include in the error message.  Each object corresponds to a placeholder in the
    /// <paramref name="format"/> string.</param>
    void Error(string format, params object[] parameters);

    /// <summary>
    /// Logs a fatal error message with the specified format and parameters.
    /// </summary>
    /// <remarks>Fatal log messages indicate critical errors that cause the application to terminate or enter an
    /// unrecoverable state.  Use this method to log such errors for diagnostic purposes.</remarks>
    /// <param name="format">A composite format string that specifies the format of the log message.  This string can include placeholders, which
    /// are replaced by the values in <paramref name="parameters"/>.</param>
    /// <param name="parameters">An array of objects to format and include in the log message.  These values are inserted into the placeholders
    /// defined in <paramref name="format"/>.</param>
    void Fatal(string format, params object[] parameters);

    /// <summary>
    /// Logs a message with the specified log level and formatted content.
    /// </summary>
    /// <remarks>This method is intended for internal use and may not be visible in standard IntelliSense.</remarks>
    /// <param name="loggerLevel">The severity level of the log message.</param>
    /// <param name="format">A composite format string that specifies the format of the log message.</param>
    /// <param name="parameters">An array of objects to format and include in the log message.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    void Log(LoggerLevel loggerLevel, string format, params object[] parameters);
}
