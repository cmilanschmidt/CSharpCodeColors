using CSharpCodeColors.Models.Colors;
using CSharpCodeColors.Models.VsCode.Themes;

namespace CSharpCodeColors.Services.VsCode.Themes;

/// <summary>
/// Works out the color a VS Code theme gives a C# token, following VS Code's colorThemeData.ts: the theme's
/// semantic rules first, then the TextMate scopes registered for the token type, probed in the theme's tokenColors.
/// Font styles aren't needed: the adapted output leaves them to the theme.
/// </summary>
internal sealed class ThemeTokenResolver(VsCodeTheme theme, SemanticTokenRegistry registry)
{
    public const string Language = "csharp";

    public VsCodeTheme Theme => theme;

    /// <summary>
    /// The color the theme gives the tokens of a mapping: that of its first semantic token, falling back to the
    /// color most of its TextMate scopes get, then to the editor foreground.
    /// </summary>
    public ThemeStyle ResolveMapping(IReadOnlyList<string> semanticTokens, IReadOnlyList<string> textMateScopes)
    {
        if (semanticTokens.Count > 0 && ResolveSemantic(semanticTokens[0]) is { } color)
            return new ThemeStyle(color, $"semantic {semanticTokens[0]}");
        if (MostCommonScopeColor(textMateScopes) is { } scopeColor)
            return new ThemeStyle(scopeColor, "TextMate scopes");
        return new ThemeStyle(theme.Foreground, "editor.foreground");
    }

    /// <summary>
    /// The color most of the scopes get (a scope no rule colors counts as uncolored), so that one unusual scope
    /// in a long list (meta.embedded in punctuation) doesn't decide; null if most are uncolored.
    /// </summary>
    private RgbColor? MostCommonScopeColor(IReadOnlyList<string> textMateScopes) => textMateScopes
        .Select(s => ResolveScopes([s.Split(' ', StringSplitOptions.RemoveEmptyEntries)]))
        .GroupBy(c => c)
        .OrderByDescending(g => g.Count())
        .FirstOrDefault()?.Key;

    /// <summary>
    /// The color of a semantic token ("class:csharp", "variable.readonly:csharp"), or null where VS Code would
    /// fall back to the TextMate grammar's color. The best scoring theme rule wins; only without one do the scopes
    /// registered for the token type count, again the best scoring. Later rules win ties.
    /// </summary>
    public RgbColor? ResolveSemantic(string selector)
    {
        var token = SemanticSelector.Parse(selector);
        string language = token.Language ?? Language;

        RgbColor? color = null;
        int best = -1;
        foreach (var rule in theme.SemanticRules)
        {
            int score = registry.Score(rule.Selector, token.Type, token.Modifiers, language);
            if (score >= 0 && score >= best)
                (color, best) = (rule.Foreground, score);
        }
        if (color != null)
            return color;

        foreach (var (defaultSelector, scopesToProbe) in registry.Defaults)
        {
            int score = registry.Score(defaultSelector, token.Type, token.Modifiers, language);
            if (score >= 0 && score >= best && ResolveScopes(scopesToProbe) is { } probed)
                (color, best) = (probed, score);
        }
        return color;
    }

    /// <summary>
    /// The foreground for the first scope list (outermost scope first) that a theme rule matches: the best scoring
    /// rule with a foreground, later rules winning ties. As in VS Code, a list whose matching rules only set a font
    /// style ends the search without a color.
    /// </summary>
    public RgbColor? ResolveScopes(IEnumerable<IReadOnlyList<string>> scopeLists)
    {
        foreach (var scopes in scopeLists)
        {
            RgbColor? foreground = null;
            int foregroundScore = -1;
            bool matched = false;
            foreach (var rule in theme.TextMateRules)
            {
                int score = rule.Scope.Match(scopes);
                if (score < 0)
                    continue;
                matched |= rule.Foreground != null || rule.SetsFontStyle;
                if (rule.Foreground != null && score >= foregroundScore)
                    (foreground, foregroundScore) = (rule.Foreground, score);
            }
            if (matched)
                return foreground;
        }
        return null;
    }
}
