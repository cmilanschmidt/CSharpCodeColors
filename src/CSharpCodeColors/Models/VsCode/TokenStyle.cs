using System.Text.Json.Nodes;
using CSharpCodeColors.Models.Colors;

namespace CSharpCodeColors.Models.VsCode;

/// <summary>The style written for a token. A null <see cref="Bold"/> writes no font style, leaving it to the theme.</summary>
internal sealed record TokenStyle(RgbColor? Foreground, bool? Bold, bool Additive)
{
    public JsonObject ToSemanticRule()
    {
        var rule = new JsonObject();
        if (Foreground is { } color)
            rule["foreground"] = color.ToString();
        if (Additive)
        {
            if (Bold == true)
                rule["bold"] = true;
        }
        else if (Bold is { } bold)
        {
            rule["fontStyle"] = bold ? "bold" : "";
        }
        return rule;
    }

    public JsonObject ToTextMateSettings()
    {
        var rule = new JsonObject();
        if (Foreground is { } color)
            rule["foreground"] = color.ToString();
        if (Bold is { } bold && (!Additive || bold))
            rule["fontStyle"] = bold ? "bold" : "";
        return rule;
    }
}
