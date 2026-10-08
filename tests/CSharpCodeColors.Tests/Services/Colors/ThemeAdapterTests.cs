using CSharpCodeColors.Models.Colors;
using CSharpCodeColors.Services.Colors;
using static CSharpCodeColors.Tests.TestData.TestItems;

namespace CSharpCodeColors.Tests.Services.Colors;

public class ThemeAdapterTests
{
    private static readonly RgbColor DarkBackground = Rgb(0x1F1F1F);
    private static readonly RgbColor LightBackground = Rgb(0xFFFFFF);

    private static AdaptInput Input(string item, int visualStudio, int theme, int? priority = null) =>
        new(item, Rgb(visualStudio), Rgb(theme), priority);

    // A palette spanning a typical theme's lightness and chroma, so new colors aren't clamped by a tiny test palette.
    private static readonly RgbColor[] Palette = [Rgb(0xD4D4D4), Rgb(0x404040), Rgb(0xC586C0), Rgb(0x569CD6)];

    private static Dictionary<string, AdaptedColor> Adapt(RgbColor background, params AdaptInput[] inputs) =>
        ThemeAdapter.Adapt(inputs, background, [.. inputs.Select(i => i.Theme), .. Palette]).ToDictionary(c => c.Item);

    private static double Distance(RgbColor x, RgbColor y) => OkLab.FromRgb(x).DistanceTo(OkLab.FromRgb(y));

    [Test]
    public async Task Distinctions_the_theme_already_makes_keep_the_theme_colors()
    {
        var result = Adapt(DarkBackground,
            Input("Keyword", 0x569CD6, 0x569CD6, 1),
            Input("String", 0xD69D85, 0xCE9178, 3));
        await Assert.That(result["Keyword"].Output).IsEqualTo(Rgb(0x569CD6));
        await Assert.That(result["String"].Output).IsEqualTo(Rgb(0xCE9178));
        await Assert.That(result.Values.All(c => c.Origin == AdaptOrigin.Theme)).IsTrue();
    }

    [Test]
    public async Task Theme_distinctions_Visual_Studio_lacks_are_kept()
    {
        // Dark Modern colors enum members, Visual Studio doesn't.
        var result = Adapt(DarkBackground,
            Input("Plain Text", 0xDCDCDC, 0xCCCCCC, 0),
            Input("enum member name", 0xDCDCDC, 0x4FC1FF));
        await Assert.That(result["enum member name"].Output).IsEqualTo(Rgb(0x4FC1FF));
    }

    [Test]
    public async Task Item_Visual_Studio_colors_like_the_theme_keeps_it_and_the_other_gets_Visual_Studios_difference()
    {
        // Dark Modern shows structs like classes; with the theme's class color equal to Visual Studio's,
        // the struct gets exactly Visual Studio's struct color.
        var result = Adapt(DarkBackground,
            Input("class name", 0x4EC9B0, 0x4EC9B0, 5),
            Input("struct name", 0x86C691, 0x4EC9B0, 16));
        await Assert.That(result["class name"].Output).IsEqualTo(Rgb(0x4EC9B0));
        await Assert.That(result["struct name"].Output).IsEqualTo(Rgb(0x86C691));
        await Assert.That(result["struct name"].Origin).IsEqualTo(AdaptOrigin.Derived);
        await Assert.That(result["struct name"].Basis).IsEqualTo("class name");
    }

    [Test]
    public async Task The_more_important_item_keeps_the_theme_color()
    {
        // Monokai shows keywords and regex anchors in the same pink; the regex anchor's Visual Studio color is
        // closer to it, but keywords are what the color is for.
        var result = Adapt(DarkBackground,
            Input("Keyword", 0x569CD6, 0xF92672, 1),
            Input("regex - anchor", 0xF979AE, 0xF92672));
        await Assert.That(result["Keyword"].Output).IsEqualTo(Rgb(0xF92672));
        await Assert.That(result["regex - anchor"].Origin).IsNotEqualTo(AdaptOrigin.Theme);
    }

    [Test]
    public async Task Without_priorities_the_closest_Visual_Studio_color_keeps_the_theme_color()
    {
        var result = Adapt(DarkBackground,
            Input("a", 0xF979AE, 0xF92672),
            Input("b", 0x569CD6, 0xF92672));
        await Assert.That(result["a"].Output).IsEqualTo(Rgb(0xF92672));
    }

    [Test]
    public async Task New_colors_stay_distinguishable_and_readable_on_a_light_theme()
    {
        // Light Modern: the theme shows properties like locals (dark blue); Visual Studio Light shows them black
        // like plain text and operators. The new color must differ from all of them, not just be black.
        var result = Adapt(LightBackground,
            Input("Plain Text", 0x000000, 0x3B3B3B, 0),
            Input("Operator", 0x000000, 0x000000, 10),
            Input("local name", 0x1F377F, 0x001080, 7),
            Input("property name", 0x000000, 0x001080, 12));
        var property = result["property name"].Output;
        await Assert.That(Distance(property, result["local name"].Output)).IsGreaterThanOrEqualTo(ThemeAdapter.VisualStudioDifference);
        await Assert.That(Distance(property, result["Operator"].Output)).IsGreaterThanOrEqualTo(ThemeAdapter.ThemeDifference);
        await Assert.That(Distance(property, result["Plain Text"].Output)).IsGreaterThanOrEqualTo(ThemeAdapter.ThemeDifference);
        await Assert.That(OkLab.Contrast(property, LightBackground)).IsGreaterThanOrEqualTo(4.5);
    }

    [Test]
    public async Task A_new_color_snaps_to_a_nearby_unused_palette_color()
    {
        var palette = new[] { Rgb(0x4EC9B0), Rgb(0x88C895) };
        var result = ThemeAdapter.Adapt(
            [Input("class name", 0x4EC9B0, 0x4EC9B0, 5), Input("struct name", 0x86C691, 0x4EC9B0, 16)],
            DarkBackground, palette).ToDictionary(c => c.Item);
        await Assert.That(result["struct name"].Output).IsEqualTo(Rgb(0x88C895));
        await Assert.That(result["struct name"].Origin).IsEqualTo(AdaptOrigin.Palette);
    }

    [Test]
    public async Task Small_hue_differences_turn_the_theme_hue_and_large_ones_take_Visual_Studios()
    {
        var themeOrange = OkLab.FromRgb(Rgb(0xCB4B16));
        var teal = OkLab.FromRgb(Rgb(0x4EC9B0));
        var green = OkLab.FromRgb(Rgb(0x86C691));
        var yellow = OkLab.FromRgb(Rgb(0xDCDCAA));

        // Teal to green is a shade: the orange turns by the same angle.
        var shade = ThemeAdapter.Transfer(themeOrange, teal, green);
        double turn = (green.Hue - teal.Hue + 540) % 360 - 180;
        await Assert.That(Math.Abs((shade.Hue - themeOrange.Hue + 540) % 360 - 180 - turn)).IsLessThan(0.5);

        // Teal to yellow is another color: the result is yellow, in the orange's saturation.
        var other = ThemeAdapter.Transfer(themeOrange, teal, yellow);
        await Assert.That(Math.Abs(other.Hue - yellow.Hue)).IsLessThan(0.5);
    }
}
