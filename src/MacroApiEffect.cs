using System;
using System.Drawing;
using PaintDotNet;
using PaintDotNet.Effects;
using PaintNetMacroApi.Api;
using PaintNetMacroApi.Core;
using PaintNetMacroApi.Ui;
// MacroApiToken lives in PaintNetMacroApi.Ui

namespace PaintNetMacroApi;

[PluginSupportInfo(typeof(MacroApiPluginSupportInfo))]
public sealed class MacroApiEffect : Effect
{
    private static ApiServer? _server;
    private static MacroWindow? _window;

    public MacroApiEffect()
        : base("Macro API", (Image?)null, "Tools", new EffectOptions { Flags = EffectFlags.Configurable })
    {
    }

    public override EffectConfigDialog CreateConfigDialog()
    {
        if (_server is null)
        {
            var bridge = new PaintDotNetBridge(this.Services);
            _server = new ApiServer(bridge, port: 8787);
            _server.Start();
        }

        if (_window is null || _window.IsDisposed)
            _window = new MacroWindow(_server);

        _window.Show();
        var dlg = new NoopDialog();
        dlg.EffectToken = new MacroApiToken();
        return dlg;
    }

    public override void Render(EffectConfigToken parameters, RenderArgs dstArgs, RenderArgs srcArgs,
                                Rectangle[] rois, int startIndex, int length)
    {
        for (int i = startIndex; i < startIndex + length; i++)
            dstArgs.Surface.CopySurface(srcArgs.Surface, rois[i].Location, rois[i]);
    }
}

public sealed class MacroApiPluginSupportInfo : IPluginSupportInfo
{
    public string Author      => "PaintNetMacroApi";
    public string Copyright   => "MIT";
    public string DisplayName => "Macro API";
    public Version Version    => typeof(MacroApiPluginSupportInfo).Assembly.GetName().Version!;
    public Uri WebsiteUri     => new Uri("https://localhost/");
}
