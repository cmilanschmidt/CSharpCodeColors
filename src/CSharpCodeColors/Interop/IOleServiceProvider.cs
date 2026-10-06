using System.Runtime.InteropServices;

namespace CSharpCodeColors.Interop;

[ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOleServiceProvider
{
    [PreserveSig] int QueryService(ref Guid service, ref Guid riid, out IntPtr ppv);
}
