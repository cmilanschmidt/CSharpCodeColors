namespace CSharpCodeColors.Models.VsCode.Themes;

/// <summary>A color theme an installed VS Code extension contributes ("contributes.themes" in its package.json).</summary>
/// <param name="Id">The theme's "id", if it has one.</param>
/// <param name="Label">The name the theme picker shows, with %placeholders% resolved from package.nls.json.</param>
/// <param name="UiTheme">"vs" (light), "vs-dark", "hc-black" or "hc-light".</param>
/// <param name="Path">The theme file.</param>
/// <param name="Extension">The extension's publisher.name, e.g. "vscode.theme-defaults".</param>
internal sealed record ThemeContribution(string? Id, string Label, string UiTheme, string Path, string Extension)
{
    /// <summary>The name VS Code matches "[Theme Name]" blocks in settings against.</summary>
    public string SettingsId => Id ?? Label;

    public bool IsDark => UiTheme is "vs-dark" or "hc-black";
}
