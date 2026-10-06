using CSharpCodeColors.Enums;

namespace CSharpCodeColors.Models.Commands;

/// <summary>The command given on the command line.</summary>
internal sealed record CommandLine(Command Command, IReadOnlyList<string> Args)
{
    public const string Usage = """
        Usage:
          CSharpCodeColors          Write VS Code C# color overrides from the running Visual Studio 2026 (settings in appsettings.json).
          CSharpCodeColors --list   List every Fonts and Colors item Visual Studio exposes.
        """;

    public static CommandLine Parse(string[] args) => new(args switch
    {
        [] => Command.Generate,
        ["--list"] => Command.List,
        ["--help" or "-h" or "-?" or "/?"] => Command.Help,
        _ => Command.Invalid,
    }, args);
}
