using System.Collections.Concurrent;
using Tryit.Configure;

namespace Tryit.Logger.Internals;

internal static class LoggerHelper
{
    private static byte[] writeBuffer = new byte[8 * 1024];
    private static char[] charBuffer = new char[4 * 1024];

    internal const string LoggerFileExtension = ".log";

    internal const string nodeName = "paths";

    internal static readonly ConcurrentQueue<LoggerItem> loggerItemPool = new();

    internal static readonly IConfiguration LoggerSettings = ConfigurationFactory.GetConfiguration(Path.Combine("configs", "logger.json"));

    internal static readonly Dictionary<string, LoggerWriter> LoggerWriterMaps = new();

    internal static readonly Dictionary<string, InnerLog> LoggerMaps = new();

    internal static readonly byte[] NewLineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);

    internal static readonly List<LoggerWriter> LoggerWrites = new();

    internal static readonly string[] LoggerLevelStrings = Enum.GetValues(typeof(LoggerLevel)).OfType<LoggerLevel>().Select(i => i.ToString().PadLeft(5, ' ')).ToArray();

    internal static readonly int MaxFileSize = (int)(LoggerSettings.Read<double>("file_size", 2d).CoerceAtLeast(0.1) * 1024 * 1024);

    internal static readonly int record_delay_ms = (int)(LoggerSettings.Read("record_delay", 0.1d).CoerceAtLeast(0.1) * 1000);

    internal static readonly LoggerLevel MinLoggerLevel = LoggerSettings.Read("min_level", LoggerLevel.Info);

    static LoggerHelper()
    {
        ThreadPool.QueueUserWorkItem(static async (u) => await LoopWriteAsync());
    }

    private static async Task LoopWriteAsync()
    {
        for (int counter = 0, waitInterval = record_delay_ms; ; )
        {
            await Task.Delay(waitInterval).ConfigureAwait(false);

            DateTime dateTime = DateTime.Now;

            bool hasWriten = false;

            for (int i = LoggerWrites.Count - 1; i >= 0; i--)
            {
                try
                {
                    hasWriten |= LoggerWrites[i].Write(ref writeBuffer, ref charBuffer, dateTime);
                }
                catch
                {
                    //ignore
                }
            }

            if (hasWriten)
            {
                waitInterval = record_delay_ms;

                counter = 0;

                continue;
            }

            //如果10秒内没有写入日志，则将写入间隔设置为3秒，降低CPU占用
            if ((counter += record_delay_ms) > 10_000)
            {
                waitInterval = 3_000;
                counter = 0;
            }
        }
    }
}
