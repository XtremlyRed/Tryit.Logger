namespace Tryit.Logger;

/// <summary>
/// Represents the severity levels for logging messages in an application or system.
/// </summary>
public enum LoggerLevel
{
    /// <summary>
    /// Represents a trace level used for logging or diagnostic purposes.
    /// </summary>
    /// <remarks>This enumeration value is typically used to indicate that detailed information about the
    /// execution of an application should be logged. It is often used for debugging or tracing  the flow of execution
    /// in an application.</remarks>
    Trace = 1 << 0,

    /// <summary>
    /// Represents the debug mode for the application or system.
    /// </summary>
    /// <remarks>This enumeration value is typically used to indicate that the application is running in a
    /// debug state, which may enable additional logging, diagnostics, or developer-specific features.</remarks>
    Debug = 1 << 1,

    /// <summary>
    /// Represents informational messages or data.
    /// </summary>
    Info = 1 << 2,

    /// <summary>
    /// Represents a warning level or state.
    /// </summary>
    /// <remarks>This enumeration value is typically used to indicate a warning condition in logging, monitoring, or
    /// other diagnostic contexts.</remarks>
    Warn = 1 << 3,

    /// <summary>
    /// Represents an error state or condition.
    /// </summary>
    /// <remarks>This enumeration value is typically used to indicate that an operation has failed or
    /// encountered an error.</remarks>
    Error = 1 << 4,

    /// <summary>
    /// Represents a fatal error level, typically used to indicate critical issues that cause the application to
    /// terminate.
    /// </summary>
    Fatal = 1 << 5,
}
