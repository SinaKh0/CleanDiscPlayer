using Microsoft.Extensions.Logging;

namespace CleanDiscPlayer.Core.Logging
{
    /// <summary>
    /// Configuration settings for application logging.
    /// </summary>
    public class LoggingConfiguration
    {
        /// <summary>
        /// Minimum log level for console output.
        /// </summary>
        public LogLevel ConsoleLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// Whether to enable file logging.
        /// </summary>
        public bool EnableFileLogging { get; set; }

        /// <summary>
        /// Whether to enable console logging.
        /// </summary>
        public bool EnableConsoleLogging { get; set; } = true;

        /// <summary>
        /// Custom path for log file. If null, uses default path.
        /// </summary>
        public string? LogFilePath { get; set; }

        /// <summary>
        /// Default logging configuration (Information level, console only).
        /// </summary>
        public static LoggingConfiguration Default => new()
        {
            //ConsoleLevel = LogLevel.Information,
            ConsoleLevel = LogLevel.Warning,
            EnableFileLogging = false,
            EnableConsoleLogging = true
        };

        /// <summary>
        /// Verbose logging configuration (Debug level, console only).
        /// </summary>
        public static LoggingConfiguration Verbose => new()
        {
            ConsoleLevel = LogLevel.Debug,
            EnableFileLogging = false,
            EnableConsoleLogging = true
        };

        /// <summary>
        /// Debug logging configuration (Trace level, console + file).
        /// </summary>
        public static LoggingConfiguration Debug => new()
        {
            ConsoleLevel = LogLevel.Trace,
            EnableFileLogging = true,
            EnableConsoleLogging = true
        };

        /// <summary>
        /// GUI mode logging configuration (file only, no console).
        /// </summary>
        public static LoggingConfiguration GuiMode => new()
        {
            ConsoleLevel = LogLevel.Information,
            EnableFileLogging = true,
            EnableConsoleLogging = false
        };

        /// <summary>
        /// Gets the default log file path in user's AppData folder.
        /// </summary>
        public string GetDefaultLogFilePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CleanDiscPlayer",
                "logs",
                $"cleandisc-{DateTime.Now:yyyy-MM-dd}.log"
            );

            // C:\Program Files\CleanDiscPlayer\logs\  (requires admin)
            // or wherever the .exe is located:
            // Path.Combine(AppContext.BaseDirectory, "logs", $"cleandisc-{DateTime.Now:yyyy-MM-dd}.log")
        }
    }
}