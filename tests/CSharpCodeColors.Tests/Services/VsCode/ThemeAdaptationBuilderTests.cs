using System.Text.Json.Nodes;
using CSharpCodeColors.Models.VsCode;
using CSharpCodeColors.Models.VsCode.Themes;
using CSharpCodeColors.Options;
using CSharpCodeColors.Services.Colors;
using CSharpCodeColors.Services.VsCode;
using CSharpCodeColors.Services.VsCode.Themes;
using CSharpCodeColors.Tests.TestData;
using TUnit.Assertions.Enums;
using static CSharpCodeColors.Tests.TestData.TestItems;

namespace CSharpCodeColors.Tests.Services.VsCode;

public class ThemeAdaptationBuilderTests
{
    private const string Mappings = """
        { "VisualStudioItem": "class name", "SemanticTokens": [ "class:csharp" ], "TextMateScopes": [ "source.cs entity.name.type.class.cs" ] },
        { "VisualStudioItem": "struct name", "SemanticTokens": [ "struct:csharp" ] },
        { "VisualStudioItem": "static symbol", "SemanticTokens": [ "*.static:csharp" ] }
        """;

    private static AppOptions Settings() => TestOptions.Parse($$"""{ "Mappings": [ {{Mappings}} ] }""");

    /// <summary>A theme like Dark Modern for types: classes and structs alike in teal, from the theme's TextMate rules.</summary>
    private static ThemeTokenResolver Theme()
    {
        var contribution = new ThemeContribution("Dark Modern", "Dark Modern", "vs-dark", "dark_modern.json", "vscode.theme-defaults");
        var rules = new[] { new TextMateRule(ScopeSelector.Parse("entity.name.type"), Rgb(0x4EC9B0), SetsFontStyle: true) };
        var theme = new VsCodeTheme(contribution, Rgb(0xCCCCCC), Rgb(0x1F1F1F), rules, [], SemanticHighlighting: true);
        return new ThemeTokenResolver(theme, new SemanticTokenRegistry());
    }

    private static readonly ColorResolver VisualStudio = Resolver(
        Explicit("class name", 0x4EC9B0), Explicit("struct name", 0x86C691), Default("static symbol"));

    [Test]
    public async Task Theme_colors_are_kept_and_Visual_Studio_distinctions_added()
    {
        var adaptation = ThemeAdaptationBuilder.Build(Settings(), VisualStudio, Theme());
        var colors = adaptation.Colors.ToDictionary(c => c.Item);
        await Assert.That(colors["class name"].Output).IsEqualTo(Rgb(0x4EC9B0));
        await Assert.That(colors["struct name"].Output).IsNotEqualTo(Rgb(0x4EC9B0));
        await Assert.That(adaptation.ThemeStyles["struct name"].Source).IsEqualTo("semantic struct:csharp");
        // Static symbol adds nothing in Visual Studio, so nothing is written for it.
        await Assert.That(adaptation.Styles["static symbol"]).IsNull();
    }

    [Test]
    public async Task Output_is_scoped_to_the_theme_and_leaves_font_styles_to_it()
    {
        var adaptation = ThemeAdaptationBuilder.Build(Settings(), VisualStudio, Theme());
        var json = JsonNode.Parse(VsCodeSettingsBuilder.Build(Settings(), VisualStudio, adaptation).Json)!;

        var semantic = json["editor.semanticTokenColorCustomizations"]!.AsObject();
        await Assert.That(semantic.Select(p => p.Key)).IsEquivalentTo(["[Dark Modern]"], CollectionOrdering.Matching);
        var classRule = semantic["[Dark Modern]"]!["rules"]!["class:csharp"]!.AsObject();
        await Assert.That(classRule.Select(p => p.Key)).IsEquivalentTo(["foreground"], CollectionOrdering.Matching);
        var textMate = json["editor.tokenColorCustomizations"]!["[Dark Modern]"]!["textMateRules"]![0]!["settings"]!.AsObject();
        await Assert.That(textMate.Select(p => p.Key)).IsEquivalentTo(["foreground"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Bold_static_symbols_stay_bold()
    {
        var visualStudio = Resolver(Explicit("class name", 0x4EC9B0), Explicit("struct name", 0x86C691), Default("static symbol", bold: true));
        var adaptation = ThemeAdaptationBuilder.Build(Settings(), visualStudio, Theme());
        await Assert.That(adaptation.Styles["static symbol"]).IsEqualTo(new TokenStyle(null, Bold: true, Additive: true));
    }
}
