namespace CSharpCodeColors.Options;

internal sealed class MappingOptions
{
    /// <summary>The Fonts and Colors item name exactly as DTE reports it (see --list).</summary>
    public string VisualStudioItem { get; set; } = "";

    public List<string> SemanticTokens { get; set; } = [];

    public List<string> TextMateScopes { get; set; } = [];
}
