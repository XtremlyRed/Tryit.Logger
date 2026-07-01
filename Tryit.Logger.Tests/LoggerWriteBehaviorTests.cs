using Xunit;

namespace Tryit.Logger.Tests;

public class LoggerWriteBehaviorTests
{
    [Fact]
    public async Task GetLogger_WithGeneratedFileName_StripsLogExtensionAsync()
    {
        string category = TestContext.CreateCategory();
        ILogger logger = await TestContext.CreateCategoryLoggerAsync("CustomHost", category, () => "custom-name.log");

        logger.Warn("Named file message");

        string directory = Path.Combine(TestContext.LogRoot, category);
        string filePath = await TestContext.WaitForLogFileAsync(directory,
            file => Path.GetFileName(file).Equals("custom-name.1.log", StringComparison.OrdinalIgnoreCase));
        string content = await TestContext.WaitForFileContentAsync(filePath,
            text => text.Contains("CustomHost,Named file message", StringComparison.Ordinal));

        Assert.Contains(", Warn,", content);
        Assert.Contains("CustomHost,Named file message", content);
    }

    [Fact]
    public async Task Logger_WithStringHost_WritesStringValueAsync()
    {
        string category = TestContext.CreateCategory();
        ILogger logger = await TestContext.CreateCategoryLoggerAsync("WorkerService", category);

        logger.Warn("Host should be written as provided");

        string content = await TestContext.WaitForCategoryContentAsync(category,
            text => text.Contains("WorkerService,Host should be written as provided", StringComparison.Ordinal));

        Assert.Contains("WorkerService,Host should be written as provided", content);
    }

    [Fact]
    public async Task Logger_WithObjectHost_WritesRuntimeTypeNameAsync()
    {
        string category = TestContext.CreateCategory();
        ILogger logger = await TestContext.CreateCategoryLoggerAsync(new SampleHost(), category);

        logger.Error("Object host message");

        string content = await TestContext.WaitForCategoryContentAsync(category,
            text => text.Contains("SampleHost,Object host message", StringComparison.Ordinal));

        Assert.Contains(",Error,", content);
        Assert.Contains("SampleHost,Object host message", content);
    }

    [Fact]
    public async Task Logger_WithTypeHost_WritesTypeNameAsync()
    {
        string category = TestContext.CreateCategory();
        ILogger logger = await TestContext.CreateCategoryLoggerAsync(typeof(SampleHost), category);

        logger.Warn("Type host message");

        string content = await TestContext.WaitForCategoryContentAsync(category,
            text => text.Contains("SampleHost,Type host message", StringComparison.Ordinal));

        Assert.Contains("SampleHost,Type host message", content);
    }

    [Fact]
    public async Task GetLogger_WithTargetFile_WritesToSpecifiedFileAsync()
    {
        FileInfo fileInfo = TestContext.CreateTargetFileInfo();
        ILogger logger = LoggerFactory.GetLogger("TargetHost", fileInfo);

        await Task.Delay(TestContext.InitializationDelayMs);
        logger.Fatal("Target file message");

        string content = await TestContext.WaitForFileContentAsync(fileInfo.FullName,
            text => text.Contains("TargetHost,Target file message", StringComparison.Ordinal));

        Assert.Contains(",Fatal,", content);
        Assert.Contains("TargetHost,Target file message", content);
    }

    [Fact]
    public async Task Warn_WritesLogEntryWithExpectedShapeAsync()
    {
        string category = TestContext.CreateCategory();
        ILogger logger = await TestContext.CreateCategoryLoggerAsync("ShapeHost", category);

        logger.Warn("Structured message");

        string content = await TestContext.WaitForCategoryContentAsync(category,
            text => text.Contains("ShapeHost,Structured message", StringComparison.Ordinal));
        string line = TestContext.GetLastNonEmptyLine(content);
        string[] segments = line.Split(',', 5);

        Assert.Equal(5, segments.Length);
        Assert.Matches(@"^\d{2}:\d{2}:\d{2}\.\d{3}$", segments[0]);
        Assert.Equal(" Warn", segments[1]);
        Assert.False(string.IsNullOrWhiteSpace(segments[2]));
        Assert.Equal("ShapeHost", segments[3]);
        Assert.Equal("Structured message", segments[4]);
    }

