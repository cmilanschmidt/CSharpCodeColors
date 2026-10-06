using CSharpCodeColors.Models.Colors;

namespace CSharpCodeColors.Models.VsCode;

internal sealed record BuildResult(
    string Json,
    int SemanticRuleCount,
    int TextMateRuleCount,
    List<string> Mapped,
    List<string> Missing,
    List<(string Item, RgbColor Background)> BackgroundsSkipped,
    List<string> NothingToEmit,
    List<string> Warnings);
