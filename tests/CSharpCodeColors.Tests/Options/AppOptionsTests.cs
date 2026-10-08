using CSharpCodeColors.Exceptions;
using CSharpCodeColors.Options;
using CSharpCodeColors.Tests.TestData;

namespace CSharpCodeColors.Tests.Options;

public class AppOptionsTests
{
    private static AppOptions Parse(string json) => TestOptions.Parse(json);

    private static string Error(string json) => Assert.Throws<FatalException>(() => Parse(json)).Message;

    [Test]
    public async Task Parses_minimal_settings_with_comments_and_trailing_commas()
    {
        var settings = Parse("""
            // comment
            { "Mappings": [ { "VisualStudioItem": "class name", "SemanticTokens": [ "class:csharp" ], }, ], }
            """);
        await Assert.That(settings.OutputPath).IsEqualTo("settings.json");
        await Assert.That(settings.VsCodeThemeScope).IsNull();
        await Assert.That(settings.Mappings[0].TextMateScopes).IsEmpty();
    }

    [Test]
    public async Task Null_lists_are_empty()
    {
        var settings = Parse("""{ "Mappings": [ { "VisualStudioItem": "x", "SemanticTokens": null, "TextMateScopes": [ "source.cs" ] } ] }""");
        await Assert.That(settings.Mappings[0].SemanticTokens).IsEmpty();
    }

    [Test]
    public async Task Malformed_json_is_reported() =>
        await Assert.That(Error("{\n  \"Mappings\": [\n    { \"VisualStudioItem\": \"x\" \"SemanticTokens\": [] }\n  ]\n}"))
            .StartsWith("Can't load appsettings.jsonc:");

    [Test]
    public async Task Unknown_property_is_reported() =>
        await Assert.That(Error("""{ "Mappings": [ { "VisualStudioItems": "x" } ] }""")).Contains("'VisualStudioItems'");

    [Test]
    public async Task Wrong_type_is_reported() => await Assert.That(Error("""{ "Mappings": "x" }""")).Contains("'Mappings'");

    [Test]
    public async Task Empty_file_is_reported() => await Assert.That(Error("")).Contains("appsettings.jsonc");

    [Test]
    public async Task Json_null_is_reported() => await Assert.That(Error("null")).Contains("must be an object");

    [Test]
    public async Task Empty_mappings_are_rejected() => await Assert.That(Error("""{ "Mappings": [] }""")).Contains("at least one mapping");

    [Test]
    public async Task Mapping_without_selectors_is_rejected() =>
        await Assert.That(Error("""{ "Mappings": [ { "VisualStudioItem": "x" } ] }""")).Contains("needs at least one entry");

    [Test]
    public async Task Empty_item_name_is_rejected() =>
        await Assert.That(Error("""{ "Mappings": [ { "SemanticTokens": [ "class:csharp" ] } ] }""")).Contains("VisualStudioItem must not be empty");

    [Test]
    [Arguments("class")]
    [Arguments("class:typescript")]
    [Arguments("class:csharp ")]
    [Arguments("*:csharp,class")]
    [Arguments("")]
    public async Task Semantic_selector_must_be_csharp_only(string selector) =>
        await Assert.That(Error($$"""{ "Mappings": [ { "VisualStudioItem": "x", "SemanticTokens": [ "{{selector}}" ] } ] }""")).Contains("only applies to C#");

    [Test]
    [Arguments("class:csharp")]
    [Arguments("*.static:csharp")]
    [Arguments("variable.readonly.static:csharp")]
    public async Task Valid_semantic_selectors_are_accepted(string selector)
    {
        var settings = Parse($$"""{ "Mappings": [ { "VisualStudioItem": "x", "SemanticTokens": [ "{{selector}}" ] } ] }""");
        await Assert.That(settings.Mappings[0].SemanticTokens[0]).IsEqualTo(selector);
    }

    [Test]
    [Arguments("keyword.control")]
    [Arguments("source.js")]
    [Arguments("source.css")]
    [Arguments("comment.block.cs, comment")]
    [Arguments("comment.block.cs | comment")]
    [Arguments("source.cs - comment")]
    [Arguments("source.csharp keyword")]
    [Arguments("")]
    public async Task TextMate_scope_must_be_csharp_only(string scope) =>
        await Assert.That(Error($$"""{ "Mappings": [ { "VisualStudioItem": "x", "TextMateScopes": [ "{{scope}}" ] } ] }""")).Contains("must target C# only");

    [Test]
    [Arguments("source.cs")]
    [Arguments("source.cs keyword.type")]
    [Arguments("comment.block.documentation.cs punctuation.definition.tag.cs")]
    [Arguments("entity.name.type.class.cs")]
    public async Task Valid_TextMate_scopes_are_accepted(string scope)
    {
        var settings = Parse($$"""{ "Mappings": [ { "VisualStudioItem": "x", "TextMateScopes": [ "{{scope}}" ] } ] }""");
        await Assert.That(settings.Mappings[0].TextMateScopes[0]).IsEqualTo(scope);
    }

    [Test]
    public async Task Duplicate_semantic_selector_names_both_mappings()
    {
        string message = Error("""
            { "Mappings": [
                { "VisualStudioItem": "class name", "SemanticTokens": [ "class:csharp" ] },
                { "VisualStudioItem": "record class name", "SemanticTokens": [ "class:csharp" ] } ] }
            """);
        await Assert.That(message).Contains("Mappings[0] (\"class name\")");
        await Assert.That(message).Contains("Mappings[1] (\"record class name\")");
    }

