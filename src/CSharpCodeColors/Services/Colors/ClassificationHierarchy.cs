using CSharpCodeColors.Constants;

namespace CSharpCodeColors.Services.Colors;

/// <summary>
/// What the Visual Studio editor does with classification formats, which DTE doesn't expose.
/// Source: the [BaseDefinition] attributes of the ClassificationTypeDefinition exports in
/// Microsoft.CodeAnalysis.EditorFeatures.dll and Microsoft.VisualStudio.Platform.VSEditor.dll of
/// Visual Studio 18.10. Names are the Fonts and Colors
/// item names DTE reports; classifications not listed here derive from "formal language", which
/// has no Fonts and Colors item, so they end at Plain Text.
/// </summary>
internal static class ClassificationHierarchy
{
    internal static readonly Dictionary<string, string> Bases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Number"] = "Literal",
        ["String"] = "Literal",
        ["keyword - control"] = "Keyword",
        ["operator - overloaded"] = "Operator",

        ["class name"] = "Identifier",
        ["constant name"] = "Identifier",
        ["delegate name"] = "Identifier",
        ["enum member name"] = "Identifier",
        ["enum name"] = "Identifier",
        ["event name"] = "Identifier",
        ["field name"] = "Identifier",
        ["interface name"] = "Identifier",
        ["label name"] = "Identifier",
        ["local name"] = "Identifier",
        ["method name"] = "Identifier",
        ["module name"] = "Identifier",
        ["namespace name"] = "Identifier",
        ["parameter name"] = "Identifier",
        ["property name"] = "Identifier",
        ["struct name"] = "Identifier",
        ["type parameter name"] = "Identifier",

        ["record class name"] = "class name",
        ["array name"] = "class name",
        ["record struct name"] = "struct name",
        ["pointer name"] = "struct name",
        ["function pointer name"] = "struct name",
        ["extension method name"] = "method name",

        ["json - array"] = "punctuation",
        ["json - object"] = "punctuation",
        ["json - punctuation"] = "punctuation",
        ["json - comment"] = "Comment",
        ["json - keyword"] = "Keyword",
        ["json - number"] = "Number",
        ["json - operator"] = "Operator",
        ["json - string"] = "String",
        // "json - text" derives from the "text" classification, which renders as Plain Text, not as the
        // "Text" item (measured: #DCDCDC, not Text's #DADADA, in VS Dark), so it is left to the default.
        ["json - property name"] = "method name",
        ["json - constructor name"] = "struct name",
    };

    /// <summary>
    /// Classifications Roslyn layers on top of another one (Roslyn's AdditiveClassificationTypeToTokenModifier).
    /// Only the properties set explicitly on them change what the editor shows.
    /// </summary>
    private static readonly HashSet<string> AdditiveItems = new(StringComparer.OrdinalIgnoreCase)
    {
        "static symbol",
        "reassigned variable",
        "obsolete symbol",
    };

    /// <summary>The item a Default color is inherited from, or null for Plain Text.</summary>
    public static string? BaseOf(string item) =>
        item.Equals(ClassificationNames.PlainText, StringComparison.OrdinalIgnoreCase) ? null : Bases.GetValueOrDefault(item, ClassificationNames.PlainText);

    public static bool IsAdditive(string item) => AdditiveItems.Contains(item);
}
