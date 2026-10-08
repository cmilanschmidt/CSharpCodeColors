using CSharpCodeColors.Constants;
using CSharpCodeColors.Enums;
using CSharpCodeColors.Exceptions;
using CSharpCodeColors.Models.Colors;
using CSharpCodeColors.Models.Commands;
using CSharpCodeColors.Models.VsCode;
using CSharpCodeColors.Options;
using CSharpCodeColors.Services.Colors;
using CSharpCodeColors.Services.VisualStudio;
using CSharpCodeColors.Services.VsCode;
using CSharpCodeColors.Services.VsCode.Themes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CSharpCodeColors.Services.Commands;

/// <summary>Runs the command given on the command line once, sets the exit code and stops the host.</summary>
internal sealed class CommandRunner(
    CommandLine commandLine,
    AppOptionsReader settingsReader,
    IVisualStudioReader visualStudio,
    IHostApplicationLifetime lifetime,
    ILogger<CommandRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The work is synchronous (COM on an STA thread); yield so it doesn't hold up the host's startup.
        await Task.Yield();
        try
        {
            Environment.ExitCode = Run();
        }
        catch (FatalException e)
        {
            logger.LogError("{Message}", e.Message);
            Environment.ExitCode = 1;
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Unexpected failure.");
            Environment.ExitCode = 1;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    internal int Run() => commandLine.Command switch
    {
        Command.Generate => Generate(),
        Command.List => List(),
        Command.Themes => Themes(),
        Command.Help => Help(),
        _ => Invalid(),
    };

    private int Generate()
    {
        var settings = settingsReader.Read();

        var vs = visualStudio.Read();
        logger.LogInformation("Read {ItemCount} Fonts and Colors items from Visual Studio 2026 (process {ProcessId}).", vs.Items.Count, vs.ProcessId);

        var colors = new ColorResolver(vs.Items);
        var adaptation = settings.AdaptToVsCodeTheme is { } themeName ? Adapt(settings, colors, themeName) : null;
        var result = VsCodeSettingsBuilder.Build(settings, colors, adaptation);
        foreach (var warning in result.Warnings)
            logger.LogWarning("{Warning}", warning);

        OutputFileWriter.Write(settings.OutputPath, result.Json);
        string scope = (adaptation?.Theme.Contribution.SettingsId ?? settings.VsCodeThemeScope) is { } theme ? $", only for the VS Code theme \"{theme}\"" : "";
        logger.LogInformation("Wrote {OutputPath} ({SemanticRuleCount} semantic token rules, {TextMateRuleCount} TextMate rules{ThemeScope}).",
            settings.OutputPath, result.SemanticRuleCount, result.TextMateRuleCount, scope);

        logger.LogInformation("");
        logger.LogInformation("Summary: {MappedCount} items mapped, {MissingCount} missing, {BackgroundsSkippedCount} with backgrounds skipped.",
            result.Mapped.Count, result.Missing.Count, result.BackgroundsSkipped.Count);
        foreach (var name in result.Missing)
            logger.LogInformation("  missing: {Item}", AppOptions.Show(name));
        foreach (var (item, background) in result.BackgroundsSkipped)
            logger.LogInformation("  background skipped: \"{Item}\" ({Background})", item, background);
        foreach (var name in result.NothingToEmit)
            logger.LogInformation("  no rule needed: \"{Item}\" adds nothing in Visual Studio right now (no color or bold set on it)", name);
        if (result.Missing.Count > 0)
            logger.LogInformation("  Check the names with --list. If C# items are missing, open a C# file in Visual Studio once and run this again.");
        if (adaptation != null)
            ReportAdaptation(adaptation);
        return 0;
    }

    private ThemeAdaptation Adapt(AppOptions settings, ColorResolver colors, string themeName)
    {
        var extensions = VsCodeExtensions.Load(settings.VsCodePath);
        var contribution = extensions.FindTheme(themeName);
        var theme = ThemeFileReader.Read(contribution);
        logger.LogInformation("Adapting to the VS Code theme \"{Theme}\" ({Extension}, {Path}).", contribution.SettingsId, contribution.Extension, contribution.Path);

        bool visualStudioDark = OkLab.FromRgb(colors.Background(ClassificationNames.PlainText).Color).L < 0.5;
        bool themeDark = OkLab.FromRgb(theme.Background).L < 0.5;
        if (visualStudioDark != themeDark)
        {
            logger.LogWarning("Visual Studio uses a {VisualStudioKind} theme, but \"{Theme}\" is {ThemeKind}. Switch Visual Studio to a {ThemeKind} theme for colors that fit.",
                visualStudioDark ? "dark" : "light", contribution.SettingsId, themeDark ? "dark" : "light", themeDark ? "dark" : "light");
        }

        return ThemeAdaptationBuilder.Build(settings, colors, new ThemeTokenResolver(theme, extensions.SemanticTokens));
    }

    private void ReportAdaptation(ThemeAdaptation adaptation)
    {
        var colors = adaptation.Colors;
        int kept = colors.Count(c => c.Origin == AdaptOrigin.Theme);
        logger.LogInformation("");
        logger.LogInformation("Adapted to \"{Theme}\": {KeptCount} items keep the theme's color, {NewCount} get a new color for a distinction the theme doesn't make.",
            adaptation.Theme.Contribution.SettingsId, kept, colors.Count - kept);
        int nameWidth = Math.Max(4, colors.Max(c => c.Item.Length));
        logger.LogInformation("  {Item}  Visual Studio  Theme    Output", "Item".PadRight(nameWidth));
        foreach (var c in colors)
        {
            string how = c.Origin switch
            {
                AdaptOrigin.Theme => $"theme ({adaptation.ThemeStyles[c.Item].Source})",
                AdaptOrigin.Palette => $"new: a theme palette color close to {c.Basis}'s color moved like in Visual Studio",
                AdaptOrigin.Derived => $"new: {c.Basis}'s color moved like in Visual Studio",
                _ => $"new: {c.Basis}'s color moved like in Visual Studio, then adjusted to stay distinct and readable",
            };
            logger.LogInformation("  {Item}  {VisualStudio}        {Theme}  {Output}  {How}", c.Item.PadRight(nameWidth), c.VisualStudio, c.Theme, c.Output, how);
        }
    }

    private int Themes()
    {
        var settings = settingsReader.Read();
        var extensions = VsCodeExtensions.Load(settings.VsCodePath);
        logger.LogInformation("VS Code color themes ({InstallPath} and your extensions).", extensions.InstallPath);
        logger.LogInformation("Use the name in the first column for AdaptToVsCodeTheme and VsCodeThemeScope.");
        logger.LogInformation("");
        var themes = extensions.Themes.OrderBy(t => t.SettingsId, StringComparer.OrdinalIgnoreCase).ToList();
        int nameWidth = Math.Max(4, themes.Select(t => t.SettingsId.Length).DefaultIfEmpty().Max());
        int labelWidth = Math.Max(12, themes.Select(t => t.Label.Length).DefaultIfEmpty().Max());
        logger.LogInformation("{Name}  {Label}  {Kind,-20}  Extension", "Name".PadRight(nameWidth), "Theme picker".PadRight(labelWidth), "Type");
        foreach (var theme in themes)
        {
            string kind = theme.UiTheme switch
            {
                "vs" => "light",
                "vs-dark" => "dark",
                "hc-black" => "dark, high contrast",
                "hc-light" => "light, high contrast",
                _ => theme.UiTheme,
            };
            logger.LogInformation("{Name}  {Label}  {Kind,-20}  {Extension}", theme.SettingsId.PadRight(nameWidth), theme.Label.PadRight(labelWidth), kind, theme.Extension);
        }
        return 0;
    }

    private int List()
    {
        var vs = visualStudio.Read();
        var colors = new ColorResolver(vs.Items);
        logger.LogInformation("Fonts and Colors items (Text Editor) of Visual Studio 2026, process {ProcessId}.", vs.ProcessId);
        logger.LogInformation("A color shown as '#RRGGBB' is set explicitly (by the theme or by you). 'default' means Visual Studio");
        logger.LogInformation("has no color for the item (Default/Automatic in Tools > Options) and the editor inherits the color shown.");
        logger.LogInformation("");

        int nameWidth = Math.Max(4, vs.Items.Max(i => i.Name.Length));
        logger.LogInformation("{Item}  {Foreground,-44}  {Background,-44}  Bold", "Item".PadRight(nameWidth), "Foreground", "Background");
        foreach (var item in vs.Items)
        {
            string foreground = Describe(item.Foreground, colors.Foreground(item.Name), item.Name);
            string background = Describe(item.Background, colors.Background(item.Name), item.Name);
            logger.LogInformation("{Item}  {Foreground,-44}  {Background,-44}  {Bold}", item.Name.PadRight(nameWidth), foreground, background, item.Bold ? "yes" : "no");
        }
        return 0;

        static string Describe(VsColor color, EffectiveColor effective, string name) =>
            !color.IsDefault ? color.Color.ToString()
            : ClassificationHierarchy.IsAdditive(name) ? "default (adds nothing)"
            : effective.FromItem.Equals(name, StringComparison.OrdinalIgnoreCase) ? $"default -> {effective.Color}"
            : $"default -> {effective.Color} (from {effective.FromItem})";
    }

    private int Help()
    {
        logger.LogInformation("{Usage}", CommandLine.Usage);
        return 0;
    }

    private int Invalid()
    {
        logger.LogError("Unknown arguments: {Arguments}{NewLine}{Usage}",
            string.Join(' ', commandLine.Args.Select(a => $"\"{a}\"")), Environment.NewLine, CommandLine.Usage);
        return 1;
    }
}
