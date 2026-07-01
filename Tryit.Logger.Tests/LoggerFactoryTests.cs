using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Sdk;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Tryit.Logger.Tests;

public class LoggerFactoryTests
{
    [Fact]
    public void GetLogger_WithNullHostAndCategory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => LoggerFactory.GetLogger<object>(null!, TestContext.CreateCategory()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void GetLogger_WithInvalidCategory_ThrowsArgumentException(string? category)
    {
        Assert.Throws<ArgumentException>(() => LoggerFactory.GetLogger(new object(), category!));
        Assert.Throws<ArgumentException>(() => LoggerFactory.GetLogger(new object(), category!, null));
    }

    [Fact]
    public async Task GetLogger_WithSameCategory_ReturnsCachedInstance()
    {
        string category = TestContext.CreateCategory();

        ILogger first = await TestContext.CreateCategoryLoggerAsync(new object(), category);
        ILogger second = await TestContext.CreateCategoryLoggerAsync("AlternateHost", category);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetLogger_WithSameCategoryAndDifferentFileNameGenerators_ReturnsCachedInstance()
    {
        string category = TestContext.CreateCategory();

        ILogger first = await TestContext.CreateCategoryLoggerAsync("FirstHost", category, () => "first-name.log");
        ILogger second = await TestContext.CreateCategoryLoggerAsync("SecondHost", category, () => "second-name.log");

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
        FileInfo fileInfo = TestContext.CreateTargetFileInfo();

        ILogger first = LoggerFactory.GetLogger(new object(), fileInfo);
        ILogger second = LoggerFactory.GetLogger("FileHost", fileInfo);

        Assert.Same(first, second);
    }
}

internal static class TestContext
{
    internal const int InitializationDelayMs = 400;
    private const int PollIntervalMs = 100;
    private const int WaitTimeoutSeconds = 8;

    internal static readonly string TestRunRoot = Path.Combine(AppContext.BaseDirectory, "test-artifacts", Guid.NewGuid().ToString("N"));
    internal static readonly string ConfigRoot = Path.Combine(TestRunRoot, "configs");
    internal static readonly string LogRoot = Path.Combine(TestRunRoot, "logs");

    static TestContext()
    {
        Directory.CreateDirectory(ConfigRoot);
        Directory.CreateDirectory(LogRoot);

        File.WriteAllText(
            Path.Combine(ConfigRoot, "logger.json"),
            """
            {
              "file_size": 0.1,
              "record_delay": 0.1,
              "record_days": -1,
              "min_level": 8
            }
            """
        );

        LoggerFactory.LogPath = LogRoot;

        LoggerFactory.LogConfigPath = ConfigRoot;
    }

    internal static async Task<ILogger> CreateCategoryLoggerAsync(object host, string category, Func<string>? fileNameGenerator = null)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(WaitTimeoutSeconds);

        while (true)
        {
            try
            {
                ILogger logger = LoggerFactory.GetLogger(host, category, fileNameGenerator);
                await Task.Delay(InitializationDelayMs);
                return logger;
            }
            catch (NullReferenceException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(PollIntervalMs);
            }
        }
    }

    internal static FileInfo CreateTargetFileInfo()
    {
        string directory = Path.Combine(TestRunRoot, "target-files");
        Directory.CreateDirectory(directory);
        return new FileInfo(Path.Combine(directory, $"{Guid.NewGuid():N}.log"));
    }

    internal static string CreateCategory([CallerMemberName] string? memberName = null)
    {
        return $"{memberName}-{Guid.NewGuid():N}";
    }

    internal static async Task<string> WaitForCategoryContentAsync(string category, Func<string, bool> predicate)
    {
        return await WaitForDirectoryContentAsync(Path.Combine(LogRoot, category), predicate);
    }

    internal static async Task<string> WaitForDirectoryContentAsync(string directory, Func<string, bool> predicate)
    {
        string filePath = await WaitForLogFileAsync(directory, static _ => true);
        return await WaitForFileContentAsync(filePath, predicate);
    }

    internal static async Task<string> WaitForLogFileAsync(string directory, Func<string, bool> predicate)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(WaitTimeoutSeconds);

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

            await Task.Delay(PollIntervalMs);
        }

        string files = Directory.Exists(directory) ? string.Join(", ", Directory.EnumerateFiles(directory)) : "none";
        throw new XunitException($"A log file was not created in directory '{directory}' before the timeout. Existing files: {files}.");
    }

    internal static async Task<string> WaitForFileContentAsync(string filePath, Func<string, bool> predicate)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(WaitTimeoutSeconds);

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

            await Task.Delay(PollIntervalMs);
        }

        throw new XunitException($"File '{filePath}' did not contain the expected content before the timeout.");
    }

    internal static async Task AssertNoLogFileCreatedAsync(string category)
    {
        string directory = Path.Combine(LogRoot, category);
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);

        while (DateTime.UtcNow < deadline)
        {
            if (Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.log", SearchOption.TopDirectoryOnly).Any())
            {
                throw new XunitException($"No log file was expected in directory '{directory}', but at least one file was created.");
            }

            await Task.Delay(PollIntervalMs);
        }
    }

    internal static string GetLastNonEmptyLine(string content)
    {
        string? line = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

        return line ?? throw new XunitException("The log content did not contain any non-empty lines.");
    }
}

internal sealed class SampleHost;
