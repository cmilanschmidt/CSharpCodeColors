using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using CSharpCodeColors.Exceptions;
using CSharpCodeColors.Interop;
using CSharpCodeColors.Models.Colors;
using Microsoft.Extensions.Logging;

namespace CSharpCodeColors.Services.VisualStudio;

internal sealed class VisualStudioReader(ILogger<VisualStudioReader> logger) : IVisualStudioReader
{
    // Visual Studio 2026 is version 18; each instance registers its DTE as "!VisualStudio.DTE.18.0:<pid>".
    private const string DteMonikerPrefix = "!VisualStudio.DTE.18.0:";

    private static readonly Guid TextEditorCategory = new("A27B4E24-A735-4D1D-B8E7-9716E1E3D8E0");
    private static readonly Guid FontAndColorStorageService = new("40BC7B1A-E625-4DA1-86B4-7660F3CCBB16");
    private const uint FcsfReadOnly = 0x1, FcsfLoadDefaults = 0x2, FcsfNoAutoColors = 0x8;

    private const int RpcCallRejected = unchecked((int)0x80010001);
    private const int RpcDisconnected = unchecked((int)0x80010108);
    private const int RpcServerUnavailable = unchecked((int)0x800706BA);
    private const int RpcCallFailed = unchecked((int)0x800706BE);

    /// <summary>Connects and reads every item. Runs the COM work on an STA thread with a message filter.</summary>
    public VisualStudioColors Read() => RunOnSta(() =>
    {
        NativeMethods.CoRegisterMessageFilter(new MessageFilter(logger), out var oldFilter);
        object? dte = null;
        try
        {
            (int pid, dte) = FindSingleInstance();
            return new VisualStudioColors(pid, ReadItems(dte));
        }
        catch (COMException e)
        {
            throw new FatalException(DescribeComFailure(e.HResult, e.Message));
        }
        catch (Exception e) when (e is MissingMemberException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or InvalidCastException)
        {
            // Late-bound calls and casts report COM failures (VS busy, VS gone, not really VS) as binder or cast
            // errors whose message carries the HRESULT, e.g. "Could not get dispatch ID for Name (error: 0x80010001)".
            var match = System.Text.RegularExpressions.Regex.Match(e.Message, @"0x([0-9A-Fa-f]{8})");
            int hr = match.Success ? unchecked((int)Convert.ToUInt32(match.Groups[1].Value, 16)) : 0;
            throw new FatalException(hr != 0
                ? DescribeComFailure(hr, e.Message)
                : $"The Visual Studio automation object didn't respond as expected ({e.Message}). If Visual Studio is closing or still starting, run this again once it is up.");
        }
        catch (InvalidComObjectException e)
        {
            throw new FatalException($"Lost the connection to Visual Studio: {e.Message}");
        }
        finally
        {
            if (dte != null)
                Marshal.ReleaseComObject(dte);
            NativeMethods.CoRegisterMessageFilter(oldFilter, out _);
        }
    });

    private static (int Pid, object Dte) FindSingleInstance()
    {
        Marshal.ThrowExceptionForHR(NativeMethods.GetRunningObjectTable(0, out var rot));
        Marshal.ThrowExceptionForHR(NativeMethods.CreateBindCtx(0, out var ctx));
        rot.EnumRunning(out var monikers);
        var found = new List<(int Pid, IMoniker Moniker)>();
        var batch = new IMoniker[1];
        while (monikers.Next(1, batch, IntPtr.Zero) == 0)
        {
            batch[0].GetDisplayName(ctx, null, out string name);
            if (name.StartsWith(DteMonikerPrefix, StringComparison.Ordinal)
                && int.TryParse(name.AsSpan(DteMonikerPrefix.Length), out int pid)
                && IsRunning(pid))
            {
                found.Add((pid, batch[0]));
            }
        }

        if (found.Count == 0)
        {
            throw new FatalException(
                "No running Visual Studio 2026 instance found. Start Visual Studio 2026 and run this again. " +
                "(If Visual Studio runs as administrator, this tool must run as administrator too.)");
        }
        if (found.Count > 1)
        {
            throw new FatalException(
                $"{found.Count} Visual Studio 2026 instances are running (process IDs {string.Join(", ", found.Select(f => f.Pid))}). " +
                "Close all but the one whose colors you want and run this again.");
        }

        rot.GetObject(found[0].Moniker, out object dte);
        return (found[0].Pid, dte);
    }

