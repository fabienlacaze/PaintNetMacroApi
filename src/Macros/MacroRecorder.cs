using System;
using System.Collections.Generic;
using System.Text.Json;
using PaintNetMacroApi.Core;

namespace PaintNetMacroApi.Macros;

public enum RecorderState { Idle, Recording }

public sealed class MacroRecorder
{
    private readonly object _gate = new();
    private List<MacroStep> _buffer = new();

    public RecorderState State { get; private set; } = RecorderState.Idle;
    public string? CurrentName { get; private set; }

    public event Action<RecorderState>? StateChanged;

    public void Start(string name)
    {
        lock (_gate)
        {
            CurrentName = name;
            _buffer = new List<MacroStep>();
            State = RecorderState.Recording;
        }
        StateChanged?.Invoke(State);
    }

    public Macro Stop()
    {
        Macro m;
        lock (_gate)
        {
            m = new Macro(CurrentName ?? "untitled", _buffer);
            _buffer = new List<MacroStep>();
            State = RecorderState.Idle;
            CurrentName = null;
        }
        StateChanged?.Invoke(State);
        return m;
    }

    public void Observe(ApiCall call)
    {
        if (State != RecorderState.Recording) return;
        // Treat method+path as the op identifier, keep the body as opaque JSON.
        JsonElement args = default;
        if (!string.IsNullOrWhiteSpace(call.Body))
        {
            try { args = JsonDocument.Parse(call.Body).RootElement.Clone(); }
            catch { args = JsonDocument.Parse("{}").RootElement.Clone(); }
        }
        else args = JsonDocument.Parse("{}").RootElement.Clone();

        lock (_gate) _buffer.Add(new MacroStep($"{call.Method} {call.Path}", args));
    }

    public IReadOnlyList<MacroStep> Snapshot()
    {
        lock (_gate) return _buffer.ToArray();
    }
}
