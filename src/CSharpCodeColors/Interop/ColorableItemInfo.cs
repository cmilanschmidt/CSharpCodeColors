using System.Runtime.InteropServices;

namespace CSharpCodeColors.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct ColorableItemInfo
{
    public uint crForeground;
    public uint crBackground;
    public uint dwFontFlags;
    public int bForegroundValid;
    public int bBackgroundValid;
    public int bFontFlagsValid;
}
