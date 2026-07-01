# Tryit.Logger

Tryit.Logger is a lightweight file-based logging library for .NET applications. It focuses on a small public API, simple category-based logger creation, buffered asynchronous file writing, configurable log filtering, and automatic file rotation.

The library is designed for applications that want direct file logging without adopting a larger logging framework. It supports modern .NET runtimes, .NET Standard, and older .NET Framework targets.

## Table of Contents

- [Overview](#overview)
- [Key Features](#key-features)
- [Supported Frameworks](#supported-frameworks)
- [Installation](#installation)
- [Package Dependency](#package-dependency)
- [Quick Start](#quick-start)
- [Logger Creation](#logger-creation)
- [Host Name Resolution](#host-name-resolution)
- [Path Configuration](#path-configuration)
- [Configuration File](#configuration-file)
- [Log Levels](#log-levels)
- [File Layout and Naming](#file-layout-and-naming)
- [Log Entry Format](#log-entry-format)
- [Write Pipeline](#write-pipeline)
- [Formatting Rules](#formatting-rules)
- [Caching Behavior](#caching-behavior)
- [Target File Logging](#target-file-logging)
- [Public API Reference](#public-api-reference)
- [Usage Patterns](#usage-patterns)
- [Operational Notes](#operational-notes)
- [Known Behaviors and Limitations](#known-behaviors-and-limitations)
- [Testing](#testing)
- [Repository](#repository)
- [License](#license)

## Overview

Tryit.Logger exposes a single main abstraction, `ILogger`, and a single entry point, `LoggerFactory`.

With it, you can:

- Create a logger for a category such as `app`, `jobs`, or `orders`
- Route category logs into dedicated directories
- Write messages with six severity levels
- Send logs directly to a specific file when needed
- Control minimum log level and file size through a JSON configuration file
- Reuse the same logger instance for the same category or target file

The library writes logs asynchronously through a background loop. Calls to `Trace`, `Debug`, `Info`, `Warn`, `Error`, `Fatal`, and `Log` enqueue formatted messages first, and file output happens shortly after.

## Key Features

- Small API surface
- Category-based logger retrieval
- Optional direct file logger creation
- Buffered asynchronous writes
- Automatic target directory creation
- File rotation based on maximum file size
- Default date-based file naming
- Optional custom file name generation
- Configurable minimum log level
- Broad target framework coverage

## Supported Frameworks

The package targets the following frameworks:

- .NET 6
- .NET 8
- .NET 10
- .NET Framework 4.8
- .NET Framework 4.5.1
- .NET Standard 2.0
- .NET Standard 2.1

## Installation

Install from NuGet with the .NET CLI:

```powershell
dotnet add package Tryit.Logger
```

Install from the Package Manager Console:

```powershell
Install-Package Tryit.Logger
```

Add the namespace in code:

```csharp
using Tryit.Logger;
```

## Package Dependency

This package depends on:

- `Tryit.Configure`

That dependency is used to read and write logger settings from `logger.json`.

## Quick Start

### Category-based logging

```csharp
using Tryit.Logger;

var logger = this.GetLogger("app");

logger.Info("Application started at {0}", DateTime.Now);
logger.Warn("Cache warmup is still in progress");
logger.Error("Request failed for user {0}", userId);
```

### Static factory usage

```csharp
using Tryit.Logger;

ILogger logger = LoggerFactory.GetLogger("WorkerService", "jobs");
logger.Info("Job runner initialized");
```

### Custom file naming

```csharp
using Tryit.Logger;

ILogger logger = LoggerFactory.GetLogger(
    "ApiHost",
    "requests",
    () => "api-requests.log");

logger.Warn("Slow request detected");
```

### Direct logging to a specific file

```csharp
using Tryit.Logger;

ILogger logger = LoggerFactory.GetLogger(
    "ImportService",
    new FileInfo(Path.Combine("logs", "imports", "manual-import.log")));

logger.Fatal("Import terminated unexpectedly");
```

## Logger Creation

The factory supports three creation patterns.

### 1. Category logger

```csharp
ILogger logger = LoggerFactory.GetLogger(host, "orders");
```

This creates or returns a cached logger associated with the category name.

### 2. Category logger with custom file name generator

```csharp
ILogger logger = LoggerFactory.GetLogger(host, "orders", () => "order-stream.log");
```

This behaves like the category logger above, but the file name stem comes from the provided delegate.

### 3. Direct file logger

```csharp
ILogger logger = LoggerFactory.GetLogger(host, new FileInfo("logs/special/manual.log"));
```

This writes to the given file path instead of using category directory naming.

## Host Name Resolution

The value passed as `host` is converted into the host segment written to each log entry.

Resolution rules are:

- If `host` is a `string`, the string value is written as-is
- If `host` is a `Type`, the type name is written
- Otherwise, the runtime type name of the object is written

Examples:

```csharp
ILogger logger1 = LoggerFactory.GetLogger("BillingService", "billing");
ILogger logger2 = LoggerFactory.GetLogger(typeof(OrderProcessor), "orders");
ILogger logger3 = LoggerFactory.GetLogger(new OrderProcessor(), "orders");
```

Resulting host names:

- `BillingService`
- `OrderProcessor`
- `OrderProcessor`

## Path Configuration

The library uses two configurable root paths.

Default values:

- Log path: `..\logs`
- Configuration path: `configs`

You can override both paths before creating loggers:

```csharp
LoggerFactory.UpdatePath(
    loggerPath: Path.Combine(AppContext.BaseDirectory, "logs"),
    loggerConfigurePath: Path.Combine(AppContext.BaseDirectory, "configs"));
```

### Recommendation

Call `LoggerFactory.UpdatePath(...)` once during application startup and before the first category-based logger is created.

## Configuration File

The logger configuration is read from:

```text
{configuration-path}/logger.json
```

Example:

```json
{
  "file_size": 2,
  "record_delay": 0.1,
  "record_days": -1,
  "min_level": 4
}
```

### Configuration Keys

| Key | Type | Meaning |
| --- | --- | --- |
| `file_size` | number | Maximum size of one log file in megabytes before rolling to the next file index |
| `record_delay` | number | Delay in seconds between background flush attempts when activity is present |
| `record_days` | number | Retention threshold input used by the internal retention-time calculation |
| `min_level` | enum or integer | Minimum `LoggerLevel` that will be written |

### Effective minimum values

The implementation applies a lower bound to some settings:

- `file_size` is coerced to at least `0.1`
- `record_delay` is coerced to at least `0.1`

That means values smaller than `0.1` are treated as `0.1`.

## Log Levels

`LoggerLevel` uses these values:

| Level | Numeric Value |
| --- | ---: |
| `Trace` | 1 |
| `Debug` | 2 |
| `Info` | 4 |
| `Warn` | 8 |
| `Error` | 16 |
| `Fatal` | 32 |

### Minimum level examples

Write `Info` and above:

```json
{
  "min_level": 4
}
```

Write `Warn` and above:

```json
{
  "min_level": 8
}
```

Write only `Error` and `Fatal`:

```json
{
  "min_level": 16
}
```

Any message with a lower value than `min_level` is ignored and never queued for file output.

## File Layout and Naming

### Category-based logger layout

Category-based loggers write into a directory under the configured log root:

```text
{log-root}/{category}/
```

Example:

```text
logs/orders/
```

### Default file naming

If no custom name generator is provided, file names are based on the current date:

```text
yyyy-MM-dd.{index}.log
```

Examples:

- `2025-01-15.1.log`
- `2025-01-15.2.log`

The index increases when the active file reaches the configured size limit.

### Custom file naming

If a custom file name generator is provided, the logger uses the generated name stem and appends the rolling index:

```text
{generated-name}.{index}.log
```

If the generated value already ends with `.log`, the extension is removed before the final rolling name is built.

Example input:

```csharp
() => "api-requests.log"
```

Possible output file:

```text
api-requests.1.log
```

### Direct file logger layout

When logging directly to a `FileInfo`, the logger writes to that exact file path instead of generating a category directory or a rolling date-based file name.

## Log Entry Format

Each message is written as one line with this structure:

```text
HH:mm:ss.fff,LEVEL,THREAD,HOST,MESSAGE
```

Example:

```text
09:41:12.135, Warn,12,WorkerService,Slow request detected
```

### Segment details

| Segment | Description |
| --- | --- |
| `HH:mm:ss.fff` | Local time when the message was enqueued |
| `LEVEL` | Padded level name such as ` Warn` or `Error` |
| `THREAD` | Current thread name if available, otherwise the managed thread ID |
| `HOST` | Resolved host name |
| `MESSAGE` | Final formatted message content |

## Write Pipeline

The write flow is:

1. A logging call is made
2. The message is filtered by `min_level`
3. The message is formatted into a single line
4. The line is enqueued in memory
5. A background writer flushes queued content to disk after a delay
6. The current file is reused until it reaches the configured size limit or the date changes

### Important behavior

- Logging calls do not write synchronously to disk
- File creation may happen shortly after the log method returns
- The writer creates directories if they do not exist
- Write failures are internally captured and the content is retried later

## Formatting Rules

All public log methods accept a composite format string and `params object[]` arguments.

Example:

```csharp
logger.Info("User {0} signed in from {1}", userName, ipAddress);
```

This uses `string.Format(...)` semantics.

### Formatting failure behavior

If formatting throws, the logger falls back to concatenating:

- the original format string
- the joined argument values
- the base exception text

This preserves useful debugging information instead of dropping the log entry.

## Caching Behavior

The factory caches logger instances.

### Category cache key

For category-based loggers, the key is the category string.

Implication:

- requesting the same category twice returns the same logger instance
- later calls with a different host do not create a new logger
- later calls with a different custom file name generator do not replace the existing logger for that category

### Direct file cache key

For direct file loggers, the key is `fileInfo.FullName`.

Implication:

- requesting the same full file path twice returns the same logger instance

## Target File Logging

Direct file logging is useful when a component must write to a predetermined file.

Example:

```csharp
var fileInfo = new FileInfo(Path.Combine("logs", "exports", "manual-export.log"));
ILogger logger = LoggerFactory.GetLogger("ExportService", fileInfo);

logger.Info("Export started");
logger.Fatal("Export failed");
```

Behavior:

- the file directory is created automatically if needed
- the file path is fixed
- date-based naming is not used
- size-based rolling is not used for that specific file path

## Public API Reference

### ILogger

```csharp
public interface ILogger
{
    void Trace(string format, params object[] parameters);
    void Debug(string format, params object[] parameters);
    void Info(string format, params object[] parameters);
    void Warn(string format, params object[] parameters);
    void Error(string format, params object[] parameters);
    void Fatal(string format, params object[] parameters);
    void Log(LoggerLevel loggerLevel, string format, params object[] parameters);
}
```

### LoggerFactory

```csharp
public static class LoggerFactory
{
    public static void UpdatePath(string loggerPath, string loggerConfigurePath);
    public static ILogger GetLogger<T>(this T host, string category) where T : notnull;
    public static ILogger GetLogger<T>(this T host, string category, Func<string>? logFileNameGanerator) where T : notnull;
    public static ILogger GetLogger<T>(this T host, FileInfo fileInfo) where T : notnull;
}
```

### LoggerLevel

```csharp
public enum LoggerLevel
{
    Trace = 1,
    Debug = 2,
    Info = 4,
    Warn = 8,
    Error = 16,
    Fatal = 32
}
```

## Usage Patterns

### Application startup configuration

```csharp
using Tryit.Logger;

LoggerFactory.UpdatePath(
    Path.Combine(AppContext.BaseDirectory, "logs"),
    Path.Combine(AppContext.BaseDirectory, "configs"));

ILogger logger = LoggerFactory.GetLogger("AppHost", "startup");
logger.Info("Application startup completed");
```

### Per-category organization

```csharp
ILogger apiLogger = LoggerFactory.GetLogger("ApiHost", "api");
ILogger jobLogger = LoggerFactory.GetLogger("JobHost", "jobs");
ILogger auditLogger = LoggerFactory.GetLogger("AuditHost", "audit");
```

This keeps files separated by category.

### Generic host object usage

```csharp
public sealed class OrderProcessor
{
    private readonly ILogger logger;

    public OrderProcessor()
    {
        logger = this.GetLogger("orders");
    }

    public void Run(int orderId)
    {
        logger.Info("Processing order {0}", orderId);
    }
}
```

## Operational Notes

- The first category-based logger creation triggers internal configuration initialization
- Category names must not be null, empty, or whitespace
- Host values must not be null
- Empty or whitespace log messages are ignored
- Messages below the configured minimum level are ignored
- Direct file loggers create their target directory when needed
- Loggers are intended to be reused rather than recreated repeatedly

## Known Behaviors and Limitations

This section describes current observable behavior of the implementation.

- Logging is asynchronous, so log files may not exist immediately after a log method returns
- Category logger initialization depends on internal background startup, so startup order matters if you customize paths
- Category names are used as directory names; choose values that are valid for your file system
- The implementation computes a retention threshold from `record_days`, but old file deletion is not currently active in the write path
- Category-based logger caching means the first logger created for a category determines the underlying writer instance for that category
- The public API uses composite formatting, not message-template parsing
- The custom file name generator is expected to return a file name string, not a full path management policy

## Testing

The repository includes automated tests that cover:

- guard clauses for null and invalid inputs
- category logger caching
- direct file logger caching
- host name resolution
- custom file naming
- direct file output
- minimum log level filtering
- formatting fallback behavior
- log line shape validation

Run tests with:

```powershell
dotnet test
```

## Repository

Source repository:

- https://github.com/XtremlyRed/Tryit.Logger

## License

This project is distributed under the license contained in the `LICENSE` file.
