using CSharpCodeColors.Models.VsCode.Themes;

namespace CSharpCodeColors.Services.VsCode.Themes;

/// <summary>
/// What VS Code knows about semantic token types: their super types, and the TextMate scopes it probes in the
/// theme when the theme has no semantic rule for a token. Built-in entries come from VS Code's
/// tokenClassificationRegistry.ts (checked against VS Code 1.140); extensions add theirs through
/// "semanticTokenTypes" and "semanticTokenScopes" (the C# extension maps "keyword" to "keyword.cs", for example).
/// </summary>
internal sealed class SemanticTokenRegistry
{
    private readonly Dictionary<string, string> _superTypes = new(StringComparer.Ordinal) { ["member"] = "method" };
    private readonly List<(SemanticSelector Selector, IReadOnlyList<IReadOnlyList<string>> ScopesToProbe)> _defaults = [];

    public SemanticTokenRegistry()
    {
        Default("comment", "comment");
        Default("string", "string");
        Default("keyword", "keyword.control");
        Default("number", "constant.numeric");
        Default("regexp", "constant.regexp");
        Default("operator", "keyword.operator");
        Default("namespace", "entity.name.namespace");
        Default("type", "entity.name.type", "support.type");
        Default("struct", "entity.name.type.struct");
        Default("class", "entity.name.type.class", "support.class");
        Default("interface", "entity.name.type.interface");
        Default("enum", "entity.name.type.enum");
        Default("typeParameter", "entity.name.type.parameter");
        Default("function", "entity.name.function", "support.function");
        Default("method", "entity.name.function.member", "support.function");
        Default("macro", "entity.name.function.preprocessor");
        Default("variable", "variable.other.readwrite", "entity.name.variable");
        Default("parameter", "variable.parameter");
        Default("property", "variable.other.property");
        Default("enumMember", "variable.other.enummember");
        Default("event", "variable.other.event");
        Default("decorator", "entity.name.decorator", "entity.name.function");
        Default("variable.readonly", "variable.other.constant");
        Default("property.readonly", "variable.other.constant.property");
        Default("type.defaultLibrary", "support.type");
        Default("class.defaultLibrary", "support.class");
        Default("interface.defaultLibrary", "support.class");
        Default("variable.defaultLibrary", "support.variable", "support.other.variable");
        Default("variable.defaultLibrary.readonly", "support.constant");
        Default("property.defaultLibrary", "support.variable.property");
        Default("property.defaultLibrary.readonly", "support.constant.property");
        Default("function.defaultLibrary", "support.function");
        Default("member.defaultLibrary", "support.function");

        void Default(string selector, params string[] scopes) =>
            _defaults.Add((SemanticSelector.Parse(selector), scopes.Select(s => (IReadOnlyList<string>)[s]).ToList()));
    }

    /// <summary>A "semanticTokenTypes" entry of an extension.</summary>
    public void AddType(string id, string? superType)
    {
        if (!string.IsNullOrEmpty(superType))
            _superTypes[id] = superType;
    }

    /// <summary>A "semanticTokenScopes" entry of an extension: each scope string may list several scopes separated by spaces.</summary>
    public void AddScopes(string selector, string? language, IEnumerable<string> scopes)
    {
        var parsed = SemanticSelector.Parse(selector);
        if (parsed.Language == null && language != null)
            parsed = parsed with { Language = language };
        _defaults.Add((parsed, scopes.Select(s => (IReadOnlyList<string>)s.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList()));
    }

    /// <summary>Default rules in registration order: later rules win ties.</summary>
    public IReadOnlyList<(SemanticSelector Selector, IReadOnlyList<IReadOnlyList<string>> ScopesToProbe)> Defaults => _defaults;

    /// <summary>The type followed by its super types, nearest first.</summary>
    public IReadOnlyList<string> Hierarchy(string type)
    {
        var result = new List<string> { type };
        while (_superTypes.TryGetValue(result[^1], out var super) && !result.Contains(super))
            result.Add(super);
        return result;
    }

    /// <summary>
    /// How well <paramref name="selector"/> matches a token, as VS Code's TokenSelector.match scores it, or -1:
    /// +10 for a matching language, +100 minus the super type distance for a matching type, +100 per modifier.
    /// </summary>
    public int Score(SemanticSelector selector, string type, IReadOnlyList<string> modifiers, string language)
    {
        int score = 0;
        if (selector.Language != null)
        {
            if (selector.Language != language)
                return -1;
            score += 10;
        }
        if (selector.Type != SemanticSelector.Wildcard)
        {
            int level = Hierarchy(type).ToList().IndexOf(selector.Type);
            if (level == -1)
                return -1;
            score += 100 - level;
        }
        foreach (var modifier in selector.Modifiers)
            if (!modifiers.Contains(modifier))
                return -1;
        return score + selector.Modifiers.Count * 100;
    }
}
