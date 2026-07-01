using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tryit.Logger.Internals;

/// <summary>
/// Provides functionality for writing log entries to files with support for log rotation,  asynchronous processing, and
/// configurable logging levels.
/// </summary>
/// <remarks>The <see cref="Logger"/> class is designed to handle logging operations efficiently by
/// maintaining an internal queue for log messages and processing them asynchronously. It supports  features such as log
/// file rotation based on size or date, custom log file naming conventions,  and configurable logging levels. Instances
/// of this class are tracked globally, and a background  task ensures that log entries are written to files in a
/// thread-safe manner.  This class is intended for internal use and is not thread-safe for direct manipulation of its
/// internal state. Use the provided methods to interact with the logging functionality.</remarks>
[DebuggerDisplay("{currentFileInfo.FullName}")]
internal partial class Logger : ConcurrentQueue<string>
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int currentDataTimeDay = DateTime.Now.Day;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string? writeErrorContent = null;

    /// <summary>
    /// A delegate function used to generate the log file name.
    /// </summary>
    /// <remarks>This delegate is intended to provide a mechanism for dynamically generating log file names
    /// based on custom logic. The function should return a string representing the desired log file name.</remarks>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly Func<string> logFileNameGenerator = null!;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private long currentFileSize;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly string directoryName = default!;

    /// <summary>
    /// Represents the maximum index of files that can be processed or managed.
    /// </summary>
    /// <remarks>This field is initialized to -1, indicating that no files have been indexed yet. It is not
    /// directly accessible and is intended for internal use only.</remarks>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int maxFileIndex = -1;

    /// <summary>
    /// Represents the current file information associated with the operation.
    /// </summary>
    /// <remarks>This field is marked with <see cref="DebuggerBrowsableAttribute"/> to hide it from debugger
    /// display. It is intended for internal use and should not be accessed directly.</remarks>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private FileInfo? currentFileInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerWriter"/> class, which is responsible for writing log entries
    /// to a file.
    /// </summary>
    /// <remarks>This constructor ensures that the specified logging directory exists and initializes the
    /// logging configuration,  including the maximum file size and minimum logging level. Multiple instances of <see
    /// cref="Logger"/> are tracked globally.</remarks>
    /// <param name="nodeName">The name of the logging node, used to identify the source of the logs.</param>
    /// <param name="targetDirName">The default directory where log files will be created. If the directory does not exist, it will be created.</param>
    /// <param name="logFileNameGanerator">An optional function to generate log file names dynamically. If not provided, a default naming convention will
    /// be used.</param>
    public Logger(object host, string targetDirName, Func<string>? logFileNameGanerator = null)
    {
        hostName = host switch
        {
            string hostString => hostString,
            Type type => type.Name,
            _ => host?.GetType()?.Name ?? string.Empty,
        };

        logFileNameGenerator = logFileNameGanerator!;

        directoryName = targetDirName;

        lock (LoggerHelper.Writers)
        {
            LoggerHelper.Writers.Add(this);
        }
    }

    /// <summary>
    /// Writes a formatted log message to the internal log queue.
    /// </summary>
    /// <remarks>This method formats the log message with the provided arguments, appends metadata such as the current
    /// timestamp and thread information, and enqueues the message for asynchronous processing. If the <paramref
    /// name="hostName"/> or <paramref name="format"/> is null, empty, or whitespace, the method will return without
    /// performing any action. Similarly, messages with a <paramref name="loggerLevel"/> below the configured minimum
    /// logging level will be ignored.</remarks>
    /// <param name="hostName">The name of the host generating the log message. Cannot be null, empty, or whitespace.</param>
    /// <param name="format">The format string for the log message. Cannot be null, empty, or whitespace. Supports composite formatting.</param>
    /// <param name="args">An array of objects to format into the <paramref name="format"/> string. Can be null or empty if no formatting is
    /// required.</param>
    /// <param name="loggerLevel">The severity level of the log message. Messages with a level lower than the configured minimum logging level will be
    /// ignored.</param>
    internal void WriteLogger(string hostName, string format, object[] args, LoggerLevel loggerLevel)
    {
        if (loggerLevel < LoggerHelper.loggerConfigure.MinLoggerLevel)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(hostName) || string.IsNullOrWhiteSpace(format))
        {
            return;
        }

        if (args != null && args.Length > 0)
        {
            try
            {
                format = string.Format(format, args);
            }
            catch (Exception ex)
            {
                format = string.Concat(format, string.Join(" ", args), Environment.NewLine, ex.GetBaseException());
            }
        }

        Thread currentThread = Thread.CurrentThread;
        string threadName = (currentThread.Name ?? currentThread.ManagedThreadId.ToString()).PadLeft(2);
        string currentDateTime = DateTime.Now.ToString("HH:mm:ss.fff");

        if (LoggerHelper.LoggerLevelMaps.TryGetValue(loggerLevel, out string? loggerLevelString) == false)
        {
            loggerLevelString = "     ";
        }

        string message = string.Format("{0},{1},{2},{3},{4}", currentDateTime, loggerLevelString, threadName, hostName, format);

        Enqueue(message);
    }

    /// <summary>
    /// Generates and retrieves the full path of the log file to be used for logging.
    /// </summary>
    /// <remarks>This method determines the appropriate log file based on the current date, file naming conventions,
    /// and file size constraints. If a custom log file name generator is provided, it will be used to  generate the base
    /// file name. The method ensures that the log file is rotated when the maximum file  size is exceeded or when the date
    /// changes.</remarks>
    /// <returns>The full path of the log file to be used. If no suitable file exists, a new file is created.</returns>
    protected virtual void FileInitialize(out FileInfo fileInfo, ref DateTime dateTime, ref DateTime deleteDatetime)
    {
        while (true)
        {
            if (CanUseThisFile(ref dateTime))
            {
                fileInfo = currentFileInfo!;

                return;
            }

            currentFileSize = 0;

            string fileName = FileNameGenerator();

            if (maxFileIndex <= 0)
            {
                DateTime innerDeleteDatetime = deleteDatetime;

                DirectoryInfo directoryInfo = new DirectoryInfo(directoryName);

                if (directoryInfo.Exists == false)
                {
                    directoryInfo.Create();
                }

                FileBlock[] items = directoryInfo.EnumerateFiles().Select(i => new FileBlock(i, innerDeleteDatetime)).ToArray();

                var beginTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day);

                maxFileIndex = items.Where(x => x.CreateTime == beginTime).OrderByDescending(x => x.Index).FirstOrDefault()?.Index ?? 0;

                maxFileIndex = maxFileIndex < 1 ? 1 : maxFileIndex;

                //items.ForEach(x => x.TryDelete());

                //items = null!;
            }

            currentFileInfo = fileInfo = new(Path.Combine(directoryName, $"{fileName}.{maxFileIndex}{LoggerHelper.LoggerFileExtension}"));

            if (currentFileInfo.Exists == false)
            {
                currentFileInfo.Directory?.Create();

                return;
            }

            if (currentFileInfo.Exists)
            {
                currentFileSize = currentFileInfo.Length;
            }

            if (currentFileSize >= LoggerHelper.loggerConfigure.MaxFileSize) //超过指定的文件大小
            {
                maxFileIndex++;
            }
        }
    }

    #region write

    internal bool Write(StringBuilder fileBuilder, ref DateTime dateTime, ref DateTime deleteDatetime)
    {
        if (IsEmpty)
        {
            return false;
        }

        FileInitialize(out FileInfo? fileInfo, ref dateTime, ref deleteDatetime);

        if (writeErrorContent is not null)
        {
            fileBuilder.Append(writeErrorContent);

            writeErrorContent = null;
        }

        while (TryDequeue(out string? content))
        {
            currentFileSize = currentFileSize + Encoding.UTF8.GetByteCount(content) + LoggerHelper.NewLineCount;

            fileBuilder.AppendLine(content);
        }

        string fileContent = fileBuilder.ToString();

        try
        {
            File.AppendAllText(fileInfo.FullName, fileContent, Encoding.UTF8);
        }
        catch (DirectoryNotFoundException)
        {
            fileInfo.Directory?.Create();

            writeErrorContent = fileContent;
        }
        catch (Exception)
        {
            writeErrorContent = fileContent;
        }

        return true;
    }

    private bool CanUseThisFile(ref DateTime dateTime)
    {
        if (currentFileInfo is null)
        {
            return false;
        }

        if (currentDataTimeDay != dateTime.Day)
        {
            currentDataTimeDay = dateTime.Day;

            maxFileIndex = 1;

            return false;
        }

        if (currentFileSize >= LoggerHelper.loggerConfigure.MaxFileSize)
        {
            maxFileIndex++;
            return false; //文件大小 比 最大值 还大的话 那就不能用了
        }

        return true;
    }

    private string FileNameGenerator()
    {
        if (logFileNameGenerator is not null)
        {
            string fileName = logFileNameGenerator();

            return fileName.EndsWith(LoggerHelper.LoggerFileExtension, StringComparison.OrdinalIgnoreCase) ? Path.GetFileNameWithoutExtension(fileName) : fileName;
        }

        return $"{DateTime.Now:yyyy-MM-dd}";
    }

    #endregion

    internal class TargetFileLoggerWriter : Logger
    {
        public TargetFileLoggerWriter(object host, FileInfo fileInfo, string defaultDirName, Func<string>? logFileNameGanerator = null)
            : base(host, defaultDirName, logFileNameGanerator)
        {
            currentFileInfo = fileInfo;
            fileInfo.Directory?.Create();
        }

        protected override void FileInitialize(out FileInfo fileInfo, ref DateTime dateTime, ref DateTime deleteDateTime)
        {
            fileInfo = currentFileInfo!;
        }
    }

    private class FileBlock
    {
        public readonly FileInfo FileInfo;
        private readonly DateTime deleteDateTime;

        public FileBlock(FileInfo fileInfo, DateTime deleteDateTime)
        {
            FileInfo = fileInfo;
            this.deleteDateTime = deleteDateTime;
            var Splits = FileInfo.Name.Split('.');

            if (Splits.Length != 3)
            {
                CanDelete = true;
                return;
            }

            var dateTime = (DateTime.TryParse(Splits[0], out DateTime dd) ? dd : DateTime.MaxValue);

            CreateTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day);

            Index = int.TryParse(Splits[1], out int index) ? index : -1;
        }

        public DateTime CreateTime { get; }

        public int Index;

        public bool CanDelete
        {
            get => field || CreateTime <= deleteDateTime;
            set;
        }

        public bool TryDelete()
        {
            if (CanDelete)
            {
                try
                {
                    File.Delete(FileInfo.FullName);
                }
                catch
                {
                    //ignore
                }
            }

            return false;
        }
    }
}
