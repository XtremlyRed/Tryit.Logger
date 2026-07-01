using System.Collections.Concurrent;
using Tryit.Configure;

namespace Tryit.Logger.Internals;

internal static class LoggerHelper
{
    internal const string LoggerFileExtension = ".log";

    internal const string nodeName = "paths";

    internal static readonly ConcurrentDictionary<string, Logger> Loggers = new();

    internal static readonly int NewLineCount = Encoding.UTF8.GetByteCount(Environment.NewLine);

    internal static readonly List<Logger> Writers = new();

    internal static string defaultConfigPath = "configs";

    internal static string defaultLogPath = Path.Combine("..", "logs");

    internal static readonly IDictionary<LoggerLevel, string> LoggerLevelMaps = Enum.GetValues(typeof(LoggerLevel)).OfType<LoggerLevel>().ToDictionary(i => i, i => i.ToString().PadLeft(5, ' '));

    internal static LoggerConfigure loggerConfigure = default!;

    private static bool IsInitialized;

    public static void TryInitialize()
    {
        if (IsInitialized)
        {
            return;
        }

        lock (Loggers)
        {
            if (IsInitialized)
            {
                return;
            }

            IsInitialized = true;

            loggerConfigure = new LoggerConfigure(defaultConfigPath);

            ThreadPool.QueueUserWorkItem(static async (u) => await LoopWriteAsync().ConfigureAwait(false));
        }
    }

    private static async Task LoopWriteAsync()
    {
        for (; ; )
        {
            await loggerConfigure.WaitAsync().ConfigureAwait(false);

            bool hasContent = false;

            for (int i = Writers.Count - 1; i >= 0; i--)
            {
                if (Writers[i].NeedWrite)
                {
                    hasContent = true;
                    break;
                }
            }

            if (hasContent == false)
            {
                loggerConfigure.WaitIntervalCheck(hasContent);

                continue;
            }

            DateTime dateTime = DateTime.Now;

            DateTime retentionTime = loggerConfigure.GetRetentionTime(ref dateTime);

            hasContent = false;

            for (int i = Writers.Count - 1; i >= 0; i--)
            {
                try
                {
                    loggerConfigure.stringBuilder.Clear();

                    hasContent |= Writers[i].Write(loggerConfigure.stringBuilder, ref dateTime, ref retentionTime);
                }
                catch
                {
                    //ignore
                }
            }

            loggerConfigure.WaitIntervalCheck(hasContent);
        }
    }

    private static double CoerceAtLeast(this double value, double minValue)
    {
        return value < minValue ? minValue : value;
    }

    internal static DateTime GetDayBegin(this DateTime dateTime)
    {
        return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day);
    }

    internal class LoggerConfigure
    {
        internal LoggerConfigure(string defaultPath = "configs")
        {
            FileInfo fileInfo = new FileInfo(Path.Combine(defaultPath, "logger.json"));

            if (fileInfo.Directory is not null && fileInfo.Directory.Exists == false)
            {
                fileInfo.Directory?.Create();
            }

            LoggerSettings = ConfigurationFactory.GetConfiguration(fileInfo.FullName);

            MaxFileSize = (int)(LoggerSettings.Read<double>("file_size", 2d).CoerceAtLeast(0.1) * 1024 * 1024);

            scan_cycle = (int)(LoggerSettings.Read("scan_cycle", 0.1d).CoerceAtLeast(0.1) * 1000);

            record_days = LoggerSettings.Read<double>("record_days", -1d);

            MinLoggerLevel = LoggerSettings.Read("min_level", LoggerLevel.Info);

            max_scan_cycle = (int)(LoggerSettings.Read("max_scan_cycle", 3d).CoerceAtLeast(0.5) * 1000);

            maxCounter = Convert.ToUInt64(10_000 / scan_cycle);
        }

        internal readonly IConfiguration LoggerSettings;

        internal readonly int MaxFileSize;

        internal readonly LoggerLevel MinLoggerLevel;

        internal readonly StringBuilder stringBuilder = new StringBuilder();

        internal ulong counter = 0;

        private readonly ulong maxCounter;

        private readonly int scan_cycle;

        private readonly int max_scan_cycle;

        private readonly double record_days;

        internal DateTime GetRetentionTime(ref DateTime dateTime)
        {
            DateTime retentionTime = (record_days < 1 ? DateTime.MinValue : dateTime.AddDays(-record_days.CoerceAtLeast(1))).GetDayBegin();
            return retentionTime;
        }

        internal void WaitIntervalCheck(bool hasWriten)
        {
            if (hasWriten)
            {
                counter = 0;

                return;
            }

            counter++;
        }

        internal Task WaitAsync()
        {
            if (counter > maxCounter)
            {
                return Task.Delay(max_scan_cycle);
            }

            return Task.Delay(scan_cycle);
        }
    }
}
