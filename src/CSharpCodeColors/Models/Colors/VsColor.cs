namespace CSharpCodeColors.Models.Colors;

/// <summary>
/// One color of a Fonts and Colors item. <see cref="IsDefault"/> means Visual Studio has no explicit
/// color for it ("Default" / "Automatic"), so the editor inherits it from the base classification.
/// <see cref="Color"/> is the RGB Visual Studio resolves the value to on its own; it is only meaningful
/// for explicit colors and for the root item, Plain Text.
/// </summary>
internal readonly record struct VsColor(bool IsDefault, RgbColor Color)
{
    // __VSCOLORTYPE values returned by IVsFontAndColorUtilities.GetColorType.
    private const int CtInvalid = 0, CtRaw = 1, CtColorIndex = 2, CtSysColor = 3, CtVsColor = 4;

    // COLORINDEX values meaning "the Plain Text foreground/background", shown as "Default" in Tools > Options.
    private const uint CiUserTextForeground = 0, CiUserTextBackground = 1;

    /// <summary>
    /// Classifies a color read from IVsFontAndColorStorage.
    /// </summary>
    /// <param name="encoded">The color as stored (read without FCSF_NOAUTOCOLORS).</param>
    /// <param name="colorType">The __VSCOLORTYPE of <paramref name="encoded"/>.</param>
    /// <param name="resolved">The same color read with FCSF_NOAUTOCOLORS, i.e. as a plain COLORREF.</param>
    public static VsColor FromStorage(uint encoded, int colorType, uint resolved)
    {
        bool isDefault = colorType switch
        {
            CtRaw or CtSysColor or CtVsColor => false,
            CtColorIndex => (encoded & 0x00FFFFFF) is CiUserTextForeground or CiUserTextBackground,
            // CT_AUTOMATIC, CT_TRACK_FOREGROUND, CT_TRACK_BACKGROUND and CT_INVALID carry no color of their own.
            _ => true,
        };
        return new VsColor(isDefault, RgbColor.FromColorRef(resolved));
    }

    public static VsColor Explicit(RgbColor color) => new(false, color);

    public static VsColor Default(RgbColor fallback) => new(true, fallback);
}
