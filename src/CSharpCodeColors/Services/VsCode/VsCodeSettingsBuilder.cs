using System.Text.Json;
using System.Text.Json.Nodes;
using CSharpCodeColors.Constants;
using CSharpCodeColors.Models.Colors;
using CSharpCodeColors.Models.VsCode;
using CSharpCodeColors.Options;
using CSharpCodeColors.Services.Colors;

namespace CSharpCodeColors.Services.VsCode;

/// <summary>Turns the mappings and the colors read from Visual Studio into VS Code settings.</summary>
internal static class VsCodeSettingsBuilder
{
    /// <summary>
    /// With <paramref name="adaptation"/>, items are written in the adapted styles and the output is scoped to the
    /// adapted theme (settings validation keeps VsCodeThemeScope null then).
    /// </summary>
    public static BuildResult Build(AppOptions settings, ColorResolver colors, ThemeAdaptation? adaptation = null)
    {
        var semanticRules = new JsonObject();
        var textMateRules = new JsonArray();
        var mapped = new List<string>();
        var missing = new List<string>();
        var backgrounds = new List<(string, RgbColor)>();
        var nothingToEmit = new List<string>();
        var warnings = new List<string>();
        var editorBackground = colors.Background(ClassificationNames.PlainText).Color;

        // VS Code gives "*.static:csharp" the same score as "method:csharp" and lets the later rule win,
        // so additive items (layered on top in VS) are always written after the others.
        var ordered = settings.Mappings
            .OrderBy(m => ClassificationHierarchy.IsAdditive(m.VisualStudioItem) ? 1 : 0); // stable: keeps the file order otherwise
        foreach (var mapping in ordered)
        {
            var item = colors.Find(mapping.VisualStudioItem);
            if (item == null)
            {
                if (!missing.Contains(mapping.VisualStudioItem))
                {
                    missing.Add(mapping.VisualStudioItem);
                    warnings.Add($"{AppOptions.Show(mapping.VisualStudioItem)} is not a Fonts and Colors item in Visual Studio; skipped it. Run with --list to see the available names.");
                }
                continue;
            }
            bool firstTime = !mapped.Contains(item.Name);
            if (firstTime)
                mapped.Add(item.Name);

            var background = item.Background.IsDefault && ClassificationHierarchy.IsAdditive(item.Name)
                ? editorBackground
                : colors.Background(item.Name).Color;
            if (background != editorBackground && firstTime)
            {
                backgrounds.Add((item.Name, background));
                warnings.Add($"\"{item.Name}\" has background {background} in Visual Studio; VS Code token rules can't set a background, so it was skipped.");
            }

            var style = adaptation != null && adaptation.Styles.TryGetValue(item.Name, out var adapted) ? adapted : StyleFor(item, colors);
            if (style == null)
            {
                if (firstTime)
                    nothingToEmit.Add(item.Name);
                continue;
            }

            foreach (var selector in mapping.SemanticTokens)
                semanticRules[selector] = style.ToSemanticRule();
            if (mapping.TextMateScopes.Count > 0)
            {
                textMateRules.Add(new JsonObject
                {
                    ["name"] = $"Visual Studio: {item.Name}",
                    ["scope"] = new JsonArray(mapping.TextMateScopes.Select(s => (JsonNode)JsonValue.Create(s)!).ToArray()),
                    ["settings"] = style.ToTextMateSettings(),
                });
            }
        }

        var semantic = new JsonObject { ["enabled"] = true, ["rules"] = semanticRules };
        var textMate = new JsonObject { ["textMateRules"] = textMateRules };
        var root = new JsonObject();
        if ((adaptation?.Theme.Contribution.SettingsId ?? settings.VsCodeThemeScope) is { } theme)
        {
            root["editor.semanticTokenColorCustomizations"] = new JsonObject { [$"[{theme}]"] = semantic };
            root["editor.tokenColorCustomizations"] = new JsonObject { [$"[{theme}]"] = textMate };
        }
        else
        {
            root["editor.semanticTokenColorCustomizations"] = semantic;
            root["editor.tokenColorCustomizations"] = textMate;
        }

        // Relaxed escaping keeps theme names readable ("[Dark+]" rather than "[Dark\u002B]"); the file never goes into HTML.
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        string json = root.ToJsonString(options) + Environment.NewLine;
        return new BuildResult(json, semanticRules.Count, textMateRules.Count, mapped, missing, backgrounds, nothingToEmit, warnings);
    }

    /// <summary>
    /// The style VS shows for an item. Normal items always get a color and an explicit bold/non-bold,
    /// because VS never shows italics. Additive items (static symbol) only contribute what is set on
    /// them explicitly, so the underlying classification keeps its color; null if they contribute nothing.
    /// </summary>
    internal static TokenStyle? StyleFor(VsColorItem item, ColorResolver colors)
    {
        if (ClassificationHierarchy.IsAdditive(item.Name))
        {
            RgbColor? foreground = item.Foreground.IsDefault ? null : item.Foreground.Color;
            return foreground == null && !item.Bold ? null : new TokenStyle(foreground, item.Bold, Additive: true);
        }
        return new TokenStyle(colors.Foreground(item.Name).Color, item.Bold, Additive: false);
    }
}
