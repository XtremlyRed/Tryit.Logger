using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;

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
[DebuggerDisplay("{currentFileInfo?.FullName}")]
internal partial class LoggerWriter : ConcurrentQueue<LoggerItem>
{
    private int currentDataTimeDay = DateTime.Now.Day;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int hasWritenLength = 0;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private byte[] writeErrorContent = [];

    /// <summary>
    /// A delegate function used to generate the log file name.
    /// </summary>
    /// <remarks>This delegate is intended to provide a mechanism for dynamically generating log file names
    /// based on custom logic. The function should return a string representing the desired log file name.</remarks>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly Func<string> logFileNameGenerator = null!;

    private long currentFileSize;

    private readonly string directoryName = default!;

    /// <summary>
    /// Represents the maximum index of files that can be processed or managed.
    /// </summary>
    /// <remarks>This field is initialized to -1, indicating that no files have been indexed yet. It is not
    /// directly accessible and is intended for internal use only.</remarks>
    private int maxFileIndex = -1;

    /// <summary>
    /// Represents the current file information associated with the operation.
    /// </summary>
    /// <remarks>This field is marked with <see cref="DebuggerBrowsableAttribute"/> to hide it from debugger
    /// display. It is intended for internal use and should not be accessed directly.</remarks>
    private FileInfo? currentFileInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerWriter"/> class, which is responsible for writing log entries
    /// to a file.
    /// </summary>
    /// <remarks>This constructor ensures that the specified logging directory exists and initializes the
    /// logging configuration,  including the maximum file size and minimum logging level. Multiple instances of <see
    /// cref="Logger"/> are tracked globally.</remarks>
    /// <param name="directoryName">The default directory where log files will be created. If the directory does not exist, it will be created.</param>
    /// <param name="logFileNameGanerator">An optional function to generate log file names dynamically. If not provided, a default naming convention will
    /// be used.</param>
    public LoggerWriter(string directoryName, Func<string>? logFileNameGanerator = null)
    {
        logFileNameGenerator = logFileNameGanerator!;

        this.directoryName = directoryName;

        lock (LoggerHelper.LoggerWrites)
        {
            LoggerHelper.LoggerWrites.Add(this);
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
    internal void Notify(string hostName, string format, object[] args, LoggerLevel loggerLevel)
    {
        if (loggerLevel < LoggerHelper.MinLoggerLevel)
        {
            return;
        }

        var loggerItem = LoggerHelper.loggerItemPool.TryDequeue(out var existItem) ? existItem : new LoggerItem();

        Thread currentThread = Thread.CurrentThread;

        loggerItem.hostName = hostName;
        loggerItem.format = format;
        loggerItem.args = args;
        loggerItem.loggerLevel = loggerLevel;
        loggerItem.currentDateTime = DateTime.Now;
        loggerItem.threadInfo.threadName = currentThread.Name!;
        loggerItem.threadInfo.threadId = currentThread.ManagedThreadId;

        Enqueue(loggerItem);
    }

    /// <summary>
    /// Generates and retrieves the full path of the log file to be used for logging.
    /// </summary>
    /// <remarks>This method determines the appropriate log file based on the current date, file naming conventions,
    /// and file size constraints. If a custom log file name generator is provided, it will be used to  generate the base
    /// file name. The method ensures that the log file is rotated when the maximum file  size is exceeded or when the date
    /// changes.</remarks>
    /// <returns>The full path of the log file to be used. If no suitable file exists, a new file is created.</returns>
    protected virtual void FileInitialize(out FileInfo fileInfo, DateTime dateTime)
    {
        while (true)
        {
            if (CanUseThisFile(dateTime))
            {
                fileInfo = currentFileInfo!;

                return;
            }

            currentFileSize = 0;

            string fileName = FileNameGenerator();

            if (maxFileIndex <= 0)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(directoryName);

                if (directoryInfo.Exists == false)
                {
                    directoryInfo.Create();
                }

                FileBlock[] items = directoryInfo.EnumerateFiles().Select(i => new FileBlock(i)).ToArray();

                DateTime beginTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day);

                maxFileIndex = (items.Where(x => x.CreateTime == beginTime).OrderByDescending(x => x.Index).FirstOrDefault()?.Index ?? 1);

                maxFileIndex = maxFileIndex < 1 ? 1 : maxFileIndex;
            }

            currentFileInfo = fileInfo = new(Path.Combine(directoryName, $"{fileName}.{maxFileIndex}{LoggerHelper.LoggerFileExtension}"));

            if (currentFileInfo.Exists == false)
            {
                currentFileSize = 0;

                currentFileInfo.Directory?.Create();

                return;
            }

            if (currentFileInfo.Exists)
            {
                currentFileSize = currentFileInfo.Length;
            }

            if (currentFileSize >= LoggerHelper.MaxFileSize) //超过指定的文件大小
            {
                maxFileIndex++;
            }
        }
    }

