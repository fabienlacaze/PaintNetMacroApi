using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using PaintNetMacroApi.Api;
using PaintNetMacroApi.Macros;

namespace PaintNetMacroApi.Ui;

public sealed class MacroWindow : Form
{
    // Palette
    private static readonly Color BgDark      = Color.FromArgb(30, 30, 32);
    private static readonly Color BgPanel     = Color.FromArgb(40, 40, 44);
    private static readonly Color BgList      = Color.FromArgb(36, 36, 40);
    private static readonly Color FgPrimary   = Color.FromArgb(232, 232, 235);
    private static readonly Color FgMuted     = Color.FromArgb(160, 160, 168);
    private static readonly Color Accent      = Color.FromArgb(80, 140, 240);
    private static readonly Color RecRed      = Color.FromArgb(220, 70, 70);
    private static readonly Color StopGray    = Color.FromArgb(110, 110, 120);
    private static readonly Color PlayGreen   = Color.FromArgb(70, 170, 100);
    private static readonly Color WarnAmber   = Color.FromArgb(220, 160, 60);

    private readonly ApiServer _server;
    private readonly MacroStore _store = new();
    private readonly MacroPlayer _player;
    private HistoryListener? _historyListener;

    private readonly ListView _list;
    private readonly ListView _stepsList;
    private readonly RichTextBox _preview;
    private readonly Label _statusUrl;
    private readonly Label _statusState;
    private readonly Label _stateDot;
    private readonly Button _btnRecord, _btnStop, _btnPlay, _btnRedo, _btnDelete, _btnRefresh, _btnFolder, _btnCompact;
    private readonly Button _btnStepDisable, _btnStepDelete, _btnStepUp, _btnStepDown;
    private string? _lastPlayedName;
    private System.Collections.Generic.List<(int idx, string png)>? _prePlaySnapshot;
    private Size _normalSize;
    private FormBorderStyle _normalBorder;
    private bool _isCompact;
    private CompactBar? _compactBar;

    private static readonly Regex PngRx = new("(\"png\"\\s*:\\s*\")[^\"]{60,}(\")", RegexOptions.Compiled);

    public MacroWindow(ApiServer server)
    {
        _server = server;
        _player = new MacroPlayer(server.Router);

        Text = "Paint.NET Macro API";
        Width = 880;
        Height = 560;
        MinimumSize = new Size(640, 400);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        TopMost = true;
        BackColor = BgDark;
        ForeColor = FgPrimary;
        Font = new Font("Segoe UI", 9.5f);
        Icon = TryLoadAppIcon();

        var asmVer = typeof(MacroWindow).Assembly.GetName().Version;
        var verShort = asmVer is null ? "" : $"v{asmVer.Major}.{asmVer.Minor}.{asmVer.Build}";
        Text = $"Paint.NET Macro API {verShort}";

        // Header bar (very top): name + version + author
        var header = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = BgDark };
        var headerName = new Label
        {
            Text = "Paint.NET Macro API",
            Font = new Font("Segoe UI Semibold", 11.5f),
            ForeColor = FgPrimary,
            AutoSize = true,
            Left = 14, Top = 9,
            BackColor = Color.Transparent,
        };
        var headerMeta = new Label
        {
            Text = $"  ·  {verShort}  ·  by fabidou",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = FgMuted,
            AutoSize = true,
            Top = 12,
            BackColor = Color.Transparent,
        };
        header.Controls.Add(headerName);
        header.Controls.Add(headerMeta);
        header.Resize += (_, _) => headerMeta.Left = headerName.Right;
        headerMeta.Left = headerName.Right;

