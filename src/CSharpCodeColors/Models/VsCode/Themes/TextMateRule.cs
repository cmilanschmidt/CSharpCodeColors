using CSharpCodeColors.Models.Colors;
using CSharpCodeColors.Services.VsCode.Themes;

namespace CSharpCodeColors.Models.VsCode.Themes;

/// <summary>
/// A rule of a theme's "tokenColors". Only its color is used, but a rule that only sets a font style still
/// matters: when VS Code probes scopes for a semantic token, such a rule ends the search.
/// </summary>
internal sealed record TextMateRule(ScopeSelector Scope, RgbColor? Foreground, bool SetsFontStyle);
