using CSharpCodeColors.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace CSharpCodeColors.Extensions;

internal static class LoggingBuilderExtensions
{
    /// <summary>Plain console output; warnings and errors go to standard error.</summary>
    public static ILoggingBuilder AddPlainConsole(this ILoggingBuilder logging)
    {
        logging
            .AddConsole(options =>
            {
                options.FormatterName = PlainConsoleFormatter.FormatterName;
                options.LogToStandardErrorThreshold = LogLevel.Warning;
            })
            .AddConsoleFormatter<PlainConsoleFormatter, ConsoleFormatterOptions>()
            .AddFilter("Microsoft", LogLevel.Warning);
        logging.Services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
        return logging;
    }
}
