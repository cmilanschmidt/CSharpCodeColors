using System.Text.Json;
using System.Text.RegularExpressions;

namespace CSharpCodeColors.Options;

/// <summary>The settings in appsettings.json, bound through Microsoft.Extensions.Configuration.</summary>
internal sealed class AppOptions
{
    public string OutputPath { get; set; } = "settings.json";

    /// <summary>VS Code theme name to scope the overrides to, e.g. "Default Dark Modern"; null for all themes.</summary>
    public string? VsCodeThemeScope { get; set; }

    public List<MappingOptions> Mappings { get; set; } = [];

    /// <summary>Applied after binding, before validation.</summary>
    internal static void Normalize(AppOptions settings)
    {
        foreach (var mapping in settings.Mappings)
        {
            var scopes = mapping.TextMateScopes;
            for (int i = 0; i < scopes.Count; i++)
                if (scopes[i] != null)
                    scopes[i] = NormalizeScope(scopes[i]);
        }
    }

    internal static string NormalizeScope(string scope) => Regex.Replace(scope.Trim(), " +", " ");

    // A semantic token selector with an explicit csharp language: type(.modifier)*:csharp, type may be '*'.
    private static readonly Regex SemanticSelector = new(@"^(\*|[A-Za-z][\w-]*)(\.[A-Za-z][\w-]*)*:csharp\z");

    // A TextMate scope selector made of plain scope names separated by spaces (descendant selectors only).
    private static readonly Regex ScopePath = new(@"^[A-Za-z0-9_][A-Za-z0-9_.-]*( +[A-Za-z0-9_][A-Za-z0-9_.-]*)*\z");

    /// <summary>VS Code treats modifiers as a set, so "a.x.y:csharp" and "a.y.x:csharp" are the same selector.</summary>
    private static string SemanticKey(string selector)
    {
        var parts = selector[..selector.IndexOf(':')].Split('.');
        return string.Join('.', parts.Take(1).Concat(parts.Skip(1).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))) + ":csharp";
    }

    /// <summary>A value as it would appear in JSON, so control characters stay visible in messages.</summary>
    internal static string Show(string? value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    internal List<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(OutputPath))
            errors.Add("OutputPath must not be empty.");
        if (VsCodeThemeScope != null && VsCodeThemeScope.Trim().Length == 0)
            errors.Add("VsCodeThemeScope must be null or a VS Code theme name.");
        else if (VsCodeThemeScope != null && VsCodeThemeScope != VsCodeThemeScope.Trim())
            errors.Add($"VsCodeThemeScope {Show(VsCodeThemeScope)} has leading or trailing spaces; VS Code matches theme names exactly.");
        if (VsCodeThemeScope != null && (VsCodeThemeScope.Contains('[') || VsCodeThemeScope.Contains(']')))
            errors.Add("VsCodeThemeScope is the plain theme name, without brackets (e.g. \"Default Dark Modern\").");
        if (Mappings.Count == 0)
        {
            errors.Add("Mappings must contain at least one mapping.");
            return errors;
        }

        var semanticOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        var scopeOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < Mappings.Count; i++)
        {
            var m = Mappings[i];
            string owner = $"Mappings[{i}] ({Show(m.VisualStudioItem)})";
            if (string.IsNullOrWhiteSpace(m.VisualStudioItem))
                errors.Add($"Mappings[{i}]: VisualStudioItem must not be empty.");
            if (m.SemanticTokens.Count == 0 && m.TextMateScopes.Count == 0)
                errors.Add($"{owner}: needs at least one entry in SemanticTokens or TextMateScopes.");

            foreach (var selector in m.SemanticTokens)
            {
                if (selector == null || !SemanticSelector.IsMatch(selector))
                    errors.Add($"{owner}: semantic token selector {Show(selector)} must look like \"type:csharp\" or \"type.modifier:csharp\" so it only applies to C#.");
                else if (!semanticOwners.TryAdd(SemanticKey(selector), owner))
                    errors.Add($"Semantic token selector {Show(selector)} is mapped twice: {semanticOwners[SemanticKey(selector)]} and {owner}.");
            }

            foreach (var scope in m.TextMateScopes)
            {
                string normalized = scope == null ? "" : NormalizeScope(scope);
                if (!IsCSharpScope(normalized))
                    errors.Add($"{owner}: TextMate scope {Show(scope)} must target C# only: either start with \"source.cs\" or end with a scope ending in \".cs\", using plain descendant selectors.");
                else if (!scopeOwners.TryAdd(normalized, owner))
                    errors.Add($"TextMate scope \"{normalized}\" is mapped twice: {scopeOwners[normalized]} and {owner}.");
            }
        }
        return errors;
    }

    internal static bool IsCSharpScope(string selector)
    {
        if (!ScopePath.IsMatch(selector))
            return false;
        var parts = selector.Split(' ');
        return parts[0] == "source.cs" || parts[^1].EndsWith(".cs", StringComparison.Ordinal);
    }
}
