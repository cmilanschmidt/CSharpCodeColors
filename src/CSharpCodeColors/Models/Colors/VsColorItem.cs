namespace CSharpCodeColors.Models.Colors;

/// <summary>A Fonts and Colors item of the Text Editor category, as Visual Studio reports it.</summary>
internal sealed record VsColorItem(string Name, VsColor Foreground, VsColor Background, bool Bold);