    [Test]
    public async Task Duplicate_TextMate_scope_names_both_mappings_ignoring_extra_spaces()
    {
        string message = Error("""
            { "Mappings": [
                { "VisualStudioItem": "a", "TextMateScopes": [ "source.cs  keyword" ] },
                { "VisualStudioItem": "b", "TextMateScopes": [ "source.cs keyword" ] } ] }
            """);
        await Assert.That(message).Contains("\"a\"");
        await Assert.That(message).Contains("\"b\"");
    }

    [Test]
    public async Task All_validation_errors_are_reported_together()
    {
        string message = Error("""{ "OutputPath": "", "Mappings": [ { "VisualStudioItem": "x", "SemanticTokens": [ "class" ] } ] }""");
        await Assert.That(message).Contains("OutputPath");
        await Assert.That(message).Contains("only applies to C#");
    }

    [Test]
    [Arguments("""{ "OutputPath": "a.json", "OutputPath": "b.json", "Mappings": [ { "VisualStudioItem": "a", "SemanticTokens": [ "class:csharp" ] } ] }""", "OutputPath")]
    [Arguments("""{ "Mappings": [ { "VisualStudioItem": "a", "SemanticTokens": [ "class:csharp" ] } ], "Mappings": [ { "VisualStudioItem": "b", "SemanticTokens": [ "struct:csharp" ] } ] }""", "Mappings")]
    public async Task Duplicate_json_keys_are_rejected(string json, string key) =>
        await Assert.That(Error(json)).Contains($"duplicate key '{key}");

    [Test]
    public async Task Duplicate_property_in_a_mapping_is_rejected() =>
        await Assert.That(Error("""{ "Mappings": [ { "VisualStudioItem": "a", "VisualStudioItem": "b", "SemanticTokens": [ "class:csharp" ] } ] }"""))
            .Contains("VisualStudioItem");

    [Test]
    public async Task Modifier_order_does_not_hide_a_duplicate()
    {
        string message = Error("""
            { "Mappings": [
                { "VisualStudioItem": "a", "SemanticTokens": [ "variable.static.readonly:csharp" ] },
                { "VisualStudioItem": "b", "SemanticTokens": [ "variable.readonly.static:csharp" ] } ] }
            """);
        await Assert.That(message).Contains("mapped twice");
    }

    [Test]
    public async Task Repeated_modifier_does_not_hide_a_duplicate() =>
        await Assert.That(Error("""
            { "Mappings": [
                { "VisualStudioItem": "a", "SemanticTokens": [ "class.static:csharp" ] },
                { "VisualStudioItem": "b", "SemanticTokens": [ "class.static.static:csharp" ] } ] }
            """)).Contains("mapped twice");

    [Test]
    public async Task Trailing_newline_in_selector_is_rejected_and_shown_escaped()
    {
        string message = Error("""{ "Mappings": [ { "VisualStudioItem": "x", "SemanticTokens": [ "class:csharp\n" ] } ] }""");
        await Assert.That(message).Contains("only applies to C#");
        await Assert.That(message).Contains("\"class:csharp\\n\"");
    }

    [Test]
    public async Task Trailing_newline_in_scope_is_normalized_away()
    {
        var settings = Parse("""{ "Mappings": [ { "VisualStudioItem": "x", "TextMateScopes": [ "  source.cs    keyword.cs\n" ] } ] }""");
        await Assert.That(settings.Mappings[0].TextMateScopes[0]).IsEqualTo("source.cs keyword.cs");
    }

    [Test]
    public async Task Theme_scope_with_padding_is_rejected() =>
        await Assert.That(Error("""{ "VsCodeThemeScope": " Default Dark Modern ", "Mappings": [ { "VisualStudioItem": "x", "SemanticTokens": [ "class:csharp" ] } ] }"""))
            .Contains("leading or trailing spaces");

    [Test]
    [Arguments("")]
    [Arguments("[Default Dark Modern]")]
    public async Task Bad_theme_scope_is_rejected(string theme) =>
        await Assert.That(Error($$"""{ "VsCodeThemeScope": "{{theme}}", "Mappings": [ { "VisualStudioItem": "x", "SemanticTokens": [ "class:csharp" ] } ] }"""))
            .Contains("VsCodeThemeScope");

    [Test]
    public async Task Adapt_settings_are_read()
    {
        var settings = Parse("""{ "AdaptToVsCodeTheme": "Dark Modern", "VsCodePath": "D:/Tools/VS Code", "Mappings": [ { "VisualStudioItem": "x", "SemanticTokens": [ "class:csharp" ] } ] }""");
        await Assert.That((settings.AdaptToVsCodeTheme, settings.VsCodePath)).IsEqualTo(("Dark Modern", "D:/Tools/VS Code"));
    }

    [Test]
    [Arguments("\"\"")]
    [Arguments("\" Dark Modern\"")]
    [Arguments("\"[Dark Modern]\"")]
    public async Task Bad_adapt_theme_is_rejected(string theme) =>
        await Assert.That(Error($$"""{ "AdaptToVsCodeTheme": {{theme}}, "Mappings": [ { "VisualStudioItem": "x", "SemanticTokens": [ "class:csharp" ] } ] }"""))
            .Contains("AdaptToVsCodeTheme");

    [Test]
    public async Task Scope_and_adapt_theme_together_are_rejected() =>
        await Assert.That(Error("""{ "VsCodeThemeScope": "Dark Modern", "AdaptToVsCodeTheme": "Dark Modern", "Mappings": [ { "VisualStudioItem": "x", "SemanticTokens": [ "class:csharp" ] } ] }"""))
            .Contains("not both");

    [Test]
    public async Task Empty_VS_Code_path_is_rejected() =>
        await Assert.That(Error("""{ "VsCodePath": " ", "Mappings": [ { "VisualStudioItem": "x", "SemanticTokens": [ "class:csharp" ] } ] }"""))
            .Contains("VsCodePath");
}
