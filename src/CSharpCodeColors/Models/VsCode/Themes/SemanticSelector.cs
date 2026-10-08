namespace CSharpCodeColors.Models.VsCode.Themes;

/// <summary>A semantic token selector: type (or "*"), modifiers and optional language, as in "variable.readonly:csharp".</summary>
internal sealed record SemanticSelector(string Type, IReadOnlyList<string> Modifiers, string? Language)
{
    public const string Wildcard = "*";

    public static SemanticSelector Parse(string selector)
    {
        string? language = null;
        int colon = selector.IndexOf(':');
        if (colon >= 0)
        {
            language = selector[(colon + 1)..];
            selector = selector[..colon];
        }
        var parts = selector.Split('.');
        return new SemanticSelector(parts[0], parts[1..], language);
    }
}
