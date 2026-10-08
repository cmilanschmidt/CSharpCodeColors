using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using CSharpCodeColors.Exceptions;
using CSharpCodeColors.Models.Colors;
using CSharpCodeColors.Models.VsCode.Themes;

namespace CSharpCodeColors.Services.VsCode.Themes;

/// <summary>
/// Reads a VS Code color theme the way VS Code's colorThemeData.ts loads it: a JSON theme (comments and trailing
/// commas allowed) after the theme its "include" names, or a TextMate .tmTheme property list.
/// </summary>
internal static class ThemeFileReader
{
    private static readonly JsonDocumentOptions JsonOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private sealed class Data
    {
        public Dictionary<string, string> Colors { get; } = new(StringComparer.Ordinal);
        public List<(JsonElement? Scope, string? Foreground, string? FontStyle)> TokenColors { get; } = [];
        public List<(string Selector, JsonElement Value)> SemanticTokenColors { get; } = [];
        public bool SemanticHighlighting { get; set; }
    }

    public static VsCodeTheme Read(ThemeContribution contribution)
    {
        var data = new Data();
        try
        {
            Load(contribution.Path, data, 0);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or XmlException or InvalidOperationException)
        {
            throw new FatalException($"Can't read the VS Code theme \"{contribution.SettingsId}\" ({contribution.Path}): {e.Message}");
        }

        var background = Color(data.Colors.GetValueOrDefault("editor.background"), null) ?? DefaultBackground(contribution.UiTheme);
        var foreground = Color(data.Colors.GetValueOrDefault("editor.foreground"), background) ?? DefaultForeground(contribution.UiTheme);

        var textMateRules = new List<TextMateRule>();
        foreach (var (scope, foregroundText, fontStyleText) in data.TokenColors)
        {
            // Like VS Code, rules without a scope are ignored here; a .tmTheme's global settings became colors above.
            var selectors = scope switch
            {
                { ValueKind: JsonValueKind.String } s => [s.GetString()!],
                { ValueKind: JsonValueKind.Array } a => a.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList(),
                _ => (List<string>)[],
            };
            var selector = ScopeSelector.Parse(selectors);
            if (selector.IsEmpty)
                continue;
            var ruleForeground = Color(foregroundText, background);
            if (ruleForeground != null || fontStyleText != null)
                textMateRules.Add(new TextMateRule(selector, ruleForeground, SetsFontStyle: fontStyleText != null));
        }

        var semanticRules = new List<SemanticRule>();
        foreach (var (selector, value) in data.SemanticTokenColors)
        {
            if (ReadSemanticForeground(value, background) is { } color)
                semanticRules.Add(new SemanticRule(SemanticSelector.Parse(selector), color));
        }

        return new VsCodeTheme(contribution, foreground, background, textMateRules, semanticRules, data.SemanticHighlighting);
    }

