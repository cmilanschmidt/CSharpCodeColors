using System.Globalization;

namespace CSharpCodeColors.Models.Colors;

/// <summary>An sRGB color.</summary>
internal readonly record struct RgbColor(byte R, byte G, byte B)
{
    /// <summary>Converts a Win32 COLORREF / OLE_COLOR (0x00BBGGRR) to RGB.</summary>
    public static RgbColor FromColorRef(uint colorRef) =>
        new((byte)(colorRef & 0xFF), (byte)((colorRef >> 8) & 0xFF), (byte)((colorRef >> 16) & 0xFF));

    /// <summary>
    /// Parses a CSS hex color as VS Code themes write them: #RGB, #RGBA, #RRGGBB or #RRGGBBAA.
    /// <paramref name="alpha"/> is 0..1; 1 when the color has no alpha.
    /// </summary>
    public static bool TryParseHex(string? text, out RgbColor color, out double alpha)
    {
        color = default;
        alpha = 1;
        if (text is not ['#', .. var hex] || !hex.All(Uri.IsHexDigit))
            return false;
        if (hex.Length is 3 or 4)
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        if (hex.Length is not (6 or 8))
            return false;
        color = new RgbColor(Byte(hex, 0), Byte(hex, 2), Byte(hex, 4));
        if (hex.Length == 8)
            alpha = Byte(hex, 6) / 255.0;
        return true;

        static byte Byte(string hex, int index) => byte.Parse(hex.AsSpan(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    /// <summary>This color drawn with <paramref name="alpha"/> over <paramref name="background"/>.</summary>
    public RgbColor Over(RgbColor background, double alpha) => new(
        (byte)Math.Round(R * alpha + background.R * (1 - alpha)),
        (byte)Math.Round(G * alpha + background.G * (1 - alpha)),
        (byte)Math.Round(B * alpha + background.B * (1 - alpha)));

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}
