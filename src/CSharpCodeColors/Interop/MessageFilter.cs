using Microsoft.Extensions.Logging;

namespace CSharpCodeColors.Interop;

/// <summary>
/// Retries calls Visual Studio rejects because it is busy (e.g. a modal dialog is open),
/// and gives up after <see cref="Timeout"/> so the tool can report it.
/// </summary>
internal sealed class MessageFilter(ILogger logger) : IOleMessageFilter
{
    private const int ServerCallIsHandled = 0, ServerCallRetryLater = 2, PendingMsgWaitDefProcess = 2;
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    // One deadline for the whole run, counted from the first rejection, rather than per call.
    private System.Diagnostics.Stopwatch? _sinceFirstRejection;

    public int HandleInComingCall(int callType, IntPtr taskCaller, int tickCount, IntPtr interfaceInfo) => ServerCallIsHandled;

    public int RetryRejectedCall(IntPtr taskCallee, int tickCount, int rejectType)
    {
        if (rejectType != ServerCallRetryLater)
            return -1;
        if (_sinceFirstRejection == null)
        {
            logger.LogWarning("Visual Studio is busy; waiting for it to respond (close any open dialog in Visual Studio)...");
            _sinceFirstRejection = System.Diagnostics.Stopwatch.StartNew();
        }
        return _sinceFirstRejection.Elapsed >= Timeout ? -1 : 250;
    }

    public int MessagePending(IntPtr taskCallee, int tickCount, int pendingType) => PendingMsgWaitDefProcess;
}
