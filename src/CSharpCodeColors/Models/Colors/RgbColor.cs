namespace CSharpCodeColors.Models.Colors;

/// <summary>An sRGB color.</summary>
internal readonly record struct RgbColor(byte R, byte G, byte B)
{
    /// <summary>Converts a Win32 COLORREF / OLE_COLOR (0x00BBGGRR) to RGB.</summary>
    public static RgbColor FromColorRef(uint colorRef) =>
        new((byte)(colorRef & 0xFF), (byte)((colorRef >> 8) & 0xFF), (byte)((colorRef >> 16) & 0xFF));

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}
