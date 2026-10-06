using System.Runtime.InteropServices;

namespace CSharpCodeColors.Interop;

/// <summary>Implemented by the same object as SVsFontAndColorStorage; only the methods up to GetColorType.</summary>
[ComImport, Guid("A356A017-07EE-4D06-ACDE-FEFDBB49EB50"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVsFontAndColorUtilities
{
    [PreserveSig] int EncodeIndexedColor(int index, out uint color);
    [PreserveSig] int EncodeSysColor(int index, out uint color);
    [PreserveSig] int EncodeVSColor(int index, out uint color);
    [PreserveSig] int EncodeTrackedItem(int item, int property, out uint color);
    [PreserveSig] int EncodeInvalidColor(out uint color);
    [PreserveSig] int EncodeAutomaticColor(out uint color);
    [PreserveSig] int GetColorType(uint color, out int type);
}