    [Fact]
    public async Task Log_WhenFormattingFails_WritesFallbackContentAsync()
    {
        string category = TestContext.CreateCategory();
        ILogger logger = await TestContext.CreateCategoryLoggerAsync("FormatHost", category);

        logger.Log(LoggerLevel.Error, "Broken {0} {1}", 1);

        string content = await TestContext.WaitForCategoryContentAsync(category,
            text => text.Contains("Broken {0} {1}1", StringComparison.Ordinal));

        Assert.Contains("Broken {0} {1}1", content);
        Assert.Contains("FormatException", content);
    }

    [Fact]
    public async Task Warn_WithFormattingParameters_WritesFormattedMessageAsync()
    {
        string category = TestContext.CreateCategory();
        ILogger logger = await TestContext.CreateCategoryLoggerAsync("FormatHost", category);

        logger.Warn("User {0} processed item {1}", "Alice", 42);

        string content = await TestContext.WaitForCategoryContentAsync(category,
            text => text.Contains("FormatHost,User Alice processed item 42", StringComparison.Ordinal));

        Assert.Contains("FormatHost,User Alice processed item 42", content);
    }

    [Fact]
    public async Task Trace_BelowConfiguredMinLevel_DoesNotCreateALogFileAsync()
    {
        string category = TestContext.CreateCategory();
        ILogger logger = await TestContext.CreateCategoryLoggerAsync("LevelHost", category);

        logger.Trace("Trace should be ignored");

        await TestContext.AssertNoLogFileCreatedAsync(category);
    }

    [Fact]
    public async Task Debug_BelowConfiguredMinLevel_DoesNotCreateALogFileAsync()
    {
        string category = TestContext.CreateCategory();
        ILogger logger = await TestContext.CreateCategoryLoggerAsync("LevelHost", category);

        logger.Debug("Debug should be ignored");

        await TestContext.AssertNoLogFileCreatedAsync(category);
    }

    [Fact]
    public async Task Info_BelowConfiguredMinLevel_DoesNotCreateALogFileAsync()
    {
        string category = TestContext.CreateCategory();
        ILogger logger = await TestContext.CreateCategoryLoggerAsync("LevelHost", category);

        logger.Info("Info should be ignored");

        await TestContext.AssertNoLogFileCreatedAsync(category);
    }

    [Fact]
    public async Task Warn_AtConfiguredMinLevel_WritesLogFileAsync()
    {
        string category = TestContext.CreateCategory();
        ILogger logger = await TestContext.CreateCategoryLoggerAsync("LevelHost", category);

        logger.Warn("Warn should be written");

        string content = await TestContext.WaitForCategoryContentAsync(category,
            text => text.Contains("LevelHost,Warn should be written", StringComparison.Ordinal));

        Assert.Contains("LevelHost,Warn should be written", content);
    }

    [Fact]
    public async Task Error_AboveConfiguredMinLevel_WritesLogFileAsync()
    {
        string category = TestContext.CreateCategory();
        ILogger logger = await TestContext.CreateCategoryLoggerAsync("LevelHost", category);

        logger.Error("Error should be written");

        string content = await TestContext.WaitForCategoryContentAsync(category,
            text => text.Contains("LevelHost,Error should be written", StringComparison.Ordinal));

        Assert.Contains("LevelHost,Error should be written", content);
    }

    [Fact]
    public async Task Fatal_AboveConfiguredMinLevel_WritesLogFileAsync()
    {
        string category = TestContext.CreateCategory();
        ILogger logger = await TestContext.CreateCategoryLoggerAsync("LevelHost", category);

        logger.Fatal("Fatal should be written");

        string content = await TestContext.WaitForCategoryContentAsync(category,
            text => text.Contains("LevelHost,Fatal should be written", StringComparison.Ordinal));

        Assert.Contains("LevelHost,Fatal should be written", content);
    }

    [Fact]
    public async Task Log_WithWarnLevel_WritesLogFileAsync()
    {
        string category = TestContext.CreateCategory();
        ILogger logger = await TestContext.CreateCategoryLoggerAsync("LevelHost", category);

        logger.Log(LoggerLevel.Warn, "Direct warn entry");

        string content = await TestContext.WaitForCategoryContentAsync(category,
            text => text.Contains("LevelHost,Direct warn entry", StringComparison.Ordinal));

        Assert.Contains(", Warn,", content);
        Assert.Contains("LevelHost,Direct warn entry", content);
    }

    [Fact]
    public async Task Log_WithWhitespaceFormat_DoesNotCreateALogFileAsync()
    {
        string category = TestContext.CreateCategory();
        ILogger logger = await TestContext.CreateCategoryLoggerAsync("BlankHost", category);

        logger.Log(LoggerLevel.Warn, "   ");

        await TestContext.AssertNoLogFileCreatedAsync(category);
    }
}
