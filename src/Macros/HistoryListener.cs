using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using PaintDotNet;
using PaintNetMacroApi.Core;

namespace PaintNetMacroApi.Macros;

public sealed class HistoryListener : IDisposable
{
    private readonly PaintDotNetBridge _bridge;
    private readonly MacroRecorder _recorder;
    private object? _stack;
    private EventInfo? _event;
    private Delegate? _handler;

    public HistoryListener(PaintDotNetBridge bridge, MacroRecorder recorder)
    {
        _bridge = bridge;
        _recorder = recorder;
    }

    public bool Attach()
    {
        try
        {
            _stack = _bridge.GetHistoryStack();
            if (_stack is null) return false;

            _event = _stack.GetType().GetEvent("NewHistoryMemento",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (_event is null || _event.EventHandlerType is null) return false;

            var mi = typeof(HistoryListener).GetMethod(nameof(OnNewHistoryMemento),
                BindingFlags.NonPublic | BindingFlags.Instance)!;
            _handler = Delegate.CreateDelegate(_event.EventHandlerType, this, mi);
            _event.AddEventHandler(_stack, _handler);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_stack is not null && _event is not null && _handler is not null)
        {
            try { _event.RemoveEventHandler(_stack, _handler); } catch { }
            _stack = null;
            _event = null;
            _handler = null;
        }
    }

    private void OnNewHistoryMemento(object? sender, EventArgs e)
    {
        try
        {
            var stack = sender;
            if (stack is null) return;
            var undoProp = stack.GetType().GetProperty("UndoStack",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (undoProp?.GetValue(stack) is not IEnumerable undo) return;

            object? memento = null;
            foreach (var item in undo) memento = item;
            if (memento is null) return;

            var name = (memento.GetType().GetProperty("Name")?.GetValue(memento) as string) ?? memento.GetType().Name;
            var typeName = memento.GetType().Name;

            // Snapshot ALL layers (since a CompoundHistoryMemento can touch any of them).
            var doc = _bridge.GetActiveDocument();
            if (doc is null) return;

            var layers = new System.Collections.Generic.List<object>();
            for (int i = 0; i < doc.Layers.Count; i++)
            {
                if (doc.Layers[i] is not BitmapLayer bl) continue;
                string png = SnapshotLayerToBase64(bl);
                layers.Add(new { index = i, png });
            }

            var args = JsonSerializer.SerializeToElement(new
            {
                memento = typeName,
                name = name,
                layers = layers
            });

            _recorder.Observe(new ApiCall("HISTORY", "/history/snapshot", args.GetRawText()));
        }
        catch
        {
            // never propagate into PDN
        }
    }

    private static string SnapshotLayerToBase64(BitmapLayer bl)
    {
        using var bmp = bl.Surface.CreateAliasedBitmap();
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return Convert.ToBase64String(ms.ToArray());
    }
}
