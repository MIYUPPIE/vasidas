using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using ILogger = Serilog.ILogger;

namespace AutomationStudio.Infrastructure.Logging;

/// <summary>
/// Wires up the Serilog sinks required by section 27 (detailed execution logs, one line per
/// event, timestamped) — console for live viewing plus a rolling daily file under
/// <paramref name="logDirectory"/> so a run can be inspected after the fact.
/// </summary>
public static class LoggingSetup
{
    public static ILogger CreateSerilogLogger(string logDirectory, LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        Directory.CreateDirectory(logDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(logDirectory, "automationstudio-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    public static ILoggerFactory CreateLoggerFactory(string logDirectory, LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        var serilogLogger = CreateSerilogLogger(logDirectory, minimumLevel);
        return new LoggerFactory().AddSerilog(serilogLogger, dispose: true);
    }
}
