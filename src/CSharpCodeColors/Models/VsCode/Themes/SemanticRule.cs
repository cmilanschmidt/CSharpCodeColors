using CSharpCodeColors.Models.Colors;

namespace CSharpCodeColors.Models.VsCode.Themes;

/// <summary>A rule of a theme's "semanticTokenColors" that sets a color, such as "property.readonly:csharp": "#4FC1FF".</summary>
internal sealed record SemanticRule(SemanticSelector Selector, RgbColor Foreground);
