using CSharpCodeColors.Models.Colors;

namespace CSharpCodeColors.Tests.Models.Colors;

public class ColorTests
{
    [Test]
    // Verified in VS: 0x000000FF rendered red and 0x00FF8000 rendered #0080FF.
    [Arguments(0x000000FFu, "#FF0000")]
    [Arguments(0x00FF8000u, "#0080FF")]
    [Arguments(0x00D69C56u, "#569CD6")]
    [Arguments(0x00000000u, "#000000")]
    public async Task ColorRef_is_BBGGRR(uint colorRef, string expected) =>
        await Assert.That(RgbColor.FromColorRef(colorRef).ToString()).IsEqualTo(expected);

    [Test]
    // Values observed through IVsFontAndColorStorage / GetColorType on VS 18.10.
    [Arguments(0x0000FF00u, 1, false)] // CT_RAW: explicit
    [Arguments(0x00000000u, 1, false)] // CT_RAW explicit black is a real color
    [Arguments(0x01000000u, 2, true)]  // CT_COLORINDEX CI_USERTEXT_FG: "Default"
    [Arguments(0x01000001u, 2, true)]  // CT_COLORINDEX CI_USERTEXT_BK: "Default"
    [Arguments(0x0100000Cu, 2, false)] // CT_COLORINDEX CI_RED: a concrete color
    [Arguments(0x02000000u, 5, true)]  // CT_AUTOMATIC
    [Arguments(0x08000000u, 7, true)]  // CT_TRACK_BACKGROUND
    [Arguments(0x10000005u, 3, false)] // CT_SYSCOLOR
    [Arguments(0xFF000000u, 0, true)]  // CT_INVALID
    public async Task Storage_color_types_are_classified(uint encoded, int type, bool isDefault) =>
        await Assert.That(VsColor.FromStorage(encoded, type, 0x00123456).IsDefault).IsEqualTo(isDefault);

    [Test]
    public async Task Storage_color_uses_resolved_value() =>
        await Assert.That(VsColor.FromStorage(0x01000000, 2, 0x00123456).Color.ToString()).IsEqualTo("#563412");
}