    private static void Load(string path, Data data, int depth)
    {
        if (depth > 10)
            throw new InvalidOperationException("the \"include\" chain is too long.");
        if (!Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            LoadPropertyList(path, data);
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path), JsonOptions);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"{path} is not a JSON object.");
        if (root.TryGetProperty("include", out var include) && include.ValueKind == JsonValueKind.String)
            Load(Path.Combine(Path.GetDirectoryName(path)!, include.GetString()!), data, depth + 1);
        if (root.TryGetProperty("settings", out var settings) && settings.ValueKind == JsonValueKind.Array)
        {
            // The old format: a .tmTheme converted to JSON.
            AddTextMateSettings(settings.EnumerateArray().Select(JsonToPlist), data);
            return;
        }
        if (root.TryGetProperty("semanticHighlighting", out var semantic) && semantic.ValueKind == JsonValueKind.True)
            data.SemanticHighlighting = true;
        if (root.TryGetProperty("colors", out var colors) && colors.ValueKind == JsonValueKind.Object)
        {
            foreach (var color in colors.EnumerateObject())
            {
                if (color.Value.ValueKind != JsonValueKind.String)
                    continue;
                // "default" undoes a color set by the included theme.
                if (color.Value.GetString() == "default")
                    data.Colors.Remove(color.Name);
                else
                    data.Colors[color.Name] = color.Value.GetString()!;
            }
        }
        if (root.TryGetProperty("tokenColors", out var tokenColors))
        {
            if (tokenColors.ValueKind == JsonValueKind.Array)
            {
                foreach (var rule in tokenColors.EnumerateArray())
                {
                    if (rule.ValueKind != JsonValueKind.Object || !rule.TryGetProperty("settings", out var ruleSettings) || ruleSettings.ValueKind != JsonValueKind.Object)
                        continue;
                    JsonElement? scope = rule.TryGetProperty("scope", out var s) ? s.Clone() : null;
                    data.TokenColors.Add((scope, String(ruleSettings, "foreground"), String(ruleSettings, "fontStyle")));
                }
            }
            else if (tokenColors.ValueKind == JsonValueKind.String)
            {
                LoadPropertyList(Path.Combine(Path.GetDirectoryName(path)!, tokenColors.GetString()!), data);
            }
        }
        if (root.TryGetProperty("semanticTokenColors", out var semanticColors) && semanticColors.ValueKind == JsonValueKind.Object)
        {
            foreach (var rule in semanticColors.EnumerateObject())
                data.SemanticTokenColors.Add((rule.Name, rule.Value.Clone()));
        }
    }

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>A .tmTheme: its "settings" array, where an entry without a scope holds the editor colors.</summary>
    private static void LoadPropertyList(string path, Data data)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        using var reader = XmlReader.Create(path, settings);
        var root = XDocument.Load(reader).Root?.Elements().FirstOrDefault()
            ?? throw new InvalidOperationException($"{path} is an empty property list.");
        if (ReadPlist(root) is not Dictionary<string, object?> theme || theme.GetValueOrDefault("settings") is not List<object?> entries)
            throw new InvalidOperationException($"{path} has no \"settings\" array.");
        AddTextMateSettings(entries, data);
    }

    private static void AddTextMateSettings(IEnumerable<object?> entries, Data data)
    {
        foreach (var entry in entries.OfType<Dictionary<string, object?>>())
        {
            if (entry.GetValueOrDefault("settings") is not Dictionary<string, object?> settings)
                continue;
            if (entry.GetValueOrDefault("scope") is not string scope)
            {
                // VS Code's settingToColorIdMapping, for the two colors this tool uses.
                if (settings.GetValueOrDefault("foreground") is string foreground)
                    data.Colors["editor.foreground"] = foreground;
                if (settings.GetValueOrDefault("background") is string background)
                    data.Colors["editor.background"] = background;
                continue;
            }
            var scopeElement = JsonSerializer.SerializeToElement(scope);
            data.TokenColors.Add((scopeElement, settings.GetValueOrDefault("foreground") as string, settings.GetValueOrDefault("fontStyle") as string));
        }
    }

    private static object? ReadPlist(XElement element) => element.Name.LocalName switch
    {
        "dict" => element.Elements()
            .Where(e => e.Name.LocalName == "key")
            .Select(e => (Key: e.Value, Value: e.ElementsAfterSelf().FirstOrDefault()))
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.Last().Value is { } v ? ReadPlist(v) : null),
        "array" => element.Elements().Select(ReadPlist).ToList(),
        _ => element.Value,
    };

    private static object? JsonToPlist(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().GroupBy(p => p.Name).ToDictionary(g => g.Key, g => JsonToPlist(g.Last().Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(JsonToPlist).ToList(),
        JsonValueKind.String => element.GetString(),
        _ => null,
    };

    /// <summary>The color of a "semanticTokenColors" value: a color string, or an object's "foreground".</summary>
    private static RgbColor? ReadSemanticForeground(JsonElement value, RgbColor background) => value.ValueKind switch
    {
        JsonValueKind.String => Color(value.GetString(), background),
        JsonValueKind.Object => Color(String(value, "foreground"), background),
        _ => null,
    };

    /// <summary>A hex color; one with transparency is blended over <paramref name="background"/>, as the editor shows it.</summary>
    private static RgbColor? Color(string? text, RgbColor? background)
    {
        if (!RgbColor.TryParseHex(text?.Trim(), out var color, out double alpha))
            return null;
        return alpha < 1 && background is { } under ? color.Over(under, alpha) : color;
    }

    // VS Code's defaults for editor.background and editor.foreground (editorColorRegistry.ts).
    private static RgbColor DefaultBackground(string uiTheme) => uiTheme switch
    {
        "vs-dark" => new(0x1E, 0x1E, 0x1E),
        "hc-black" => new(0x00, 0x00, 0x00),
        _ => new(0xFF, 0xFF, 0xFF),
    };

    private static RgbColor DefaultForeground(string uiTheme) => uiTheme switch
    {
        "vs-dark" => new(0xBB, 0xBB, 0xBB),
        "hc-black" => new(0xFF, 0xFF, 0xFF),
        "hc-light" => new(0x29, 0x29, 0x29),
        _ => new(0x33, 0x33, 0x33),
    };
}
