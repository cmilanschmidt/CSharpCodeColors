using CSharpCodeColors.Enums;

namespace CSharpCodeColors.Models.Commands;

/// <summary>The command given on the command line.</summary>
internal sealed record CommandLine(Command Command, IReadOnlyList<string> Args)
{
    public const string Usage = """
        Usage:
          CSharpCodeColors            Write VS Code C# color overrides from the running Visual Studio 2026 (settings in appsettings.jsonc).
          CSharpCodeColors --list     List every Fonts and Colors item Visual Studio exposes.
          CSharpCodeColors --themes   List the installed VS Code color themes (names for AdaptToVsCodeTheme and VsCodeThemeScope).
        """;

    public static CommandLine Parse(string[] args) => new(args switch
    {
        [] => Command.Generate,
        ["--list"] => Command.List,
        ["--themes"] => Command.Themes,
        ["--help" or "-h" or "-?" or "/?"] => Command.Help,
        _ => Command.Invalid,
    }, args);
}
