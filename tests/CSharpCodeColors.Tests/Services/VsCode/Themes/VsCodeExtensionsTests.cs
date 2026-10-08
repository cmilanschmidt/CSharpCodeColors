using CSharpCodeColors.Exceptions;
using CSharpCodeColors.Models.VsCode.Themes;
using CSharpCodeColors.Services.VsCode.Themes;
using CSharpCodeColors.Tests.TestData;

namespace CSharpCodeColors.Tests.Services.VsCode.Themes;

public class VsCodeExtensionsTests
{
    /// <summary>
    /// A portable VS Code (data\extensions next to the install), so the real user extensions stay out of the test;
    /// built-in extensions under a build folder, as newer versions lay them out.
    /// </summary>
    private static TempDirectory FakeInstall()
    {
        var dir = new TempDirectory();
        dir.Write(@"0123abcd\resources\app\extensions\theme-defaults\package.json", """
            { "name": "theme-defaults", "contributes": { "themes": [
                { "id": "Dark Modern", "label": "%darkModern%", "uiTheme": "vs-dark", "path": "./themes/dark_modern.json" },
                { "label": "Plain Label", "uiTheme": "vs", "path": "./themes/plain.json" }
            ] } }
            """);
        dir.Write(@"0123abcd\resources\app\extensions\theme-defaults\package.nls.json", """{ "darkModern": { "message": "Dark Modern", "comment": [] } }""");
        dir.Write(@"data\extensions\ms-dotnettools.csharp-2.0.0\package.json", """
            { "name": "csharp", "publisher": "ms-dotnettools", "contributes": {
                "semanticTokenTypes": [ { "id": "controlKeyword", "superType": "keyword" } ],
                "semanticTokenScopes": [ { "language": "csharp", "scopes": { "keyword": [ "keyword.cs" ] } } ],
                "themes": [ { "label": "Visual Studio 2019 Dark", "uiTheme": "vs-dark", "path": "./themes/vs2019_dark.json" } ]
            } }
            """);
        dir.Write(@"data\extensions\broken\package.json", "{ not json");
        return dir;
    }

    [Test]
    public async Task Reads_built_in_and_user_themes_with_localized_labels()
    {
        using var install = FakeInstall();
        var extensions = VsCodeExtensions.Load(install.Path);
        await Assert.That(extensions.Themes.Select(t => t.SettingsId)).IsEquivalentTo(["Dark Modern", "Plain Label", "Visual Studio 2019 Dark"]);
        var modern = extensions.Themes[0];
        await Assert.That((modern.Label, modern.Extension, modern.IsDark)).IsEqualTo(("Dark Modern", "vscode.theme-defaults", true));
        await Assert.That(modern.Path).EndsWith(@"themes\dark_modern.json");
        await Assert.That(extensions.Themes[2].Extension).IsEqualTo("ms-dotnettools.csharp");
    }

    [Test]
    public async Task Reads_semantic_token_contributions()
    {
        using var install = FakeInstall();
        var registry = VsCodeExtensions.Load(install.Path).SemanticTokens;
        await Assert.That(registry.Hierarchy("controlKeyword")).IsEquivalentTo(["controlKeyword", "keyword"]);
        await Assert.That(registry.Defaults.Any(d => d.Selector.Type == "keyword" && d.Selector.Language == "csharp")).IsTrue();
    }

    [Test]
    public async Task Missing_installation_is_fatal()
    {
        using var empty = new TempDirectory();
        var error = Assert.Throws<FatalException>(() => VsCodeExtensions.Load(empty.Path));
        await Assert.That(error.Message).Contains("VsCodePath");
    }

    private static VsCodeExtensions WithThemes(params ThemeContribution[] themes) =>
        new() { InstallPath = "VS Code", Themes = themes, SemanticTokens = new SemanticTokenRegistry() };

    [Test]
    public async Task Theme_is_found_by_settings_id_then_label_then_ignoring_case()
    {
        var dark = new ThemeContribution("Visual Studio Dark", "Dark (Visual Studio)", "vs-dark", "a.json", "vscode.theme-defaults");
        var light = new ThemeContribution(null, "Quiet Light", "vs", "b.json", "vscode.theme-quietlight");
        var extensions = WithThemes(dark, light);
        await Assert.That(extensions.FindTheme("Visual Studio Dark")).IsEqualTo(dark);
        await Assert.That(extensions.FindTheme("Dark (Visual Studio)")).IsEqualTo(dark);
        await Assert.That(extensions.FindTheme("quiet light")).IsEqualTo(light);
    }

    [Test]
    public async Task Unknown_theme_lists_the_installed_ones()
    {
        var extensions = WithThemes(new ThemeContribution("Dark Modern", "Dark Modern", "vs-dark", "a.json", "vscode.theme-defaults"));
        var error = Assert.Throws<FatalException>(() => extensions.FindTheme("Default Dark Modern"));
        await Assert.That(error.Message).Contains("\"Dark Modern\"");
        await Assert.That(error.Message).Contains("--themes");
    }
}
