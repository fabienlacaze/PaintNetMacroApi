using System;
using System.Drawing;
using System.Windows.Forms;

namespace PaintNetMacroApi.Ui;

// Tiny floating bar shown in "compact" mode. Always-on-top, draggable, exposes
// just the essential controls (Record / Play / Stop + current macro name).
public sealed class CompactBar : Form
{
    private static readonly Color BgDark    = Color.FromArgb(30, 30, 32);
    private static readonly Color BgPanel   = Color.FromArgb(40, 40, 44);
    private static readonly Color FgPrimary = Color.FromArgb(232, 232, 235);
    private static readonly Color FgMuted   = Color.FromArgb(160, 160, 168);
    private static readonly Color RecRed    = Color.FromArgb(220, 70, 70);
    private static readonly Color StopGray  = Color.FromArgb(110, 110, 120);
    private static readonly Color PlayGreen = Color.FromArgb(70, 170, 100);
    private static readonly Color Accent    = Color.FromArgb(80, 140, 240);

    private readonly MacroWindow _owner;
    private readonly Label _macroLabel;
    private readonly Button _record, _play, _stop, _expand;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    private bool _dragging;
    private Point _dragStart;

    public CompactBar(MacroWindow owner)
    {
        _owner = owner;
        Text = "Macro API";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        Width = 320;
        Height = 48;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(
            owner.Location.X + owner.Width - Width - 16,
            owner.Location.Y + 60);
        BackColor = BgPanel;
        Font = new Font("Segoe UI", 9f);

        _record = MakeMini("●", RecRed,    (_, _) => _owner.TriggerRecord());
        _stop   = MakeMini("■", StopGray,  (_, _) => _owner.TriggerStop());
        _play   = MakeMini("▶", PlayGreen, (_, _) => _owner.TriggerPlay());
        _expand = MakeMini("⊞", Accent,    (_, _) => _owner.ExitCompact());
        _record.Left = 6;     _record.Top = 8;
        _stop.Left   = 42;    _stop.Top = 8;
        _play.Left   = 78;    _play.Top = 8;

        _macroLabel = new Label
        {
            Left = 120, Top = 16, Width = 160, Height = 18,
            ForeColor = FgPrimary,
            BackColor = Color.Transparent,
            Text = "(no macro)",
            Font = new Font("Segoe UI", 9f),
            AutoEllipsis = true,
        };

        _expand.Left = Width - _expand.Width - 6;
        _expand.Top  = 8;
        _expand.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        Controls.AddRange(new Control[] { _record, _stop, _play, _macroLabel, _expand });

        // Drag anywhere except on buttons
        MouseDown += OnDragStart;
        MouseMove += OnDragMove;
        MouseUp   += OnDragEnd;
        _macroLabel.MouseDown += OnDragStart;
        _macroLabel.MouseMove += OnDragMove;
        _macroLabel.MouseUp   += OnDragEnd;

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 800 };
        _refreshTimer.Tick += (_, _) => RefreshState();
        _refreshTimer.Start();
        RefreshState();
    }

    private void RefreshState()
    {
        var rec = _owner.IsRecording;
        var name = _owner.CurrentMacroName ?? "(no macro)";
        _macroLabel.Text = rec ? "● recording…" : name;
        _macroLabel.ForeColor = rec ? RecRed : FgPrimary;
        _record.Enabled = !rec;
        _stop.Enabled   = rec;
        _play.Enabled   = !rec && _owner.CurrentMacroName != null;
        ApplyEnabled(_record, RecRed);
        ApplyEnabled(_stop,   StopGray);
        ApplyEnabled(_play,   PlayGreen);
    }

    private static Button MakeMini(string text, Color accent, EventHandler onClick)
    {
        var b = new Button
        {
            Text = text,
            Width = 32, Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = accent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11f),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += onClick;
        return b;
    }

    private static void ApplyEnabled(Button b, Color accent)
    {
        b.BackColor = b.Enabled ? accent : Color.FromArgb(60, 60, 64);
        b.ForeColor = b.Enabled ? Color.White : Color.FromArgb(120, 120, 128);
    }

    private void OnDragStart(object? s, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { _dragging = true; _dragStart = e.Location; }
    }
    private void OnDragMove(object? s, MouseEventArgs e)
    {
        if (!_dragging) return;
        Location = new Point(Cursor.Position.X - _dragStart.X, Cursor.Position.Y - _dragStart.Y);
    }
    private void OnDragEnd(object? s, MouseEventArgs e) => _dragging = false;

    protected override void Dispose(bool disposing)
    {
        if (disposing) _refreshTimer?.Dispose();
        base.Dispose(disposing);
    }
}
