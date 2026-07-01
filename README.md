# Tryit.Logger

Tryit.Logger is a lightweight file-based logging library for .NET applications. It provides a simple `ILogger` API, category-based logger caching, configurable log output paths, log level filtering, asynchronous background flushing, and automatic file rotation.

## Features

- Simple logging API: `Trace`, `Debug`, `Info`, `Warn`, `Error`, `Fatal`
- Category-based logger creation and reuse
- File-based output with automatic directory creation
- Background write loop for buffered log persistence
- Configurable minimum log level
- Daily log file naming by default
- File rotation based on maximum file size
- Optional custom log file name generation
- Optional direct logging to a specific file
- Multi-targeted package for modern and legacy .NET platforms

## Supported Target Frameworks

This library targets:

- .NET 6
- .NET 8
- .NET 10
- .NET Framework 4.8
- .NET Framework 4.5.1
- .NET Standard 2.0
- .NET Standard 2.1

## Installation

Install the package from NuGet:

```powershell
dotnet add package Tryit.Logger
```

Or with the NuGet Package Manager:

```powershell
Install-Package Tryit.Logger
```

## Quick Start

### 1. Create a logger by category

```csharp
using Tryit.Logger;

var logger = this.GetLogger("app");

logger.Info("Application started at {0}", DateTime.Now);
logger.Warn("Cache is warming up");
logger.Error("Request failed for user {0}", userId);
```

### 2. Create a logger with a string host name

```csharp
using Tryit.Logger;

var logger = LoggerFactory.GetLogger("WorkerService", "jobs");

logger.Info("Job queue initialized");
```

### 3. Use a custom log file name

```csharp
using Tryit.Logger;

var logger = LoggerFactory.GetLogger("ApiHost", "requests", () => "api-requests.log");

logger.Warn("Slow request detected");
```

The generated file name is normalized to avoid duplicating the `.log` extension. For example, `api-requests.log` becomes `api-requests.1.log`.

### 4. Write directly to a specific file

```csharp
using Tryit.Logger;

var fileLogger = LoggerFactory.GetLogger(
	"ImportService",
	new FileInfo(Path.Combine("logs", "imports", "manual-import.log")));

fileLogger.Fatal("Import terminated unexpectedly");
```

## Logger Creation Model

`LoggerFactory` caches loggers to ensure the same logger instance is reused for the same key:

- `GetLogger(host, category)` caches by `category`
- `GetLogger(host, fileInfo)` caches by `fileInfo.FullName`

This means repeated calls with the same category or target file return the same logger instance.

## Host Name Resolution

The host value is written into each log line. The logger resolves the host name as follows:

- If the host is a `string`, that string is used
- If the host is a `Type`, the type name is used
- Otherwise, the runtime type name of the object is used

Example:

```csharp
var logger = LoggerFactory.GetLogger(new OrderProcessor(), "orders");
logger.Info("Order created");
```

This writes `OrderProcessor` as the host name.

## Default Paths

By default, the library uses:

- Log path: `..\logs`
- Configuration path: `configs`

You can override them before creating loggers:

```csharp
LoggerFactory.UpdatePath(
	loggerPath: Path.Combine(AppContext.BaseDirectory, "logs"),
	loggerConfigurePath: Path.Combine(AppContext.BaseDirectory, "configs"));
```

`UpdatePath` should be called during application startup, before the first logger is created.

## Configuration

The library reads settings from:

```text
{config-path}/logger.json
```

Example `logger.json`:

```json
{
  "file_size": 2,
  "record_delay": 0.1,
  "record_days": -1,
  "min_level": 4
}
```

### Configuration Keys

| Key | Type | Description |
| --- | --- | --- |
| `file_size` | number | Maximum size of a single log file in MB before rotating. Minimum effective value is `0.1`. |
| `record_delay` | number | Background flush delay in seconds. Minimum effective value is `0.1`. |
| `record_days` | number | Retention-related setting read by the writer. A value below `1` disables retention threshold calculation. |
| `min_level` | enum/integer | Minimum log level to write. Messages below this level are ignored. |

### Log Level Values

`LoggerLevel` uses the following values:

| Name | Value |
| --- | ---: |
| `Trace` | 1 |
| `Debug` | 2 |
| `Info` | 4 |
| `Warn` | 8 |
| `Error` | 16 |
| `Fatal` | 32 |

Example: to write only warnings and above, set:

```json
{
  "min_level": 8
}
```

## Log Output Structure

When using category-based logging, files are written to:

```text
{log-path}/{category}/
```

Default file naming pattern:

```text
yyyy-MM-dd.{index}.log
```

Examples:

- `logs/app/2025-01-15.1.log`
- `logs/app/2025-01-15.2.log`

Custom file naming pattern:

```text
{custom-name}.{index}.log
```

Example:

- `logs/requests/api-requests.1.log`

## Log Entry Format

Each log entry is written as a single line using this structure:

```text
HH:mm:ss.fff,LEVEL,THREAD,HOST,MESSAGE
```

Example:

```text
09:41:12.135, Warn,12,WorkerService,Slow request detected
```

Where:

- `HH:mm:ss.fff` is the local timestamp
- `LEVEL` is the padded log level text
- `THREAD` is the thread name or managed thread ID
- `HOST` is the resolved host name
- `MESSAGE` is the final formatted message

## Formatting Behavior

All logging methods support composite formatting:

```csharp
logger.Info("User {0} signed in from {1}", userName, ipAddress);
```

If formatting fails, the logger falls back to writing the original format string, appended arguments, and the base exception information.

## Public API

### `ILogger`

- `Trace(string format, params object[] parameters)`
- `Debug(string format, params object[] parameters)`
- `Info(string format, params object[] parameters)`
- `Warn(string format, params object[] parameters)`
- `Error(string format, params object[] parameters)`
- `Fatal(string format, params object[] parameters)`
- `Log(LoggerLevel loggerLevel, string format, params object[] parameters)`

### `LoggerFactory`

- `GetLogger<T>(this T host, string category)`
- `GetLogger<T>(this T host, string category, Func<string>? logFileNameGanerator)`
- `GetLogger<T>(this T host, FileInfo fileInfo)`
- `UpdatePath(string loggerPath, string loggerConfigurePath)`

## Notes

- Logging is buffered and written by a background worker.
- The library creates target directories automatically when needed.
- The first logger creation triggers internal initialization.
- Category names must not be null, empty, or whitespace.
- Host values must not be null.

## Development

Repository URL:

- https://github.com/XtremlyRed/Tryit.Logger

The project includes automated tests covering logger creation, caching, custom file names, target-file logging, log level filtering, and formatting fallback behavior.

## License

This project is distributed under the license included in the `LICENSE` file.