    private static bool IsRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static List<VsColorItem> ReadItems(object dteObject)
    {
        // Names and bold come from DTE. DTE's Foreground/Background can't tell "Default" colors apart
        // (it reports them as black/white or as the Plain Text colors, depending on VS's state), so the
        // colors come from the font and color storage service, reached through DTE's service provider.
        dynamic dte = dteObject;
        dynamic page = dte.Properties["FontsAndColors", "TextEditor"];
        dynamic dteItems = page.Item("FontsAndColorsItems").Object;
        var names = new List<(string Name, bool Bold, uint Foreground, uint Background)>();
        foreach (dynamic item in dteItems)
            names.Add(((string)item.Name, (bool)item.Bold, (uint)item.Foreground, (uint)item.Background));

        var storage = GetStorage(dteObject);
        var utilities = (IVsFontAndColorUtilities)storage;
        var encoded = ReadStorage(storage, FcsfReadOnly | FcsfLoadDefaults, names.Select(n => n.Name));
        var resolved = ReadStorage(storage, FcsfReadOnly | FcsfLoadDefaults | FcsfNoAutoColors, names.Select(n => n.Name));

        var items = new List<VsColorItem>(names.Count);
        foreach (var (name, bold, dteForeground, dteBackground) in names)
        {
            VsColor foreground, background;
            if (encoded.TryGetValue(name, out var raw) && resolved.TryGetValue(name, out var rgb))
            {
                foreground = Classify(utilities, raw.crForeground, rgb.crForeground);
                background = Classify(utilities, raw.crBackground, rgb.crBackground);
            }
            else
            {
                // The storage didn't know the item; fall back to what DTE reports.
                foreground = VsColor.Explicit(RgbColor.FromColorRef(dteForeground));
                background = VsColor.Explicit(RgbColor.FromColorRef(dteBackground));
            }
            items.Add(new VsColorItem(name, foreground, background, bold));
        }
        return items;
    }

    private static IVsFontAndColorStorage GetStorage(object dte)
    {
        var service = FontAndColorStorageService;
        var iid = typeof(IVsFontAndColorStorage).GUID;
        int hr = ((IOleServiceProvider)dte).QueryService(ref service, ref iid, out IntPtr pointer);
        if (hr != 0 || pointer == IntPtr.Zero)
            throw new FatalException($"Visual Studio did not provide its font and color storage service (HRESULT 0x{hr:X8}).");
        try
        {
            return (IVsFontAndColorStorage)Marshal.GetObjectForIUnknown(pointer);
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }

    private static Dictionary<string, ColorableItemInfo> ReadStorage(IVsFontAndColorStorage storage, uint flags, IEnumerable<string> names)
    {
        var category = TextEditorCategory;
        Marshal.ThrowExceptionForHR(storage.OpenCategory(ref category, flags));
        try
        {
            var result = new Dictionary<string, ColorableItemInfo>(StringComparer.Ordinal);
            foreach (var name in names)
            {
                var info = new ColorableItemInfo[1];
                if (storage.GetItem(name, info) == 0 && info[0].bForegroundValid != 0 && info[0].bBackgroundValid != 0)
                    result[name] = info[0];
            }
            return result;
        }
        finally
        {
            storage.CloseCategory();
        }
    }

    private static VsColor Classify(IVsFontAndColorUtilities utilities, uint encoded, uint resolved)
    {
        Marshal.ThrowExceptionForHR(utilities.GetColorType(encoded, out int type));
        return VsColor.FromStorage(encoded, type, resolved);
    }

    private static string DescribeComFailure(int hresult, string message) => hresult switch
    {
        RpcCallRejected =>
            $"Visual Studio did not respond for {MessageFilter.Timeout.TotalSeconds:0} seconds. " +
            "It is probably showing a modal dialog or is busy; close the dialog and run this again.",
        RpcDisconnected or RpcServerUnavailable or RpcCallFailed =>
            "Lost the connection to Visual Studio (did it close?). Run this again with Visual Studio 2026 open.",
        _ => $"A call to Visual Studio failed: {message} (HRESULT 0x{hresult:X8}).",
    };

    /// <summary>Upper bound for the whole read. A hung Visual Studio never rejects calls, it just doesn't answer.</summary>
    internal static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(60);

    private static T RunOnSta<T>(Func<T> work)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = work(); }
            catch (Exception e) { error = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true; // so the process can exit if the thread is stuck in a call to a hung VS
        thread.Start();
        if (!thread.Join(OverallTimeout))
        {
            throw new FatalException(
                $"Visual Studio did not answer within {OverallTimeout.TotalSeconds:0} seconds; it may be hung. " +
                "Check that Visual Studio responds, then run this again.");
        }
        if (error != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(error);
        return result;
    }
}
