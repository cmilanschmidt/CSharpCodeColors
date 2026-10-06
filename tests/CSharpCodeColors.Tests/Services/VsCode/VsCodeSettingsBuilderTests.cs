using System.Text.Json.Nodes;
using CSharpCodeColors.Models.Colors;
using CSharpCodeColors.Models.VsCode;
using CSharpCodeColors.Options;
using CSharpCodeColors.Services.VsCode;
using CSharpCodeColors.Tests.TestData;
using TUnit.Assertions.Enums;
using static CSharpCodeColors.Tests.TestData.TestItems;

namespace CSharpCodeColors.Tests.Services.VsCode;

public class VsCodeSettingsBuilderTests
{
    private static AppOptions Settings(string mappings, string? theme = null) => TestOptions.Parse(
        $$"""{ "VsCodeThemeScope": {{(theme == null ? "null" : $"\"{theme}\"")}}, "Mappings": [ {{mappings}} ] }""");

    private static (BuildResult Result, JsonObject Json) Build(AppOptions settings, params VsColorItem[] items)
    {
        var result = VsCodeSettingsBuilder.Build(settings, Resolver(items));
        return (result, JsonNode.Parse(result.Json)!.AsObject());
    }

    private const string ClassMapping = """{ "VisualStudioItem": "class name", "SemanticTokens": [ "class:csharp" ], "TextMateScopes": [ "entity.name.type.class.cs" ] }""";

