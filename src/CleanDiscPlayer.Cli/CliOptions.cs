using CleanDiscPlayer.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CleanDiscPlayer.Cli
{
    /// <summary>
    /// Parses and stores command-line options for the CLI application.
    /// </summary>
    public class CliOptions
    {
        /// <summary>
        /// Gets or sets whether verbose logging is enabled (Debug level).
        /// </summary>
        public bool Verbose { get; set; }

        /// <summary>
        /// Gets or sets whether debug logging is enabled (Trace level).
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Gets or sets whether quiet mode is enabled (Warning level and above only).
        /// </summary>
        public bool Quiet { get; set; }

        /// <summary>
        /// Gets or sets whether help was requested.
        /// </summary>
        public bool ShowHelp { get; set; }

        /// <summary>
        /// Gets or sets whether file logging is enabled.
        /// </summary>
        public bool EnableFileLogging { get; set; }

        /// <summary>
        /// Gets or sets the custom log file path. If null, uses default location.
        /// </summary>
        public string? LogFilePath { get; set; }

        /// <summary>
        /// Parses command-line arguments into a CliOptions object.
        /// </summary>
        /// <param name="args">Command-line arguments from Main().</param>
        /// <returns>A CliOptions instance with parsed values.</returns>
        /// <remarks>
        /// Supported arguments:
        /// <list type="bullet">
        /// <item><c>--help</c> or <c>-h</c>: Display help information</item>
        /// <item><c>--verbose</c> or <c>-v</c>: Enable verbose (Debug) logging</item>
        /// <item><c>--debug</c> or <c>-d</c>: Enable debug (Trace) logging</item>
        /// <item><c>--log-file</c> or <c>-l</c> [path]: Enable file logging with optional path</item>
        /// </list>
        /// </remarks>
        public static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--verbose":
                    case "-v":
                        options.Verbose = true;
                        break;

                    case "--debug":
                    case "-d":
                        options.Debug = true;
                        break;

                    //case "--quiet":
                    //case "-q":
                    //    options.Quiet = true;
                    //    break;

                    case "--log-file":
                    case "-l":
                        options.EnableFileLogging = true;
                        // Check if next arg is a path (doesn't start with -)
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            options.LogFilePath = args[i + 1];
                            i++; // Skip next arg since we consumed it
                        }
                        break;
                    case "--help":
                    case "-h":
                    case "/?":
                        options.ShowHelp = true;
                        break;
                }
            }

            return options;
        }


        /// <summary>
        /// Displays help information about command-line options.
        /// </summary>
        public static void ShowHelpText()
        {
            Console.WriteLine("CleanDisc Player - Audio CD Player");
            Console.WriteLine();
            Console.WriteLine("Usage: CleanDiscPlayer [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -h, --help              Display this help information");
            Console.WriteLine("  -v, --verbose           Enable verbose logging (shows debug and info messages)");
            Console.WriteLine("  -d, --debug             Enable debug logging (shows all messages including trace)");
            Console.WriteLine("  -l, --log-file [path]   Enable file logging (optional: specify custom log file path)");
            Console.WriteLine();
            Console.WriteLine("Logging Levels:");
            Console.WriteLine("  Default (no flags)      Shows warnings and errors only");
            Console.WriteLine("  --verbose               Shows debug, info, warnings, and errors");
            Console.WriteLine("  --debug                 Shows everything including trace messages");
            Console.WriteLine();
            Console.WriteLine("Default log file location:");
            Console.WriteLine("  Windows: %LOCALAPPDATA%\\CleanDiscPlayer\\logs\\cleandisc-YYYY-MM-DD.log");
            Console.WriteLine("  Linux:   ~/.local/share/CleanDiscPlayer/logs/cleandisc-YYYY-MM-DD.log");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  CleanDiscPlayer                    # Run with default (quiet) logging");
            Console.WriteLine("  CleanDiscPlayer --verbose          # Run with verbose console output");
            Console.WriteLine("  CleanDiscPlayer --log-file         # Run with file logging enabled");
            Console.WriteLine("  CleanDiscPlayer -d -l mylog.txt    # Debug mode with custom log file");
            Console.WriteLine();
        }

        /// <summary>
        /// Converts CLI options into a LoggingConfiguration object.
        /// </summary>
        /// <returns>A LoggingConfiguration with settings based on parsed CLI arguments.</returns>
        /// <remarks>
        /// Priority order for log levels (if multiple flags set):
        /// <list type="number">
        /// <item>Debug (--debug) → Trace level</item>
        /// <item>Verbose (--verbose) → Debug level</item>
        /// <item>Quiet (--quiet) → Warning level</item>
        /// <item>Default → Information level</item>
        /// </list>
        /// Console logging is always enabled for CLI.
        /// </remarks>
        public LoggingConfiguration ToLoggingConfiguration()
        {
            var config = new LoggingConfiguration
            {
                EnableConsoleLogging = true,  // CLI always has console
                EnableFileLogging = this.EnableFileLogging,
                LogFilePath = this.LogFilePath
            };

            // Set log level based on flags (higher priority first)
            if (Debug)
                config.ConsoleLevel = LogLevel.Trace;
            else if (Verbose)
                config.ConsoleLevel = LogLevel.Debug;
            //else if (Quiet)
            //    config.ConsoleLevel = LogLevel.Warning;
            else
                config.ConsoleLevel = LogLevel.Information;

            return config;
        }
    }
}