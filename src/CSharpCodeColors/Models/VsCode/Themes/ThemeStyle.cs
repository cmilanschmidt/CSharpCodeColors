using CSharpCodeColors.Models.Colors;

namespace CSharpCodeColors.Models.VsCode.Themes;

/// <summary>The color a theme shows a token in, and what it came from (for the report).</summary>
internal sealed record ThemeStyle(RgbColor Foreground, string Source);
