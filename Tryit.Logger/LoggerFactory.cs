namespace Tryit.Logger;

/// <summary>
/// Provides methods for creating and retrieving loggers for use in application logging.
/// </summary>
/// <remarks>The <see cref="LoggerFactory"/> class is a static utility for managing loggers and their
/// configurations. It supports creating loggers for specific categories and hosts, with optional customization for log
/// file names and headers. Loggers are cached to ensure efficient reuse and consistent logging behavior across the
/// application.</remarks>
public static class LoggerFactory
{
    /// <summary>
    /// Retrieves an <see cref="ILogger"/> instance for the specified category.
    /// </summary>
    /// <typeparam name="T">The type of the host object, which must be non-null.</typeparam>
    /// <param name="host">The host object used to retrieve the logger. Cannot be <see langword="null"/>.</param>
    /// <param name="category">The category name for the logger. Cannot be <see langword="null"/> or whitespace.</param>
    /// <returns>An <see cref="ILogger"/> instance associated with the specified category.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="host"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="category"/> is <see langword="null"/> or consists only of whitespace.</exception>
    public static ILogger GetLogger<T>(this T host, string category)
        where T : notnull
    {
        _ = host ?? throw new ArgumentNullException(nameof(host));

        _ = string.IsNullOrWhiteSpace(category) ? throw new ArgumentException("invalid category") : 0;

        return GetLogger(host, category, null);
    }

    /// <summary>
    /// Retrieves a logger instance for the specified host and category, creating it if it does not already exist.
    /// </summary>
    /// <remarks>This method ensures that a single logger instance is created and reused for the same host and
    /// category combination. If the category contains dots ('.'), they are normalized by removing empty segments. The
    /// logger's configuration is determined based on the provided category and the host's type.</remarks>
    /// <typeparam name="T">The type of the host object. The host must be non-null.</typeparam>
    /// <param name="host">The host object associated with the logger. This parameter cannot be <see langword="null"/>.</param>
    /// <param name="category">The category name used to organize and identify the logger. This parameter cannot be null, empty, or consist
    /// only of whitespace.</param>
    /// <param name="logFileNameGanerator">An optional function that generates the log file name. If provided, it will be used to determine the name of the
    /// log file.</param>
    /// <returns>An <see cref="ILogger"/> instance associated with the specified host and category.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="host"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="category"/> is null, empty, or consists only of whitespace.</exception>
    public static ILogger GetLogger<T>(this T host, string category, Func<string>? logFileNameGanerator)
        where T : notnull
    {
        _ = host ?? throw new ArgumentNullException(nameof(host));

        _ = string.IsNullOrWhiteSpace(category) ? throw new ArgumentException("invalid category") : 0;

        GetHostName(host, out var hostName);

        if (LoggerHelper.LoggerMaps.TryGetValue(hostName, out var logger) == false)
        {
            lock (LoggerHelper.LoggerMaps)
            {
                if (LoggerHelper.LoggerMaps.TryGetValue(hostName, out logger) == false)
                {
                    var loggerWriter = GetWriter(category, logFileNameGanerator);

                    LoggerHelper.LoggerMaps[hostName] = logger = new InnerLog(hostName, loggerWriter);
                }
            }
        }

        return logger;
    }

    /// <summary>
    /// Retrieves an <see cref="ILogger"/> instance associated with the specified file.
    /// </summary>
    /// <remarks>This method ensures that a single <see cref="ILogger"/> instance is created and reused for
    /// the specified file. If a logger does not already exist for the file, a new one is created and stored for future
    /// use.</remarks>
    /// <typeparam name="T">The type of the host object. Must be non-null.</typeparam>
    /// <param name="host">The host object used to associate the logger. Cannot be <see langword="null"/>.</param>
    /// <param name="fileInfo">The file information used to identify the logger. Cannot be <see langword="null"/>.</param>
    /// <returns>An <see cref="ILogger"/> instance associated with the specified file.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="host"/> or <paramref name="fileInfo"/> is <see langword="null"/>.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ILogger GetLogger<T>(this T host, FileInfo fileInfo)
        where T : notnull
    {
        _ = host ?? throw new ArgumentNullException(nameof(host));

        _ = fileInfo ?? throw new ArgumentNullException(nameof(fileInfo));

        GetHostName(host, out var hostName);

        var identity = $"{hostName}{fileInfo.FullName}";

        if (LoggerHelper.LoggerMaps.TryGetValue(identity, out var logger) == false)
        {
            lock (LoggerHelper.LoggerMaps)
            {
                if (LoggerHelper.LoggerMaps.TryGetValue(identity, out logger) == false)
                {
                    var loggerWriter = new TargetFileLoggerWriter(fileInfo);

                    LoggerHelper.LoggerMaps[identity] = logger = new InnerLog(hostName, loggerWriter);
                }
            }
        }

        return logger;
    }

    private static LoggerWriter GetWriter(string category, Func<string>? logFileNameGanerator)
    {
        if (LoggerHelper.LoggerWriterMaps.TryGetValue(category, out LoggerWriter? loggerWriter) == false)
        {
            lock (LoggerHelper.LoggerWriterMaps)
            {
                if (LoggerHelper.LoggerWriterMaps.TryGetValue(category, out loggerWriter) == false)
                {
                    string node = $"{LoggerHelper.nodeName}.{category}";

                    string directoryName = LoggerHelper.LoggerSettings.Read(node, Path.Combine("..", "logs", category));

                    LoggerHelper.LoggerWriterMaps[category] = loggerWriter = new LoggerWriter(directoryName, logFileNameGanerator);
                }
            }
        }

        return loggerWriter;
    }

    private static void GetHostName<T>(T host, out string hostName)
        where T : notnull
    {
        _ = host ?? throw new ArgumentNullException(nameof(host));

        hostName = host switch
        {
            string hostString => hostString!,
            sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal => host.ToString()!,
            Type type => type.Name!,
            _ => host?.GetType()?.Name! ?? "unknown",
        };
    }
}
