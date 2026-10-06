using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace CSharpCodeColors.Logging;

/// <summary>
/// Writes log messages as plain command-line output: information as is, warnings and errors with a
/// "warning: " or "error: " prefix. No category, event ID or timestamp.
/// </summary>
internal sealed class PlainConsoleFormatter() : ConsoleFormatter(FormatterName)
{
    public const string FormatterName = "plain";

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        string message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        textWriter.Write(logEntry.LogLevel switch
        {
            LogLevel.Warning => "warning: ",
            LogLevel.Error or LogLevel.Critical => "error: ",
            _ => "",
        });
        textWriter.WriteLine(message);
        if (logEntry.Exception != null)
            textWriter.WriteLine(logEntry.Exception);
    }
}
