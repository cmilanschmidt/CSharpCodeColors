using System.Text.Json.Nodes;
using CSharpCodeColors.Models.Colors;

namespace CSharpCodeColors.Models.VsCode;

internal sealed record TokenStyle(RgbColor? Foreground, bool Bold, bool Additive)
{
    public JsonObject ToSemanticRule()
    {
        var rule = new JsonObject();
        if (Foreground is { } color)
            rule["foreground"] = color.ToString();
        if (Additive)
        {
            if (Bold)
                rule["bold"] = true;
        }
        else
        {
            rule["fontStyle"] = Bold ? "bold" : "";
        }
        return rule;
    }

    public JsonObject ToTextMateSettings()
    {
        var rule = new JsonObject();
        if (Foreground is { } color)
            rule["foreground"] = color.ToString();
        if (!Additive || Bold)
            rule["fontStyle"] = Bold ? "bold" : "";
        return rule;
    }
}
