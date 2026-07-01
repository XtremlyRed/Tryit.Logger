using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Sdk;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Tryit.Logger.Tests;

public class UnitTest1
{
    private static readonly string TestRunRoot = Path.Combine(AppContext.BaseDirectory, "test-artifacts", Guid.NewGuid().ToString("N"));
    private static readonly string ConfigRoot = Path.Combine(TestRunRoot, "configs");
    private static readonly string LogRoot = Path.Combine(TestRunRoot, "logs");

    static UnitTest1()
    {
        Directory.CreateDirectory(ConfigRoot);
        Directory.CreateDirectory(LogRoot);

        File.WriteAllText(Path.Combine(ConfigRoot, "logger.json"),
            """
            {
              "file_size": 0.1,
              "record_delay": 0.1,
              "record_days": -1,
              "min_level": 8
            }
            """);

        LoggerFactory.UpdatePath(LogRoot, ConfigRoot);
    }

    [Fact]
    public void GetLogger_WithNullHost_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => LoggerFactory.GetLogger<object>(null!, CreateCategory()));
    }

    [Fact]
    public void GetLogger_WithInvalidCategory_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => LoggerFactory.GetLogger(new object(), " "));
        Assert.Throws<ArgumentException>(() => LoggerFactory.GetLogger(new object(), string.Empty, null));
    }

    [Fact]
    public void GetLogger_WithSameCategory_ReturnsCachedInstance()
    {
        string category = CreateCategory();

        ILogger first = LoggerFactory.GetLogger(new object(), category);
        ILogger second = LoggerFactory.GetLogger("another-host", category);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetLogger_WithNullFileInfo_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => LoggerFactory.GetLogger(new object(), fileInfo: null!));
    }

    [Fact]
    public void GetLogger_WithSameTargetFile_ReturnsCachedInstance()
    {
        FileInfo fileInfo = CreateTargetFileInfo();

        ILogger first = LoggerFactory.GetLogger(new object(), fileInfo);
        ILogger second = LoggerFactory.GetLogger("file-host", fileInfo);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetLogger_WithGeneratedFileName_WritesToGeneratedLogFileAsync()
    {
        string category = CreateCategory();
        ILogger logger = await CreateCategoryLoggerAsync("CustomHost", category, () => "custom-name.log");

        logger.Warn("Named file message");

        string directory = Path.Combine(LogRoot, category);
        string filePath = await WaitForLogFileAsync(directory, file => Path.GetFileName(file).Equals("custom-name.1.log", StringComparison.OrdinalIgnoreCase));
        string content = await WaitForFileContentAsync(filePath, text => text.Contains("CustomHost,Named file message", StringComparison.Ordinal));

        Assert.Contains(", Warn,", content);
        Assert.Contains("CustomHost,Named file message", content);
    }

    [Fact]
    public async Task Logger_WithObjectHost_WritesTypeNameToLogAsync()
    {
        string category = CreateCategory();
        ILogger logger = await CreateCategoryLoggerAsync(new SampleHost(), category);

        logger.Error("Object host message");

        string content = await WaitForCategoryContentAsync(category, text => text.Contains("SampleHost,Object host message", StringComparison.Ordinal));

        Assert.Contains(",Error,", content);
        Assert.Contains("SampleHost,Object host message", content);
    }

    [Fact]
    public async Task Info_BelowConfiguredMinLevel_IsIgnoredAsync()
    {
        string category = CreateCategory();
        ILogger logger = await CreateCategoryLoggerAsync("LevelHost", category);

        logger.Info("Ignored info message");
        logger.Warn("Accepted warn message");

        string content = await WaitForCategoryContentAsync(category, text => text.Contains("LevelHost,Accepted warn message", StringComparison.Ordinal));

        Assert.Contains("Accepted warn message", content);
        Assert.DoesNotContain("Ignored info message", content);
    }

    [Fact]
    public async Task Log_WhenFormattingFails_WritesFallbackContentAsync()
    {
        string category = CreateCategory();
        ILogger logger = await CreateCategoryLoggerAsync("FormatHost", category);

        logger.Log(LoggerLevel.Error, "Broken {0} {1}", 1);

        string content = await WaitForCategoryContentAsync(category, text => text.Contains("Broken {0} {1}1", StringComparison.Ordinal));

        Assert.Contains("Broken {0} {1}1", content);
        Assert.Contains("FormatException", content);
    }

    [Fact]
    public async Task GetLogger_WithTargetFile_WritesToSpecifiedFileAsync()
    {
        FileInfo fileInfo = CreateTargetFileInfo();
        ILogger logger = LoggerFactory.GetLogger("TargetHost", fileInfo);

        await Task.Delay(400);
        logger.Fatal("Target file message");

        string content = await WaitForFileContentAsync(fileInfo.FullName, text => text.Contains("TargetHost,Target file message", StringComparison.Ordinal));

        Assert.Contains(",Fatal,", content);
        Assert.Contains("TargetHost,Target file message", content);
    }

    private static async Task<ILogger> CreateCategoryLoggerAsync(object host, string category, Func<string>? fileNameGenerator = null)
    {
        ILogger logger = LoggerFactory.GetLogger(host, category, fileNameGenerator);
        await Task.Delay(400);
        return logger;
    }

    private static FileInfo CreateTargetFileInfo()
    {
        string directory = Path.Combine(TestRunRoot, "target-files");
        Directory.CreateDirectory(directory);
        return new FileInfo(Path.Combine(directory, $"{Guid.NewGuid():N}.log"));
    }

    private static string CreateCategory([CallerMemberName] string? memberName = null)
    {
        return $"{memberName}-{Guid.NewGuid():N}";
    }

    private static async Task<string> WaitForCategoryContentAsync(string category, Func<string, bool> predicate)
    {
        return await WaitForDirectoryContentAsync(Path.Combine(LogRoot, category), predicate);
    }

    private static async Task<string> WaitForDirectoryContentAsync(string directory, Func<string, bool> predicate)
    {
        string filePath = await WaitForLogFileAsync(directory, static _ => true);
        return await WaitForFileContentAsync(filePath, predicate);
    }

    private static async Task<string> WaitForLogFileAsync(string directory, Func<string, bool> predicate)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(8);

        while (DateTime.UtcNow < deadline)
        {
            if (Directory.Exists(directory))
            {
                string? filePath = Directory.EnumerateFiles(directory, "*.log", SearchOption.TopDirectoryOnly).FirstOrDefault(predicate);

                if (filePath is not null)
                {
                    return filePath;
                }
            }

            await Task.Delay(100);
        }

        throw new XunitException($"在目录 '{directory}' 中未等到日志文件。\n已有文件: {string.Join(", ", Directory.Exists(directory) ? Directory.EnumerateFiles(directory) : [])}");
    }

    private static async Task<string> WaitForFileContentAsync(string filePath, Func<string, bool> predicate)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(8);

        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(filePath))
            {
                string content = File.ReadAllText(filePath);

                if (predicate(content))
                {
                    return content;
                }
            }

            await Task.Delay(100);
        }

        throw new XunitException($"文件 '{filePath}' 在等待时间内未出现预期内容。");
    }

    private sealed class SampleHost;
}
