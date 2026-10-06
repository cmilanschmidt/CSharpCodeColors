using CSharpCodeColors.Constants;
using CSharpCodeColors.Models.Colors;
using CSharpCodeColors.Services.Colors;

namespace CSharpCodeColors.Tests.TestData;

/// <summary>Builds fake Fonts and Colors items resembling VS 2026 Dark.</summary>
internal static class TestItems
{
    public static readonly RgbColor PlainForeground = new(0xDC, 0xDC, 0xDC);
    public static readonly RgbColor PlainBackground = new(0x1E, 0x1E, 0x1E);

    public static VsColorItem Explicit(string name, int rgb, bool bold = false) =>
        new(name, VsColor.Explicit(Rgb(rgb)), VsColor.Default(PlainBackground), bold);

    public static VsColorItem Default(string name, bool bold = false) =>
        new(name, VsColor.Default(PlainForeground), VsColor.Default(PlainBackground), bold);

    public static VsColorItem WithBackground(string name, int foreground, int background) =>
        new(name, VsColor.Explicit(Rgb(foreground)), VsColor.Explicit(Rgb(background)), false);

    public static VsColorItem PlainText() =>
        new(ClassificationNames.PlainText, VsColor.Explicit(PlainForeground), VsColor.Explicit(PlainBackground), false);

    public static RgbColor Rgb(int rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    public static ColorResolver Resolver(params VsColorItem[] items) => new([PlainText(), .. items]);
}
