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
    private readonly Button _btnRecord, _btnStop, _btnPlay, _btnDelete, _btnNew, _btnRefresh, _btnFolder;

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

        // Toolbar (top)
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = BgPanel };
        _btnRecord  = MakeButton("●  Record",  RecRed,    OnRecord);
        _btnStop    = MakeButton("■  Stop",    StopGray,  OnStop);
        _btnPlay    = MakeButton("▶  Play",    PlayGreen, OnPlay);
        _btnNew     = MakeButton("+  New",     Accent,    OnNew);
        _btnDelete  = MakeButton("🗑  Delete",  StopGray,  OnDelete);
        _btnRefresh = MakeButton("⟳",          StopGray,  (_, _) => Refresh());
        _btnFolder  = MakeButton("📁",          StopGray,  (_, _) => Process.Start("explorer.exe", _store.Root));

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = BgPanel,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(10, 10, 10, 8),
            AutoSize = false,
        };
        bar.Controls.AddRange(new Control[] { _btnRecord, _btnStop, _btnPlay, _btnNew, _btnDelete, _btnRefresh, _btnFolder });
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
        _list.Columns.Add("Macro", 200);
        _list.Columns.Add("Steps", 60);
        _list.Columns.Add("Size", 80);

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

        var rightSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            BackColor = BgDark,
            SplitterWidth = 6,
            Orientation = Orientation.Horizontal,
        };
        rightSplit.Panel1.BackColor = BgDark;
        rightSplit.Panel2.BackColor = BgDark;

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
        _stepsList.Columns.Add("Action", 220);
        _stepsList.Columns.Add("Detail", 220);
        rightSplit.Panel1.Controls.Add(_stepsList);
        rightSplit.Panel1.Controls.Add(stepsHeader);

        var jsonHeader = new Label
        {
            Dock = DockStyle.Top, Height = 32,
            Text = "  Raw JSON (read-only — PNG payloads hidden)",
            ForeColor = FgMuted, BackColor = BgPanel,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 9.5f),
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
        };
        rightSplit.Panel2.Controls.Add(_preview);
        rightSplit.Panel2.Controls.Add(jsonHeader);

        split.Panel2.Controls.Add(rightSplit);
        split.Panel2.SizeChanged += (_, _) =>
        {
            var h = split.Panel2.ClientSize.Height;
            if (h > 100) rightSplit.SplitterDistance = Math.Max(120, h * 55 / 100);
        };

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
        Controls.Add(status);

        _list.SelectedIndexChanged += (_, _) => UpdatePreview();
        _list.DoubleClick += (_, _) => OnPlay(_list, EventArgs.Empty);

        _server.Router.Recorder.StateChanged += s => BeginInvoke(new Action(() => UpdateStateDisplay(s)));

        Shown += (_, _) =>
        {
            // After first show, drop TopMost so user can put PDN above us if desired.
            // We keep window above PDN at first launch but not pin-locked.
            BeginInvoke(new Action(() => TopMost = false));
        };

        Refresh();
        UpdateStateDisplay(_server.Router.Recorder.State);
    }

    private static Icon? TryLoadAppIcon()
    {
        try { return SystemIcons.Application; } catch { return null; }
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
                    var item = new ListViewItem(new[] { i.ToString(), label, detail });
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

        _btnPlay.Enabled = false;
        var prevText = _statusState.Text;
        _statusState.Text = $"▶ Playing {n} ({macro.Steps.Count} steps)…";
        _statusState.ForeColor = PlayGreen;
        try
        {
            await _player.PlayAsync(macro);
            _statusState.Text = $"✓ Played {n}";
            _statusState.ForeColor = PlayGreen;
        }
        catch (Exception ex)
        {
            _statusState.Text = "✗ Playback failed";
            _statusState.ForeColor = RecRed;
            MessageBox.Show(this, ex.Message, "Macro playback", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnPlay.Enabled = true;
        }
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

    private void OnNew(object? sender, EventArgs e)
    {
        var name = Prompt("New empty macro", "new-macro");
        if (string.IsNullOrWhiteSpace(name)) return;
        _store.Save(new Core.Macro(name, new()));
        Refresh();
    }

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
