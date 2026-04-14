using System;
using System.Reflection;
using PaintDotNet;
using PaintDotNet.AppModel;

namespace PaintNetMacroApi.Core;

// Wraps the IServiceProvider exposed by Effect.Services. Most of what we need
// (AppWorkspace, ActiveDocumentWorkspace) is NOT part of the public SDK —
// we reach for it via reflection. This will break across major Paint.NET
// versions; every method that reflects is marked with a TODO.
public sealed class PaintDotNetBridge
{
    private readonly IServiceProvider _services;

    public PaintDotNetBridge(IServiceProvider services)
    {
        _services = services;
        UiInvoker.Capture();
    }

    public IServiceProvider Services => _services;

    // TODO: IAppInfoService exists but does not expose AppWorkspace. In practice
    // we locate the main form via Application.OpenForms and reflect its
    // "AppWorkspace" property. Verify against target Paint.NET build.
    public object? GetAppWorkspace()
    {
        foreach (System.Windows.Forms.Form f in System.Windows.Forms.Application.OpenForms)
        {
            var prop = f.GetType().GetProperty("AppWorkspace",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop is not null) return prop.GetValue(f);
        }
        return null;
    }

    // TODO: ActiveDocumentWorkspace typically exposes .Document, .ActiveLayer,
    // .Selection, .History. These are internal to PaintDotNet.exe in most builds.
    public object? GetActiveDocumentWorkspace()
    {
        var ws = GetAppWorkspace();
        return ws?.GetType()
            .GetProperty("ActiveDocumentWorkspace",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(ws);
    }

    public Document? GetActiveDocument()
    {
        var dw = GetActiveDocumentWorkspace();
        return dw?.GetType()
            .GetProperty("Document",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(dw) as Document;
    }

    public Layer? GetActiveLayer()
    {
        var dw = GetActiveDocumentWorkspace();
        return dw?.GetType()
            .GetProperty("ActiveLayer",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(dw) as Layer;
    }

    public int GetActiveLayerIndex()
    {
        var doc = GetActiveDocument();
        var layer = GetActiveLayer();
        if (doc is null || layer is null) return -1;
        return doc.Layers.IndexOf(layer);
    }

    public object? GetHistoryStack()
    {
        var dw = GetActiveDocumentWorkspace();
        return dw?.GetType()
            .GetProperty("History", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(dw);
    }

    public void RefreshActiveDocument()
    {
        var dw = GetActiveDocumentWorkspace();
        var doc = GetActiveDocument();
        if (doc is null) return;
        try { doc.Invalidate(); } catch { }
        try
        {
            var refresh = dw?.GetType().GetMethod("Refresh",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, System.Type.EmptyTypes);
            refresh?.Invoke(dw, null);
        }
        catch { }
    }
}