    #region write

    internal bool Write(ref byte[] writeBuffer, ref char[] charBuffer, DateTime dateTime)
    {
        if (IsEmpty)
        {
            return false;
        }

        FileInitialize(out FileInfo? fileInfo, dateTime);

        int offset = 0;

        if (hasWritenLength > 0)
        {
            TryResizeArray(ref writeBuffer, hasWritenLength);

            Buffer.BlockCopy(writeErrorContent, 0, writeBuffer, 0, hasWritenLength);

            offset += hasWritenLength;

            hasWritenLength = 0;
        }

        while (TryDequeue(out LoggerItem? loggerItem))
        {
            if (loggerItem.IsValid == false)
            {
                continue;
            }

            loggerItem.ToCharArray(ref charBuffer, out var charLength);

            loggerItem.Reset();

            LoggerHelper.loggerItemPool.Enqueue(loggerItem);

            TryResizeArray(ref writeBuffer, offset + charLength * 8 + LoggerHelper.NewLineBytes.Length);

            int byteCount = Encoding.UTF8.GetBytes(charBuffer, 0, charLength, writeBuffer, offset);

            offset += byteCount;

            Buffer.BlockCopy(LoggerHelper.NewLineBytes, 0, writeBuffer, offset, LoggerHelper.NewLineBytes.Length);

            offset += LoggerHelper.NewLineBytes.Length;
        }

        try
        {
            int bufferSize = GetBufferSize(offset);

            using FileStream fileStream = new FileStream(fileInfo.FullName, FileMode.Append, FileAccess.Write, FileShare.Read | FileShare.Delete, bufferSize, FileOptions.SequentialScan);

            fileStream.Write(writeBuffer, 0, offset);

            currentFileSize += offset;
        }
        catch (DirectoryNotFoundException)
        {
            fileInfo.Directory?.Create();

            maxFileIndex = 1;

            currentFileSize = 0;

            TryResizeArray(ref writeErrorContent, offset);

            Buffer.BlockCopy(writeBuffer, 0, writeErrorContent, 0, hasWritenLength = offset);
        }
        catch (Exception)
        {
            TryResizeArray(ref writeErrorContent, offset);

            Buffer.BlockCopy(writeBuffer, 0, writeErrorContent, 0, hasWritenLength = offset);
        }

        return true;

        static void TryResizeArray(ref byte[] writeBuffer, int targetSize)
        {
            if (writeBuffer.Length > targetSize)
            {
                return;
            }

            int currentSize = writeBuffer.Length < 256 ? 256 : writeBuffer.Length;

            while (currentSize < targetSize)
            {
                currentSize *= 2;
            }

            Array.Resize(ref writeBuffer, currentSize);
        }

        static int GetBufferSize(int offset)
        {
            const int DEFAULT_SIZE = 4096;

            int size = DEFAULT_SIZE;

            while (size < offset)
            {
                size = size << 1;
            }

            return size;
        }
    }

    private bool CanUseThisFile(DateTime dateTime)
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

        currentFileInfo.Refresh();

        if (currentFileInfo.Exists == false)
        {
            currentFileSize = 0;

            return true;
        }

        if (currentFileSize >= LoggerHelper.MaxFileSize)
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


    private class FileBlock
    {
        public readonly FileInfo FileInfo;

        public FileBlock(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            string[] Splits = FileInfo.Name.Split('.');

            if (Splits.Length != 3)
            {
                return;
            }

            CreateTime = (DateTime.TryParse(Splits[0], out DateTime dd) ? dd : DateTime.MaxValue);

            CreateTime = new DateTime(CreateTime.Year, CreateTime.Month, CreateTime.Day);

            Index = int.TryParse(Splits[1], out int index) ? index : -1;
        }

        public DateTime CreateTime;

        public int Index;
    }
}

[DebuggerDisplay("{targetFileInfo?.FullName}")]
internal class TargetFileLoggerWriter : LoggerWriter
{
    private readonly FileInfo targetFileInfo;

    public TargetFileLoggerWriter(FileInfo fileInfo)
        : base(null!, null)
    {
        (this.targetFileInfo = fileInfo).Directory?.Create();
    }

    protected override void FileInitialize(out FileInfo fileInfo, DateTime dateTime)
    {
        fileInfo = this.targetFileInfo!;
    }
}
