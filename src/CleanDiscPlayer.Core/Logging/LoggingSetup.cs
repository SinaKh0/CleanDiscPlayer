using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace CleanDiscPlayer.Core.Logging
{
    /// <summary>
    /// Extension methods for configuring application logging.
    /// </summary>
    public static class LoggingSetup
    {
        /// <summary>
        /// Configures logging using the provided configuration.
        /// Sets up Serilog with console and/or file sinks based on configuration.
        /// </summary>
        /// <param name="builder">The logging builder to configure.</param>
        /// <param name="config">Logging configuration specifying console/file settings.</param>
        /// <returns>The configured logging builder.</returns>
        public static ILoggingBuilder ConfigureAppLogging(
            this ILoggingBuilder builder,
            LoggingConfiguration config)
        {
            // Create Serilog logger configuration
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Is(ConvertToSerilogLevel(config.ConsoleLevel));

            // Add console sink if enabled
            if (config.EnableConsoleLogging)
            {
                loggerConfig.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                );
            }

            // Add file sink if enabled
            if (config.EnableFileLogging)
            {
                var logPath = config.LogFilePath ?? config.GetDefaultLogFilePath();
                var logDir = Path.GetDirectoryName(logPath);

                if (!string.IsNullOrEmpty(logDir))
                    Directory.CreateDirectory(logDir);

                loggerConfig.WriteTo.File(
                    logPath,
                    rollingInterval: RollingInterval.Day,      // New file each day
                    retainedFileCountLimit: 7,                 // Keep last 7 days
                    fileSizeLimitBytes: 10 * 1024 * 1024,      // 10 MB per file
                    rollOnFileSizeLimit: true                  // Create new file if too big
                );

                if (config.EnableConsoleLogging)
                    Console.WriteLine($"Logging to file: {logPath}");
            }

            // Build Serilog logger
            Log.Logger = loggerConfig.CreateLogger();

            // Clear existing providers and add Serilog
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: true);

            return builder;
        }

        /// <summary>
        /// Converts Microsoft LogLevel to Serilog LogEventLevel.
        /// </summary>
        private static LogEventLevel ConvertToSerilogLevel(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => LogEventLevel.Verbose,
                LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Information => LogEventLevel.Information,
                LogLevel.Warning => LogEventLevel.Warning,
                LogLevel.Error => LogEventLevel.Error,
                LogLevel.Critical => LogEventLevel.Fatal,
                _ => LogEventLevel.Information
            };
        }
    }
}