    [Test]
    public async Task Output_contains_only_the_two_customization_keys()
    {
        var (_, json) = Build(Settings(ClassMapping), Explicit("class name", 0x4EC9B0));
        await Assert.That(json.Select(p => p.Key)).IsEquivalentTo(["editor.semanticTokenColorCustomizations", "editor.tokenColorCustomizations"], CollectionOrdering.Matching);
        var semantic = json["editor.semanticTokenColorCustomizations"]!.AsObject();
        await Assert.That(semantic.Select(p => p.Key)).IsEquivalentTo(["enabled", "rules"], CollectionOrdering.Matching);
        await Assert.That(semantic["enabled"]!.GetValue<bool>()).IsTrue();
        await Assert.That(json["editor.tokenColorCustomizations"]!.AsObject().Select(p => p.Key)).IsEquivalentTo(["textMateRules"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Semantic_and_TextMate_rules_carry_color_and_bold()
    {
        var (result, json) = Build(Settings(ClassMapping), Explicit("class name", 0x4EC9B0, bold: true));
        var rule = json["editor.semanticTokenColorCustomizations"]!["rules"]!["class:csharp"]!;
        await Assert.That(rule["foreground"]!.GetValue<string>()).IsEqualTo("#4EC9B0");
        await Assert.That(rule["fontStyle"]!.GetValue<string>()).IsEqualTo("bold");
        var textMate = json["editor.tokenColorCustomizations"]!["textMateRules"]![0]!;
        await Assert.That(textMate["scope"]![0]!.GetValue<string>()).IsEqualTo("entity.name.type.class.cs");
        await Assert.That(textMate["settings"]!["foreground"]!.GetValue<string>()).IsEqualTo("#4EC9B0");
        await Assert.That(textMate["settings"]!["fontStyle"]!.GetValue<string>()).IsEqualTo("bold");
        await Assert.That((result.SemanticRuleCount, result.TextMateRuleCount)).IsEqualTo((1, 1));
    }

    [Test]
    public async Task Not_bold_resets_font_style_because_VS_never_shows_italics()
    {
        var (_, json) = Build(Settings(ClassMapping), Explicit("class name", 0x4EC9B0));
        await Assert.That(json["editor.semanticTokenColorCustomizations"]!["rules"]!["class:csharp"]!["fontStyle"]!.GetValue<string>()).IsEqualTo("");
    }

    [Test]
    public async Task Default_color_is_written_as_the_inherited_color()
    {
        var (_, json) = Build(Settings("""{ "VisualStudioItem": "record class name", "SemanticTokens": [ "recordClass:csharp" ] }"""),
            Explicit("class name", 0x4EC9B0), Default("record class name"));
        await Assert.That(json["editor.semanticTokenColorCustomizations"]!["rules"]!["recordClass:csharp"]!["foreground"]!.GetValue<string>()).IsEqualTo("#4EC9B0");
    }

    [Test]
    public async Task Theme_scope_wraps_both_keys()
    {
        var (_, json) = Build(Settings(ClassMapping, "Default Dark Modern"), Explicit("class name", 0x4EC9B0));
        var semantic = json["editor.semanticTokenColorCustomizations"]!.AsObject();
        await Assert.That(semantic.Select(p => p.Key)).IsEquivalentTo(["[Default Dark Modern]"], CollectionOrdering.Matching);
        await Assert.That(semantic["[Default Dark Modern]"]!["enabled"]!.GetValue<bool>()).IsTrue();
        await Assert.That(semantic["[Default Dark Modern]"]!["rules"]!["class:csharp"]).IsNotNull();
        await Assert.That(json["editor.tokenColorCustomizations"]!["[Default Dark Modern]"]!["textMateRules"]).IsNotNull();
    }

    private const string StaticMapping = """{ "VisualStudioItem": "static symbol", "SemanticTokens": [ "*.static:csharp" ] }""";

    [Test]
    public async Task Additive_item_with_nothing_set_emits_no_rule()
    {
        var (result, json) = Build(Settings(StaticMapping), Default("static symbol"));
        await Assert.That(json["editor.semanticTokenColorCustomizations"]!["rules"]!.AsObject().Count).IsEqualTo(0);
        await Assert.That(result.Mapped).IsEquivalentTo(["static symbol"], CollectionOrdering.Matching);
        await Assert.That(result.NothingToEmit).IsEquivalentTo(["static symbol"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Additive_rules_are_written_last_whatever_the_mapping_order()
    {
        // VS Code breaks equal scores ("*.static:csharp" vs "class:csharp") by order: the later rule wins.
        var (_, json) = Build(Settings(StaticMapping + ", " + ClassMapping), Explicit("static symbol", 0xFF00FF), Explicit("class name", 0x4EC9B0));
        var keys = json["editor.semanticTokenColorCustomizations"]!["rules"]!.AsObject().Select(p => p.Key).ToList();
        await Assert.That(keys).IsEquivalentTo(["class:csharp", "*.static:csharp"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Item_used_by_two_mappings_is_counted_once()
    {
        var (result, _) = Build(Settings(ClassMapping + """, { "VisualStudioItem": "class name", "SemanticTokens": [ "recordClass:csharp" ] }"""),
            WithBackground("class name", 0x4EC9B0, 0x333300));
        await Assert.That(result.Mapped).IsEquivalentTo(["class name"], CollectionOrdering.Matching);
        await Assert.That(result.BackgroundsSkipped).HasSingleItem();
    }

    [Test]
    public async Task Additive_item_emits_only_what_is_set()
    {
        var (_, json) = Build(Settings(StaticMapping), Default("static symbol", bold: true));
        var rule = json["editor.semanticTokenColorCustomizations"]!["rules"]!["*.static:csharp"]!.AsObject();
        await Assert.That(rule.Select(p => p.Key)).IsEquivalentTo(["bold"], CollectionOrdering.Matching);

        (_, json) = Build(Settings(StaticMapping), Explicit("static symbol", 0xFF0000));
        rule = json["editor.semanticTokenColorCustomizations"]!["rules"]!["*.static:csharp"]!.AsObject();
        await Assert.That(rule.Select(p => p.Key)).IsEquivalentTo(["foreground"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Background_is_skipped_with_a_warning()
    {
        var (result, json) = Build(Settings(ClassMapping), WithBackground("class name", 0x4EC9B0, 0x333300));
        await Assert.That(result.BackgroundsSkipped).IsEquivalentTo([("class name", Rgb(0x333300))], CollectionOrdering.Matching);
        await Assert.That(result.Warnings).Contains(w => w.Contains("background"));
        var rule = json["editor.semanticTokenColorCustomizations"]!["rules"]!["class:csharp"]!.AsObject();
        await Assert.That(rule.Select(p => p.Key)).DoesNotContain(key => key.Contains("background", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task Background_equal_to_the_editor_background_is_not_reported()
    {
        var (result, _) = Build(Settings(ClassMapping), WithBackground("class name", 0x4EC9B0, 0x1E1E1E));
        await Assert.That(result.BackgroundsSkipped).IsEmpty();
    }

    [Test]
    public async Task Inherited_background_is_reported()
    {
        var (result, _) = Build(Settings("""{ "VisualStudioItem": "record class name", "SemanticTokens": [ "recordClass:csharp" ] }"""),
            WithBackground("class name", 0x4EC9B0, 0x333300), Default("record class name"));
        await Assert.That(result.BackgroundsSkipped).HasSingleItem();
    }

    [Test]
    public async Task Additive_background_only_is_reported()
    {
        var item = new VsColorItem("static symbol", VsColor.Default(PlainForeground), VsColor.Explicit(Rgb(0x333300)), false);
        var (result, _) = Build(Settings(StaticMapping), item);
        await Assert.That(result.BackgroundsSkipped).HasSingleItem();
    }

    [Test]
    public async Task Missing_item_is_warned_and_skipped()
    {
        var (result, json) = Build(Settings(ClassMapping + """, { "VisualStudioItem": "no such item", "SemanticTokens": [ "x:csharp" ] }"""),
            Explicit("class name", 0x4EC9B0));
        await Assert.That(result.Missing).IsEquivalentTo(["no such item"], CollectionOrdering.Matching);
        await Assert.That(result.Mapped).IsEquivalentTo(["class name"], CollectionOrdering.Matching);
        await Assert.That(json["editor.semanticTokenColorCustomizations"]!["rules"]!["x:csharp"]).IsNull();
        await Assert.That(result.Warnings).Contains(w => w.Contains("no such item"));
    }

    [Test]
    public async Task Item_names_match_case_insensitively_and_report_the_vs_name()
    {
        var (result, _) = Build(Settings("""{ "VisualStudioItem": "CLASS NAME", "SemanticTokens": [ "class:csharp" ] }"""), Explicit("class name", 0x4EC9B0));
        await Assert.That(result.Mapped).IsEquivalentTo(["class name"], CollectionOrdering.Matching);
    }
}