        // Toolbar (under header)
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = BgPanel };
        _btnRecord  = MakeButton("●  Record",  RecRed,    OnRecord);
        _btnPlay    = MakeButton("▶  Play",    PlayGreen, OnPlay);
        _btnRedo    = MakeButton("↶  Undo",    Accent,    OnRedo);
        _btnStop    = MakeButton("■  Stop",    StopGray,  OnStop);
        _btnDelete  = MakeButton("🗑  Delete",  StopGray,  OnDelete);
        _btnRefresh = MakeButton("⟳",          StopGray,  (_, _) => Refresh());
        _btnFolder  = MakeButton("📁",          StopGray,  (_, _) => Process.Start("explorer.exe", _store.Root));
        _btnCompact = MakeButton("⊟  Compact", Accent,    (_, _) => ToggleCompact());

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = BgPanel,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(10, 10, 10, 8),
            AutoSize = false,
        };
        bar.Controls.AddRange(new Control[] { _btnRecord, _btnPlay, _btnRedo, _btnStop, _btnDelete, _btnRefresh, _btnFolder, _btnCompact });
        toolbar.Controls.Add(bar);

        // Split: list (left) | preview (right)
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            BackColor = BgDark,
            SplitterWidth = 6,
            SplitterDistance = 360,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel1,
        };
        split.Panel1.BackColor = BgDark;
        split.Panel2.BackColor = BgDark;

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false,
            BackColor = BgList,
            ForeColor = FgPrimary,
            BorderStyle = BorderStyle.None,
            GridLines = false,
            Font = new Font("Segoe UI", 9.5f),
        };
        _list.HeaderStyle = ColumnHeaderStyle.None;
        _list.Columns.Add("", 200);
        _list.Columns.Add("", 50);
        _list.Columns.Add("", 70);

        var listHeader = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            Text = "  Saved macros",
            ForeColor = FgMuted,
            BackColor = BgPanel,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 9.5f),
        };
        split.Panel1.Controls.Add(_list);
        split.Panel1.Controls.Add(listHeader);

        // Right pane: action list (fills) + collapsible Raw JSON (bottom).
        var jsonHeader = new Label
        {
            Dock = DockStyle.Top, Height = 32,
            Text = "  ▶  Raw JSON (click to expand)",
            ForeColor = FgMuted, BackColor = BgPanel,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 9.5f),
            Cursor = Cursors.Hand,
        };
        _preview = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = BgList,
            ForeColor = FgPrimary,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 9.5f),
            DetectUrls = false,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            Visible = false, // collapsed by default
        };
        var jsonContainer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 32, // collapsed = just the header
            BackColor = BgDark,
        };
        jsonContainer.Controls.Add(_preview);
        jsonContainer.Controls.Add(jsonHeader);

        bool jsonExpanded = false;
        jsonHeader.Click += (_, _) =>
        {
            jsonExpanded = !jsonExpanded;
            _preview.Visible = jsonExpanded;
            jsonContainer.Height = jsonExpanded ? Math.Max(180, split.Panel2.ClientSize.Height * 50 / 100) : 32;
            jsonHeader.Text = jsonExpanded
                ? "  ▼  Raw JSON (click to collapse — PNG payloads hidden)"
                : "  ▶  Raw JSON (click to expand)";
        };

        var stepsHeader = new Label
        {
            Dock = DockStyle.Top, Height = 32,
            Text = "  Action sequence",
            ForeColor = FgMuted, BackColor = BgPanel,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 9.5f),
        };
        _stepsList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false,
            BackColor = BgList,
            ForeColor = FgPrimary,
            BorderStyle = BorderStyle.None,
            GridLines = false,
            Font = new Font("Segoe UI", 9.5f),
        };
        _stepsList.Columns.Add("#", 36);
        _stepsList.Columns.Add("✓", 32);
        _stepsList.Columns.Add("Action", 200);
        _stepsList.Columns.Add("Detail", 200);

        // Step edit toolbar
        var stepBar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = BgPanel };
        _btnStepDisable = MakeSmallButton("Toggle", StopGray, (_, _) => OnStepToggle());
        _btnStepDelete  = MakeSmallButton("Delete", StopGray, (_, _) => OnStepDelete());
        _btnStepUp      = MakeSmallButton("▲ Up",   StopGray, (_, _) => OnStepMove(-1));
        _btnStepDown    = MakeSmallButton("▼ Down", StopGray, (_, _) => OnStepMove(+1));
        var stepBarFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = BgPanel,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            Padding = new Padding(8, 6, 8, 6),
        };
        stepBarFlow.Controls.AddRange(new Control[] { _btnStepDisable, _btnStepDelete, _btnStepUp, _btnStepDown });
        stepBar.Controls.Add(stepBarFlow);

        split.Panel2.Controls.Add(_stepsList);
        split.Panel2.Controls.Add(stepBar);
        split.Panel2.Controls.Add(stepsHeader);
        split.Panel2.Controls.Add(jsonContainer);

        _stepsList.SelectedIndexChanged += (_, _) => UpdateStepButtonStates();

        // Status bar
        var status = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = BgPanel };
        _stateDot = new Label
        {
            Width = 14, Height = 14, Top = 8, Left = 12,
            BackColor = StopGray,
            BorderStyle = BorderStyle.None,
        };
        _stateDot.Paint += (s, e) =>
        {
            var c = ((Label)s!).BackColor;
            using var br = new SolidBrush(c);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.FillEllipse(br, 0, 0, 14, 14);
        };
        _statusState = new Label
        {
            Left = 32, Top = 7, Width = 360, Height = 16,
            ForeColor = FgPrimary,
            Text = "Idle",
            BackColor = Color.Transparent,
        };
        _statusUrl = new Label
        {
            Top = 7, Width = 280, Height = 16,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            ForeColor = FgMuted,
            Text = "Server: http://127.0.0.1:8787",
            TextAlign = ContentAlignment.MiddleRight,
            BackColor = Color.Transparent,
        };
        _statusUrl.Left = status.ClientSize.Width - _statusUrl.Width - 12;
        status.Resize += (_, _) => _statusUrl.Left = status.ClientSize.Width - _statusUrl.Width - 12;
        status.Controls.AddRange(new Control[] { _stateDot, _statusState, _statusUrl });

        Controls.Add(split);
        Controls.Add(toolbar);
        Controls.Add(header);
        Controls.Add(status);

        _list.SelectedIndexChanged += (_, _) => { UpdatePreview(); UpdateButtonStates(); };
        _list.DoubleClick += (_, _) => OnPlay(_list, EventArgs.Empty);

        _server.Router.Recorder.StateChanged += s => BeginInvoke(new Action(() =>
        {
            UpdateStateDisplay(s);
            UpdateButtonStates();
        }));

        Refresh();
        UpdateStateDisplay(_server.Router.Recorder.State);
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var hasSelection = _list.SelectedItems.Count > 0;
        var recording = _server.Router.Recorder.State == RecorderState.Recording;
        _btnRecord.Enabled  = !recording;
        _btnStop.Enabled    = recording;
        _btnPlay.Enabled    = hasSelection && !recording;
        _btnDelete.Enabled  = hasSelection && !recording;
        _btnRedo.Enabled    = _prePlaySnapshot != null && !recording;
        ApplyEnabledStyle(_btnRecord, RecRed);
        ApplyEnabledStyle(_btnStop,   StopGray);
        ApplyEnabledStyle(_btnPlay,   PlayGreen);
        ApplyEnabledStyle(_btnRedo,   Accent);
        ApplyEnabledStyle(_btnDelete, StopGray);
        UpdateStepButtonStates();
    }

    private void UpdateStepButtonStates()
    {
        var ok = _stepsList.SelectedItems.Count > 0;
        _btnStepDisable.Enabled = ok;
        _btnStepDelete.Enabled  = ok;
        _btnStepUp.Enabled      = ok && _stepsList.SelectedIndices[0] > 0;
        _btnStepDown.Enabled    = ok && _stepsList.SelectedIndices[0] < _stepsList.Items.Count - 1;
        ApplyEnabledStyle(_btnStepDisable, StopGray);
        ApplyEnabledStyle(_btnStepDelete,  StopGray);
        ApplyEnabledStyle(_btnStepUp,      StopGray);
        ApplyEnabledStyle(_btnStepDown,    StopGray);
    }

    private static void ApplyEnabledStyle(Button b, Color accent)
    {
        b.BackColor = b.Enabled ? accent : Color.FromArgb(60, 60, 64);
        b.ForeColor = b.Enabled ? Color.White : Color.FromArgb(120, 120, 128);
    }

    private static Icon? TryLoadAppIcon()
    {
        try { return SystemIcons.Application; } catch { return null; }
    }

    private static Button MakeSmallButton(string text, Color accent, EventHandler onClick)
    {
        var b = new Button
        {
            Text = text,
            Width = 78, Height = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = accent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(0, 0, 6, 0),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent, 0.15f);
        b.Click += onClick;
        return b;
    }

    private static Button MakeButton(string text, Color accent, EventHandler onClick)
    {
        var b = new Button
        {
            Text = text,
            Width = text.Length <= 4 ? 44 : 100,
            Height = 36,
            FlatStyle = FlatStyle.Flat,
            BackColor = accent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9.5f),
            Margin = new Padding(0, 0, 6, 0),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent, 0.15f);
        b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(accent, 0.1f);
        b.Click += onClick;
        return b;
    }

    public new void Refresh()
    {
        var prev = _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Text : null;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var name in _store.List())
        {
            var path = Path.Combine(_store.Root, name + ".json");
            int steps = 0;
            long size = 0;
            try { size = new FileInfo(path).Length; } catch { }
            try
            {
                using var fs = File.OpenRead(path);
                using var doc = JsonDocument.Parse(fs);
                if (doc.RootElement.TryGetProperty("Steps", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    steps = arr.GetArrayLength();
            }
            catch { }
            var item = new ListViewItem(new[] { name, steps.ToString(), FormatSize(size) });
            _list.Items.Add(item);
        }
        _list.EndUpdate();

        if (_list.Items.Count == 0) { UpdatePreview(); return; }
        var idx = 0;
        if (prev is not null)
        {
            for (int i = 0; i < _list.Items.Count; i++)
                if (_list.Items[i].Text == prev) { idx = i; break; }
        }
        _list.Items[idx].Selected = true;
        _list.Items[idx].Focused = true;
        UpdatePreview();
    }

    private static string FormatSize(long b)
        => b < 1024 ? $"{b} B"
         : b < 1024 * 1024 ? $"{b / 1024.0:F1} KB"
         : $"{b / (1024.0 * 1024):F1} MB";

    private void UpdatePreview()
    {
        _stepsList.BeginUpdate();
        _stepsList.Items.Clear();
        if (_list.SelectedItems.Count == 0)
        {
            _preview.Text = "(no macro selected)";
            _stepsList.EndUpdate();
            UpdateStepButtonStates();
            return;
        }
        var name = _list.SelectedItems[0].Text;
        try
        {
            var path = Path.Combine(_store.Root, name + ".json");
            var raw = File.ReadAllText(path);
            _preview.Text = PngRx.Replace(raw, m => $"{m.Groups[1].Value}<png base64 hidden>{m.Groups[2].Value}");

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("Steps", out var steps) && steps.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (var step in steps.EnumerateArray())
                {
                    var (label, detail) = SummarizeStep(step);
                    bool disabled = step.TryGetProperty("Disabled", out var d) && d.ValueKind == JsonValueKind.True;
                    var check = disabled ? "" : "✓";
                    var item = new ListViewItem(new[] { i.ToString(), check, label, detail });
                    if (disabled)
                    {
                        item.ForeColor = FgMuted;
                        item.Font = new Font(_stepsList.Font, FontStyle.Strikeout);
                    }
                    _stepsList.Items.Add(item);
                    i++;
                }
            }
        }
        catch (Exception ex)
        {
            _preview.Text = $"(failed to load: {ex.Message})";
        }
        _stepsList.EndUpdate();
        UpdateStepButtonStates();
    }

    // Translate a raw step JSON into a human-readable (action, detail) row.
    private static (string action, string detail) SummarizeStep(JsonElement step)
    {
        var op = step.TryGetProperty("Op", out var opEl) ? opEl.GetString() ?? "?" : "?";
        var args = step.TryGetProperty("Args", out var a) ? a : default;

        // History snapshot rows: pull the friendly name we already capture.
        if (op == "HISTORY /history/snapshot")
        {
            string name = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("name", out var n)
                ? n.GetString() ?? "" : "";
            string memento = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("memento", out var m)
                ? m.GetString() ?? "" : "";
            int layers = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("layers", out var l) && l.ValueKind == JsonValueKind.Array
                ? l.GetArrayLength() : 0;
            var label = string.IsNullOrEmpty(name) ? memento : name;
            return (label, $"{layers} layer snapshot{(layers > 1 ? "s" : "")} · {memento}");
        }

        // API calls: METHOD /path → keep them readable
        var space = op.IndexOf(' ');
        if (space > 0)
        {
            var method = op[..space];
            var path = op[(space + 1)..];
            return ($"{method} {path}", SummarizeArgs(args));
        }
        return (op, SummarizeArgs(args));
    }

    private static string SummarizeArgs(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object) return "";
        var parts = new System.Collections.Generic.List<string>();
        foreach (var p in args.EnumerateObject())
        {
            if (p.Name == "png" || p.Name == "layers") continue;
            string v = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString() ?? "",
                JsonValueKind.Number => p.Value.GetRawText(),
                JsonValueKind.True   => "true",
                JsonValueKind.False  => "false",
                JsonValueKind.Object => "{…}",
                JsonValueKind.Array  => "[…]",
                _ => ""
            };
            if (v.Length > 40) v = v[..37] + "…";
            parts.Add($"{p.Name}={v}");
            if (parts.Count >= 4) break;
        }
        return string.Join(", ", parts);
    }

    private void UpdateStateDisplay(RecorderState s)
    {
        if (s == RecorderState.Recording)
        {
            _stateDot.BackColor = RecRed;
            _statusState.Text = $"● Recording — {_server.Router.Recorder.CurrentName}";
            _statusState.ForeColor = RecRed;
        }
        else
        {
            _stateDot.BackColor = StopGray;
            _statusState.Text = "Idle";
            _statusState.ForeColor = FgMuted;
        }
        _stateDot.Invalidate();
    }

    private string? SelectedName()
    {
        if (_list.SelectedItems.Count > 0) return _list.SelectedItems[0].Text;
        if (_list.Items.Count == 0)
        {
            MessageBox.Show(this, "No macros yet — Record one first.", "Macro API",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }
        MessageBox.Show(this, "Select a macro in the list first.", "Macro API",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        return null;
    }

    private void OnRecord(object? sender, EventArgs e)
    {
        var name = Prompt("New recording", $"macro-{DateTime.Now:yyyyMMdd-HHmmss}");
        if (string.IsNullOrWhiteSpace(name)) return;
        _server.Router.Recorder.Start(name);

        _historyListener?.Dispose();
        _historyListener = new HistoryListener(_server.Bridge, _server.Router.Recorder);
        var ok = _historyListener.Attach();
        if (!ok)
        {
            _statusState.Text = $"● Recording — {name} (UI hook FAILED, API only)";
            _statusState.ForeColor = WarnAmber;
        }
    }

    private void OnStop(object? sender, EventArgs e)
    {
        if (_server.Router.Recorder.State != RecorderState.Recording) return;
        _historyListener?.Dispose();
        _historyListener = null;
        var macro = _server.Router.Recorder.Stop();
        _store.Save(macro);
        Refresh();
    }

    private async void OnPlay(object? sender, EventArgs e)
    {
        var n = SelectedName();
        if (n is null) return;
        var macro = _store.Load(n);
        if (macro is null) return;
        await PlayMacroAsync(macro, n);
    }

    private void OnDelete(object? sender, EventArgs e)
    {
        var n = SelectedName();
        if (n is null) return;
        if (MessageBox.Show(this, $"Delete macro '{n}'?", "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _store.Delete(n);
        Refresh();
    }

    private async void OnRedo(object? sender, EventArgs e)
    {
        if (_prePlaySnapshot is null || _prePlaySnapshot.Count == 0) return;
        _btnRedo.Enabled = false;
        _statusState.Text = "↶ Restoring pre-play state…";
        _statusState.ForeColor = Accent;
        try
        {
            foreach (var (idx, png) in _prePlaySnapshot)
            {
                var body = JsonSerializer.Serialize(new { png });
                var res = await _server.Router.DispatchAsync("POST", $"/layer/{idx}/replace", body);
                if (res.Status >= 400) throw new InvalidOperationException(res.Json);
            }
            _statusState.Text = "✓ Restored to pre-play state";
            _statusState.ForeColor = Accent;
            _prePlaySnapshot = null; // single-shot; next Play will refill
        }
        catch (Exception ex)
        {
            _statusState.Text = "✗ Undo failed";
            _statusState.ForeColor = RecRed;
            MessageBox.Show(this, ex.Message, "Undo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UpdateButtonStates(); }
    }

    private async System.Threading.Tasks.Task CaptureCurrentCanvasAsync()
    {
        _prePlaySnapshot = new System.Collections.Generic.List<(int, string)>();
        // Find layer count from /document
        var docRes = await _server.Router.DispatchAsync("GET", "/document", "");
        if (docRes.Status >= 400) return;
        try
        {
            using var doc = JsonDocument.Parse(docRes.Json);
            int layerCount = doc.RootElement.GetProperty("LayerCount").GetInt32();
            for (int i = 0; i < layerCount; i++)
            {
                var snap = await _server.Router.DispatchAsync("GET", $"/layer/{i}/snapshot", "");
                if (snap.Status >= 400) continue;
                using var snapDoc = JsonDocument.Parse(snap.Json);
                if (snapDoc.RootElement.TryGetProperty("png", out var pngEl))
                    _prePlaySnapshot.Add((i, pngEl.GetString() ?? ""));
            }
        }
        catch
        {
            _prePlaySnapshot = null;
        }
    }

    private void OnStepToggle()
    {
        ApplyToSelectedStep((idx, steps) =>
        {
            var s = steps[idx];
            steps[idx] = s with { Disabled = !s.Disabled };
        });
    }

    private void OnStepDelete()
    {
        ApplyToSelectedStep((idx, steps) => steps.RemoveAt(idx), keepIdx: true);
    }

    private void OnStepMove(int delta)
    {
        ApplyToSelectedStep((idx, steps) =>
        {
            var newIdx = idx + delta;
            if (newIdx < 0 || newIdx >= steps.Count) return;
            (steps[idx], steps[newIdx]) = (steps[newIdx], steps[idx]);
        }, moveSelection: delta);
    }

    private void ApplyToSelectedStep(Action<int, System.Collections.Generic.List<Core.MacroStep>> mutate,
                                      bool keepIdx = false, int moveSelection = 0)
    {
        var n = SelectedName();
        if (n is null) return;
        if (_stepsList.SelectedIndices.Count == 0) return;
        var stepIdx = _stepsList.SelectedIndices[0];
        var macro = _store.Load(n);
        if (macro is null) return;
        if (stepIdx < 0 || stepIdx >= macro.Steps.Count) return;
        mutate(stepIdx, macro.Steps);
        _store.Save(macro);
        Refresh();
        // Restore selection
        var newStepIdx = stepIdx + moveSelection;
        if (newStepIdx < 0) newStepIdx = 0;
        if (newStepIdx >= _stepsList.Items.Count) newStepIdx = _stepsList.Items.Count - 1;
        if (newStepIdx >= 0 && _stepsList.Items.Count > 0)
        {
            _stepsList.Items[newStepIdx].Selected = true;
            _stepsList.Items[newStepIdx].Focused = true;
        }
    }

    private async System.Threading.Tasks.Task PlayMacroAsync(Core.Macro macro, string name)
    {
        _btnPlay.Enabled = false;
        _btnRedo.Enabled = false;
        _statusState.Text = $"▶ Capturing pre-play state…";
        _statusState.ForeColor = PlayGreen;
        await CaptureCurrentCanvasAsync();

        _statusState.Text = $"▶ Playing {name} ({macro.Steps.Count} steps)…";
        try
        {
            await _player.PlayAsync(macro);
            _statusState.Text = $"✓ Played {name} — Undo available";
            _statusState.ForeColor = PlayGreen;
            _lastPlayedName = name;
        }
        catch (Exception ex)
        {
            _statusState.Text = "✗ Playback failed";
            _statusState.ForeColor = RecRed;
            MessageBox.Show(this, ex.Message, "Macro playback", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UpdateButtonStates();
        }
    }

    private void ToggleCompact()
    {
        if (!_isCompact)
        {
            _normalSize = Size;
            _normalBorder = FormBorderStyle;
            Hide();
            _compactBar?.Close();
            _compactBar = new CompactBar(this);
            _compactBar.Show();
            _isCompact = true;
        }
        else
        {
            _compactBar?.Close();
            _compactBar = null;
            FormBorderStyle = _normalBorder;
            Size = _normalSize == Size.Empty ? new Size(880, 560) : _normalSize;
            Show();
            BringToFront();
            _isCompact = false;
        }
    }

    public void ExitCompact() { if (_isCompact) ToggleCompact(); }

    public void TriggerPlay() => OnPlay(this, EventArgs.Empty);
    public void TriggerRecord() => OnRecord(this, EventArgs.Empty);
    public void TriggerStop() => OnStop(this, EventArgs.Empty);
    public string? CurrentMacroName => _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Text : null;
    public bool IsRecording => _server.Router.Recorder.State == RecorderState.Recording;

    private string? Prompt(string title, string defaultValue)
    {
        using var f = new Form
        {
            Text = title,
            Width = 380,
            Height = 160,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            BackColor = BgDark,
            ForeColor = FgPrimary,
            TopMost = true,
        };
        var lbl = new Label
        {
            Text = "Name:",
            Left = 16, Top = 18, Width = 60,
            ForeColor = FgMuted,
        };
        var tb = new TextBox
        {
            Left = 16, Top = 40, Width = 340,
            Text = defaultValue,
            BackColor = BgList,
            ForeColor = FgPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10f),
        };
        var ok = MakeButton("OK", Accent, (_, _) => { f.DialogResult = DialogResult.OK; f.Close(); });
        ok.Left = 180; ok.Top = 78; ok.Width = 80;
        var cancel = MakeButton("Cancel", StopGray, (_, _) => { f.DialogResult = DialogResult.Cancel; f.Close(); });
        cancel.Left = 270; cancel.Top = 78; cancel.Width = 80;
        f.Controls.AddRange(new Control[] { lbl, tb, ok, cancel });
        f.AcceptButton = ok;
        f.CancelButton = cancel;
        tb.SelectAll();
        return f.ShowDialog(this) == DialogResult.OK ? tb.Text : null;
    }
}
