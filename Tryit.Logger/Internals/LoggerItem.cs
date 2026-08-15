namespace Tryit.Logger.Internals;

internal class LoggerItem
{
    private static readonly StringBuilder stringBuilder = new StringBuilder();

    private static readonly string[] Strings2 = new string[100];
    private static readonly string[] Strings3 = new string[1000];

    static LoggerItem()
    {
        for (int i = 0; i < 100; i++)
        {
            Strings2[i] = $"{i:D2}";
        }

        for (int i = 0; i < 1000; i++)
        {
            Strings3[i] = $"{i:D3}";
        }
    }

    internal string hostName = default!;
    internal string format = default!;
    internal object[] args = default!;
    internal LoggerLevel loggerLevel = default!;
    internal DateTime currentDateTime = default!;
    internal readonly ThreadInfo threadInfo = new ThreadInfo();

    internal void Reset()
    {
        hostName = null!;
        format = null!;
        args = null!;
        loggerLevel = LoggerLevel.Info;
        currentDateTime = DateTime.MinValue;
        threadInfo.threadId = 0;
        threadInfo.threadName = null!;
    }

    public bool IsValid => !string.IsNullOrWhiteSpace(hostName) && !string.IsNullOrWhiteSpace(format);

    public class ThreadInfo
    {
        internal string threadName = default!;
        internal int threadId = default!;
    }

    public void ToCharArray(ref char[] charArray, out int charLength)
    {
        stringBuilder.Clear();

        stringBuilder.Append(Strings2[currentDateTime.Hour]).Append(':');

        stringBuilder.Append(Strings2[currentDateTime.Minute]).Append(':');

        stringBuilder.Append(Strings2[currentDateTime.Second]).Append('.');

        stringBuilder.Append(Strings3[currentDateTime.Millisecond]).Append(',');

        stringBuilder.Append(LoggerHelper.LoggerLevelStrings[(int)loggerLevel]).Append(',');

        if (string.IsNullOrWhiteSpace(threadInfo.threadName))
        {
            stringBuilder.Append(threadInfo.threadId < 100 ? Strings2[threadInfo.threadId] : threadInfo.threadId.ToString()).Append(',');
        }
        else
        {
            stringBuilder.Append(threadInfo.threadName).Append(',');
        }

        stringBuilder.Append(hostName).Append(',');

        if (args != null && args.Length > 0)
        {
            try
            {
                stringBuilder.AppendFormat(format, args);
            }
            catch
            {
                stringBuilder.Append("[ FormatError ] ").Append(format);

                for (int i = 0, length = args?.Length ?? 0; i < length; i++)
                {
                    stringBuilder.Append(' ').Append(args![i]);
                }
            }
        }
        else
        {
            stringBuilder.Append(format);
        }

        if (stringBuilder.Length > charArray.Length)
        {
            Array.Resize(ref charArray, stringBuilder.Length);
        }

        stringBuilder.CopyTo(0, charArray, 0, charLength = stringBuilder.Length);
    }
}
