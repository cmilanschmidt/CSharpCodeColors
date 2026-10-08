using CSharpCodeColors.Models.VsCode.Themes;
using CSharpCodeColors.Services.Colors;

namespace CSharpCodeColors.Models.VsCode;

/// <summary>
/// The Visual Studio colors adapted to a VS Code theme: the style to write for each Fonts and Colors item
/// (null: nothing to write), and how each color was chosen.
/// </summary>
internal sealed record ThemeAdaptation(
    VsCodeTheme Theme,
    IReadOnlyDictionary<string, TokenStyle?> Styles,
    IReadOnlyList<AdaptedColor> Colors,
    IReadOnlyDictionary<string, ThemeStyle> ThemeStyles);
