using CSharpCodeColors.Models.Colors;

namespace CSharpCodeColors.Models.VsCode.Themes;

/// <summary>A VS Code color theme after following its "include" chain.</summary>
/// <param name="Foreground">"editor.foreground": the color of text no token rule colors.</param>
/// <param name="Background">"editor.background".</param>
/// <param name="SemanticHighlighting">The theme's "semanticHighlighting" (off unless the theme turns it on).</param>
internal sealed record VsCodeTheme(
    ThemeContribution Contribution,
    RgbColor Foreground,
    RgbColor Background,
    IReadOnlyList<TextMateRule> TextMateRules,
    IReadOnlyList<SemanticRule> SemanticRules,
    bool SemanticHighlighting)
{
    /// <summary>Every distinct foreground the theme's token rules use, for any language.</summary>
    public IReadOnlyList<RgbColor> Palette { get; } =
        TextMateRules.Select(r => r.Foreground).Concat(SemanticRules.Select(r => (RgbColor?)r.Foreground))
            .OfType<RgbColor>().Prepend(Foreground).Distinct().ToList();
}
