using CSharpCodeColors.Models.Colors;
using static CSharpCodeColors.Tests.TestData.TestItems;

namespace CSharpCodeColors.Tests.Models.Colors;

public class OkLabTests
{
    [Test]
    [Arguments(0x000000)]
    [Arguments(0xFFFFFF)]
    [Arguments(0x4EC9B0)]
    [Arguments(0xD16969)]
    [Arguments(0x0000FF)]
    public async Task Rgb_round_trips(int rgb) =>
        await Assert.That(OkLab.FromRgb(Rgb(rgb)).ToRgb()).IsEqualTo(Rgb(rgb));

    [Test]
    public async Task White_and_black_are_the_ends_of_lightness()
    {
        await Assert.That(OkLab.FromRgb(Rgb(0xFFFFFF)).L).IsEqualTo(1.0).Within(1e-4);
        await Assert.That(OkLab.FromRgb(Rgb(0x000000)).L).IsEqualTo(0.0).Within(1e-4);
        await Assert.That(OkLab.FromRgb(Rgb(0x808080)).Chroma).IsLessThan(1e-4);
    }

    [Test]
    public async Task Out_of_gamut_colors_keep_lightness_and_hue()
    {
        var vivid = OkLab.FromLch(0.9, 0.3, 230);
        await Assert.That(vivid.IsInGamut).IsFalse();
        var mapped = OkLab.FromRgb(vivid.ToRgb());
        await Assert.That(mapped.L).IsEqualTo(0.9).Within(0.01);
        await Assert.That(mapped.Hue).IsEqualTo(230).Within(2);
    }

    [Test]
    public async Task Near_black_differences_are_not_exaggerated()
    {
        // Plain OKLab puts #000001 about as far from black as two clearly different grays.
        double nearBlack = OkLab.FromRgb(Rgb(0x000001)).DistanceTo(OkLab.FromRgb(Rgb(0x000000)));
        double grays = OkLab.FromRgb(Rgb(0x808080)).DistanceTo(OkLab.FromRgb(Rgb(0x8C8C8C)));
        await Assert.That(nearBlack).IsLessThan(0.01);
        await Assert.That(grays).IsGreaterThan(0.03);
    }

    [Test]
    public async Task Contrast_follows_wcag()
    {
        await Assert.That(OkLab.Contrast(Rgb(0x000000), Rgb(0xFFFFFF))).IsEqualTo(21).Within(0.01);
        await Assert.That(OkLab.Contrast(Rgb(0x777777), Rgb(0xFFFFFF))).IsEqualTo(4.48).Within(0.01);
    }

    [Test]
    [Arguments("#4EC9B0", 0x4EC9B0, 1.0)]
    [Arguments("#4ec9b0", 0x4EC9B0, 1.0)]
    [Arguments("#abc", 0xAABBCC, 1.0)]
    [Arguments("#4EC9B080", 0x4EC9B0, 128 / 255.0)]
    public async Task Hex_colors_parse(string text, int rgb, double alpha)
    {
        await Assert.That(RgbColor.TryParseHex(text, out var color, out double parsedAlpha)).IsTrue();
        await Assert.That(color).IsEqualTo(Rgb(rgb));
        await Assert.That(parsedAlpha).IsEqualTo(alpha).Within(1e-9);
    }

    [Test]
    [Arguments("4EC9B0")]
    [Arguments("#4EC9B")]
    [Arguments("#GGGGGG")]
    [Arguments("")]
    public async Task Bad_hex_colors_are_rejected(string text) =>
        await Assert.That(RgbColor.TryParseHex(text, out _, out _)).IsFalse();
}
