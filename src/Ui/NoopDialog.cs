using PaintDotNet.Effects;

namespace PaintNetMacroApi.Ui;

public sealed class MacroApiToken : EffectConfigToken
{
    public MacroApiToken() { }
    private MacroApiToken(MacroApiToken copy) : base(copy) { }
    public override object Clone() => new MacroApiToken(this);
}

public sealed class NoopDialog : EffectConfigDialog
{
    public NoopDialog()
    {
        this.ShowInTaskbar = false;
        this.Opacity = 0;
        this.Size = new System.Drawing.Size(1, 1);
    }

    protected override void InitialInitToken()
    {
        this.theEffectToken = new MacroApiToken();
    }

    protected override void InitTokenFromDialog() { }
    protected override void InitDialogFromToken(EffectConfigToken effectToken) { }

    protected override void OnLoad(System.EventArgs e)
    {
        base.OnLoad(e);
        // Cancel so Paint.NET's EffectMenuBase.RunEffectImpl does NOT try to
        // execute Render afterwards (which would crash with progressRegions=null).
        this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        this.Close();
    }
}
