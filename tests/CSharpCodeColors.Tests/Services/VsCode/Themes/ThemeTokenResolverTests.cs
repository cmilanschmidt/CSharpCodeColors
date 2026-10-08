using CSharpCodeColors.Models.Colors;
using CSharpCodeColors.Models.VsCode.Themes;
using CSharpCodeColors.Services.VsCode.Themes;
using static CSharpCodeColors.Tests.TestData.TestItems;

namespace CSharpCodeColors.Tests.Services.VsCode.Themes;

public class ThemeTokenResolverTests
{
    private static readonly RgbColor Foreground = Rgb(0xCCCCCC);

    private static TextMateRule Rule(string scope, int color) => new(ScopeSelector.Parse(scope), Rgb(color), SetsFontStyle: false);

    private static SemanticRule Semantic(string selector, int color) => new(SemanticSelector.Parse(selector), Rgb(color));

    private static ThemeTokenResolver Resolver(IReadOnlyList<TextMateRule> rules, IReadOnlyList<SemanticRule>? semantic = null, SemanticTokenRegistry? registry = null)
    {
        var contribution = new ThemeContribution("Test", "Test", "vs-dark", "test.json", "test.theme");
        var theme = new VsCodeTheme(contribution, Foreground, Rgb(0x1F1F1F), rules, semantic ?? [], SemanticHighlighting: true);
        return new ThemeTokenResolver(theme, registry ?? CSharpRegistry());
    }

    /// <summary>What the C# extension contributes for these tests.</summary>
    private static SemanticTokenRegistry CSharpRegistry()
    {
        var registry = new SemanticTokenRegistry();
        registry.AddType("controlKeyword", "keyword");
        registry.AddType("recordClass", "class");
        registry.AddScopes("keyword", "csharp", ["keyword.cs"]);
        registry.AddScopes("controlKeyword", "csharp", ["keyword.control.cs"]);
        return registry;
    }

    // Dark+'s keyword rules.
    private static readonly TextMateRule[] DarkPlusKeywords = [Rule("keyword", 0x569CD6), Rule("keyword.control", 0xC586C0)];

    [Test]
    public async Task Language_scopes_of_the_CSharp_extension_beat_VS_Codes_defaults()
    {
        // VS Code's default probe for "keyword" is keyword.control (purple); the C# extension's is keyword.cs (blue).
        var resolver = Resolver(DarkPlusKeywords);
        await Assert.That(resolver.ResolveSemantic("keyword:csharp")).IsEqualTo(Rgb(0x569CD6));
        await Assert.That(resolver.ResolveSemantic("controlKeyword:csharp")).IsEqualTo(Rgb(0xC586C0));
    }

    [Test]
    public async Task Theme_semantic_rule_beats_scope_defaults_and_applies_to_sub_types()
    {
        var resolver = Resolver([Rule("entity.name.type.class", 0x4EC9B0)], [Semantic("class", 0x112233)]);
        await Assert.That(resolver.ResolveSemantic("class:csharp")).IsEqualTo(Rgb(0x112233));
        await Assert.That(resolver.ResolveSemantic("recordClass:csharp")).IsEqualTo(Rgb(0x112233));
    }

    [Test]
    public async Task More_specific_semantic_rule_wins()
    {
        var resolver = Resolver([], [Semantic("variable", 0x111111), Semantic("variable.readonly", 0x222222), Semantic("*.static", 0x333333)]);
        await Assert.That(resolver.ResolveSemantic("variable:csharp")).IsEqualTo(Rgb(0x111111));
        await Assert.That(resolver.ResolveSemantic("variable.readonly:csharp")).IsEqualTo(Rgb(0x222222));
    }

    [Test]
    public async Task Mapping_falls_back_to_its_TextMate_scopes_then_the_editor_foreground()
    {
        var resolver = Resolver([Rule("variable", 0x9CDCFE), Rule("meta.embedded", 0xD4D4D4)]);

        var identifier = resolver.ResolveMapping([], ["source.cs variable.other.object.cs"]);
        await Assert.That((identifier.Foreground, identifier.Source)).IsEqualTo((Rgb(0x9CDCFE), "TextMate scopes"));

        // Most scopes of punctuation match no rule; one odd scope doesn't decide.
        var punctuation = resolver.ResolveMapping(["punctuation:csharp"],
            ["source.cs punctuation.curlybrace.open.cs", "source.cs punctuation.terminator.statement.cs", "source.cs meta.embedded.interpolation.cs"]);
        await Assert.That((punctuation.Foreground, punctuation.Source)).IsEqualTo((Foreground, "editor.foreground"));
    }

    [Test]
    public async Task Probe_ends_at_a_rule_that_only_sets_a_font_style()
    {
        // VS Code stops at the first probed scope list a rule matches, even one without a color.
        var resolver = Resolver([new TextMateRule(ScopeSelector.Parse("entity.name.type.class"), null, SetsFontStyle: true), Rule("support.class", 0x4EC9B0)]);
        await Assert.That(resolver.ResolveSemantic("class:csharp")).IsNull();
    }

    [Test]
    public async Task Semantic_color_comes_before_the_TextMate_scopes()
    {
        var resolver = Resolver([.. DarkPlusKeywords, Rule("storage.modifier", 0xFF0000)]);
        var keyword = resolver.ResolveMapping(["keyword:csharp"], ["source.cs storage.modifier.public.cs"]);
        await Assert.That((keyword.Foreground, keyword.Source)).IsEqualTo((Rgb(0x569CD6), "semantic keyword:csharp"));
    }

    [Test]
    public async Task Registry_scores_like_VS_Code()
    {
        var registry = CSharpRegistry();
        int Score(string selector, string type, params string[] modifiers) =>
            registry.Score(SemanticSelector.Parse(selector), type, modifiers, "csharp");

        await Assert.That(Score("keyword", "controlKeyword")).IsEqualTo(99);
        await Assert.That(Score("controlKeyword", "controlKeyword")).IsEqualTo(100);
        await Assert.That(Score("keyword:csharp", "keyword")).IsEqualTo(110);
        await Assert.That(Score("keyword:typescript", "keyword")).IsEqualTo(-1);
        await Assert.That(Score("*.static", "method", "static")).IsEqualTo(100);
        await Assert.That(Score("*.static", "method")).IsEqualTo(-1);
        await Assert.That(Score("method.static", "method", "static", "readonly")).IsEqualTo(200);
    }
}
