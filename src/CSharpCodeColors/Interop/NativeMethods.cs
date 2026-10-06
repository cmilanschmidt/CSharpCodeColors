using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace CSharpCodeColors.Interop;

internal static class NativeMethods
{
    [DllImport("ole32.dll")]
    public static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable rot);

    [DllImport("ole32.dll")]
    public static extern int CreateBindCtx(int reserved, out IBindCtx ctx);

    [DllImport("ole32.dll")]
    public static extern int CoRegisterMessageFilter(IOleMessageFilter? newFilter, out IOleMessageFilter? oldFilter);
}
