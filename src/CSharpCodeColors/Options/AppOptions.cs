using System.Text.Json;
using System.Text.RegularExpressions;

namespace CSharpCodeColors.Options;

/// <summary>The settings in appsettings.jsonc, bound through Microsoft.Extensions.Configuration.</summary>
internal sealed class AppOptions
{
    public string OutputPath { get; set; } = "settings.json";

    /// <summary>VS Code theme name to scope the overrides to, e.g. "Dark Modern"; null for all themes. Must be null with AdaptToVsCodeTheme, which scopes to the adapted theme.</summary>
    public string? VsCodeThemeScope { get; set; }

    /// <summary>
    /// A VS Code theme to adapt the Visual Studio colors to, e.g. "Dark Modern": the output uses that theme's
    /// colors and only adds colors for the distinctions Visual Studio makes and the theme doesn't. Null writes
    /// Visual Studio's own colors.
    /// </summary>
    public string? AdaptToVsCodeTheme { get; set; }

    /// <summary>The VS Code installation folder (the one with Code.exe); null to look in the default locations.</summary>
    public string? VsCodePath { get; set; }

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
        ValidateThemeName(nameof(VsCodeThemeScope), VsCodeThemeScope, errors);
        ValidateThemeName(nameof(AdaptToVsCodeTheme), AdaptToVsCodeTheme, errors);
        if (VsCodeThemeScope != null && AdaptToVsCodeTheme != null)
            errors.Add("Set VsCodeThemeScope or AdaptToVsCodeTheme, not both: adapted colors fit only the theme they were adapted to, so they are always scoped to it.");
        if (VsCodePath != null && VsCodePath.Trim().Length == 0)
            errors.Add("VsCodePath must be null or the VS Code installation folder.");
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

    private static void ValidateThemeName(string key, string? name, List<string> errors)
    {
        if (name == null)
            return;
        if (name.Trim().Length == 0)
            errors.Add($"{key} must be null or a VS Code theme name.");
        else if (name != name.Trim())
            errors.Add($"{key} {Show(name)} has leading or trailing spaces; VS Code matches theme names exactly.");
        if (name.Contains('[') || name.Contains(']'))
            errors.Add($"{key} is the plain theme name, without brackets (e.g. \"Dark Modern\").");
    }

    internal static bool IsCSharpScope(string selector)
    {
        if (!ScopePath.IsMatch(selector))
            return false;
        var parts = selector.Split(' ');
        return parts[0] == "source.cs" || parts[^1].EndsWith(".cs", StringComparison.Ordinal);
    }
}
