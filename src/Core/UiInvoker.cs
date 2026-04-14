using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PaintNetMacroApi.Core;

// Marshalls calls to the Paint.NET UI thread. The sync context is captured
// at plugin load time from the thread that instantiates the Effect (the UI thread).
public static class UiInvoker
{
    private static SynchronizationContext? _ctx;
    private static Control? _anchor;

    public static void Capture()
    {
        _ctx = SynchronizationContext.Current;
        // Fallback anchor: a throwaway Control created on the UI thread carries its handle.
        if (_anchor is null)
        {
            _anchor = new Control();
            var _ = _anchor.Handle;
        }
    }

    public static Task<T> InvokeAsync<T>(Func<T> fn)
    {
        if (_ctx is null && _anchor is null)
        {
            // Not captured — run inline and hope caller is already on UI thread.
            return Task.FromResult(fn());
        }

        var tcs = new TaskCompletionSource<T>();
        SendOrPostCallback cb = _ =>
        {
            try { tcs.SetResult(fn()); }
            catch (Exception ex) { tcs.SetException(ex); }
        };

        if (_ctx is not null) _ctx.Post(cb, null);
        else _anchor!.BeginInvoke((Action)(() => cb(null)));

        return tcs.Task;
    }

    public static Task InvokeAsync(Action fn) => InvokeAsync<object?>(() => { fn(); return null; });
}
