using CSharpCodeColors.Models.VsCode;
using CSharpCodeColors.Models.VsCode.Themes;
using CSharpCodeColors.Options;
using CSharpCodeColors.Services.Colors;
using CSharpCodeColors.Services.VsCode.Themes;

namespace CSharpCodeColors.Services.VsCode;

/// <summary>Adapts the colors of the mapped Visual Studio items to a VS Code theme (see <see cref="ThemeAdapter"/>).</summary>
internal static class ThemeAdaptationBuilder
{
    /// <summary>
    /// The items a theme's colors are mainly for, most important first. When a theme shows several of these alike
    /// and Visual Studio doesn't (Monokai's pink for keywords, operators and regex anchors), the first keeps the
    /// theme's color and the rest get new ones. Other items get a new color before any of these does.
    /// </summary>
    internal static readonly string[] AnchorPriority =
    [
        "Plain Text", "Keyword", "Comment", "String", "Number",
        "class name", "method name", "local name", "parameter name",
        "keyword - control", "Operator", "punctuation",
        "property name", "field name", "namespace name", "interface name", "struct name", "enum name",
        "type parameter name", "delegate name", "enum member name", "constant name", "event name",
        "Preprocessor Keyword",
    ];

    public static ThemeAdaptation Build(AppOptions settings, ColorResolver colors, ThemeTokenResolver theme)
    {
        var inputs = new List<AdaptInput>();
        var themeStyles = new Dictionary<string, ThemeStyle>(StringComparer.OrdinalIgnoreCase);
        var styles = new Dictionary<string, TokenStyle?>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in settings.Mappings)
        {
            var item = colors.Find(mapping.VisualStudioItem);
            if (item == null || themeStyles.ContainsKey(item.Name) || styles.ContainsKey(item.Name))
                continue;

            if (ClassificationHierarchy.IsAdditive(item.Name))
            {
                // Additive items (static symbol) add only bold: a color of their own would override the adapted color
                // of every static symbol, and the theme's own rules (italic statics) still apply since nothing resets them.
                styles[item.Name] = item.Bold ? new TokenStyle(null, Bold: true, Additive: true) : null;
                continue;
            }

            var style = theme.ResolveMapping(mapping.SemanticTokens, mapping.TextMateScopes);
            themeStyles[item.Name] = style;
            int priority = Array.FindIndex(AnchorPriority, p => p.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
            inputs.Add(new AdaptInput(item.Name, colors.Foreground(item.Name).Color, style.Foreground, priority >= 0 ? priority : null));
        }

        var adapted = ThemeAdapter.Adapt(inputs, theme.Theme.Background, theme.Theme.Palette);
        foreach (var color in adapted)
            styles[color.Item] = new TokenStyle(color.Output, Bold: null, Additive: false);
        return new ThemeAdaptation(theme.Theme, styles, adapted, themeStyles);
    }
}
