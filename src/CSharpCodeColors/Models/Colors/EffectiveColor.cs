namespace CSharpCodeColors.Models.Colors;

/// <summary>A color after inheritance, plus the item it actually came from.</summary>
internal readonly record struct EffectiveColor(RgbColor Color, string FromItem);
