using System.Runtime.InteropServices;

namespace CSharpCodeColors.Interop;

/// <summary>SVsFontAndColorStorage; only the methods this tool calls, in vtable order.</summary>
[ComImport, Guid("40BC7B1A-E625-4DA1-86B4-7660F3CCBB16"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVsFontAndColorStorage
{
    [PreserveSig] int OpenCategory(ref Guid category, uint flags);
    [PreserveSig] int CloseCategory();
    [PreserveSig] int RemoveCategory(ref Guid category);
    [PreserveSig] int GetFont(IntPtr logFont, IntPtr fontInfo);
    [PreserveSig] int GetItem([MarshalAs(UnmanagedType.LPWStr)] string name, [Out, MarshalAs(UnmanagedType.LPArray, SizeConst = 1)] ColorableItemInfo[] info);
}
