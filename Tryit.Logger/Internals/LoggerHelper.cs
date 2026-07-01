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

    internal static bool IsInitialized;

    static LoggerHelper()
    {
        ThreadPool.QueueUserWorkItem(async (o) => await LoggerHelperInitAsync());

        static async Task LoggerHelperInitAsync()
        {
            while (IsInitialized == false)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }

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
                hasContent |= Writers[i].IsEmpty == false;
            }

            if (hasContent == false)
            {
                continue;
            }

            DateTime dateTime = DateTime.Now;

            DateTime retentionTime = loggerConfigure.GetRetentionTime(ref dateTime);

            bool hasWriten = false;

            for (int i = Writers.Count - 1; i >= 0; i--)
            {
                try
                {
                    loggerConfigure.stringBuilder.Clear();

                    hasWriten |= Writers[i].Write(loggerConfigure.stringBuilder, ref dateTime, ref retentionTime);
                }
                catch
                {
                    //ignore
                }
            }

            loggerConfigure.WaitIntervalCheck(hasWriten);
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
            LoggerSettings = ConfigurationFactory.GetConfiguration(Path.Combine(defaultPath, "logger.json"));

            MaxFileSize = (int)(LoggerSettings.Read<double>("file_size", 2d).CoerceAtLeast(0.1) * 1024 * 1024);

            record_delay_ms = waitInterval = (int)(LoggerSettings.Read("record_delay", 0.1d).CoerceAtLeast(0.1) * 1000);

            record_days = LoggerSettings.Read<double>("record_days", -1d);

            MinLoggerLevel = LoggerSettings.Read("min_level", LoggerLevel.Info);

            maxCounter = 10_000 / record_delay_ms;
        }

        internal readonly IConfiguration LoggerSettings;

        internal readonly int MaxFileSize;

        internal readonly LoggerLevel MinLoggerLevel;

        internal readonly StringBuilder stringBuilder = new StringBuilder();

        internal int counter = 0;

        private int waitInterval;

        private readonly int maxCounter;

        private readonly int record_delay_ms;

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
                waitInterval = record_delay_ms;
                counter = 0;
                return;
            }

            if (++counter > maxCounter)
            {
                waitInterval = 3000;
            }
        }

        internal Task WaitAsync()
        {
            return Task.Delay(waitInterval);
        }
    }
}
