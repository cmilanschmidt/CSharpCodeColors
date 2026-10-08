using CSharpCodeColors.Exceptions;
using CSharpCodeColors.Models.VsCode.Themes;
using CSharpCodeColors.Services.VsCode.Themes;
using CSharpCodeColors.Tests.TestData;
using static CSharpCodeColors.Tests.TestData.TestItems;

namespace CSharpCodeColors.Tests.Services.VsCode.Themes;

public class ThemeFileReaderTests
{
    private static VsCodeTheme Read(string path, string uiTheme = "vs-dark") =>
        ThemeFileReader.Read(new ThemeContribution("Test", "Test", uiTheme, path, "test.theme"));

    [Test]
    public async Task Included_theme_is_read_first_and_overridden()
    {
        using var dir = new TempDirectory();
        dir.Write("base.json", """
            {
              "colors": { "editor.background": "#101010", "editor.foreground": "#AAAAAA", "editor.lineHighlightBackground": "#202020" },
              "tokenColors": [ { "scope": "keyword", "settings": { "foreground": "#0000FF" } } ],
              "semanticTokenColors": { "property": "#123456" }
            }
            """);
        string path = dir.Write("theme.json", """
            // Comments and trailing commas, as VS Code allows.
            {
              "include": "./base.json",
              "colors": { "editor.foreground": "#BBBBBB", },
              "tokenColors": [ { "scope": ["comment", "string"], "settings": { "foreground": "#00FF00", "fontStyle": "italic" } }, ],
              "semanticHighlighting": true,
            }
            """);

        var theme = Read(path);
        await Assert.That(theme.Background).IsEqualTo(Rgb(0x101010));
        await Assert.That(theme.Foreground).IsEqualTo(Rgb(0xBBBBBB));
        await Assert.That(theme.TextMateRules.Count).IsEqualTo(2);
        await Assert.That(theme.TextMateRules[0].Foreground).IsEqualTo(Rgb(0x0000FF));
        await Assert.That(theme.TextMateRules[1].SetsFontStyle).IsTrue();
        await Assert.That(theme.SemanticRules.Single().Foreground).IsEqualTo(Rgb(0x123456));
        await Assert.That(theme.SemanticHighlighting).IsTrue();
        await Assert.That(theme.Palette).Contains(Rgb(0x00FF00));
    }

    [Test]
    public async Task Missing_editor_colors_get_VS_Code_defaults()
    {
        using var dir = new TempDirectory();
        var dark = Read(dir.Write("dark.json", "{}"), "vs-dark");
        var light = Read(dir.Write("light.json", "{}"), "vs");
        await Assert.That((dark.Background, dark.Foreground)).IsEqualTo((Rgb(0x1E1E1E), Rgb(0xBBBBBB)));
        await Assert.That((light.Background, light.Foreground)).IsEqualTo((Rgb(0xFFFFFF), Rgb(0x333333)));
    }

    [Test]
    public async Task Default_removes_an_included_color()
    {
        using var dir = new TempDirectory();
        dir.Write("base.json", """{ "colors": { "editor.foreground": "#ABCDEF" } }""");
        var theme = Read(dir.Write("theme.json", """{ "include": "base.json", "colors": { "editor.foreground": "default" } }"""));
        await Assert.That(theme.Foreground).IsEqualTo(Rgb(0xBBBBBB));
    }

    [Test]
    public async Task Transparent_token_colors_are_blended_over_the_background()
    {
        using var dir = new TempDirectory();
        var theme = Read(dir.Write("theme.json", """
            { "colors": { "editor.background": "#000000" }, "tokenColors": [ { "scope": "comment", "settings": { "foreground": "#FFFFFF80" } } ] }
            """));
        await Assert.That(theme.TextMateRules[0].Foreground).IsEqualTo(Rgb(0x808080));
    }

    [Test]
    public async Task Rules_without_scope_or_style_are_ignored()
    {
        using var dir = new TempDirectory();
        var theme = Read(dir.Write("theme.json", """
            { "tokenColors": [ { "settings": { "foreground": "#FFFFFF" } }, { "scope": "comment", "settings": { "background": "#FF0000" } } ] }
            """));
        await Assert.That(theme.TextMateRules).IsEmpty();
    }

    [Test]
    public async Task Semantic_rule_colors_can_be_strings_or_objects()
    {
        using var dir = new TempDirectory();
        var theme = Read(dir.Write("theme.json", """
            { "semanticTokenColors": {
                "class": "#111111",
                "parameter": { "italic": true },
                "property.readonly:csharp": { "foreground": "#222222", "fontStyle": "bold" }
            } }
            """));
        // Rules that only set a font style don't affect colors and are left out.
        var rules = theme.SemanticRules.ToDictionary(r => r.Selector.Type);
        await Assert.That(rules.Keys).IsEquivalentTo(["class", "property"]);
        await Assert.That(rules["class"].Foreground).IsEqualTo(Rgb(0x111111));
        await Assert.That(rules["property"].Foreground).IsEqualTo(Rgb(0x222222));
        await Assert.That(rules["property"].Selector.Modifiers).IsEquivalentTo(["readonly"]);
        await Assert.That(rules["property"].Selector.Language).IsEqualTo("csharp");
    }

    [Test]
    public async Task TextMate_theme_property_list_is_read_with_its_global_colors()
    {
        using var dir = new TempDirectory();
        dir.Write("old.tmTheme", """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
              <key>name</key><string>Old</string>
              <key>settings</key>
              <array>
                <dict><key>settings</key><dict><key>background</key><string>#272822</string><key>foreground</key><string>#F8F8F2</string></dict></dict>
                <dict><key>scope</key><string>keyword</string><key>settings</key><dict><key>foreground</key><string>#F92672</string></dict></dict>
              </array>
            </dict>
            </plist>
            """);
        var theme = Read(dir.Write("theme.json", """{ "tokenColors": "./old.tmTheme" }"""));
        await Assert.That((theme.Background, theme.Foreground)).IsEqualTo((Rgb(0x272822), Rgb(0xF8F8F2)));
        await Assert.That(theme.TextMateRules.Single().Foreground).IsEqualTo(Rgb(0xF92672));
    }

    [Test]
    public async Task Broken_theme_is_fatal_and_names_the_file()
    {
        using var dir = new TempDirectory();
        string path = dir.Write("theme.json", "{ not json");
        var error = Assert.Throws<FatalException>(() => Read(path));
        await Assert.That(error.Message).Contains("theme.json");
    }
}
