using System;
using System.Drawing;
using System.Text.Json;
using System.Threading.Tasks;
using PaintNetMacroApi.Core;

namespace PaintNetMacroApi.Api.Endpoints;

public sealed class SelectionEndpoints : IEndpointGroup
{
    private readonly PaintDotNetBridge _bridge;
    public SelectionEndpoints(PaintDotNetBridge bridge) => _bridge = bridge;

    public bool TryMatch(string method, string path, out Func<string, Task<ApiResult>> handler)
    {
        handler = null!;
        switch ((method, path))
        {
            case ("POST", "/selection/rect"): handler = Rect; return true;
            case ("POST", "/selection/clear"): handler = _ => Clear(); return true;
            case ("POST", "/selection/invert"): handler = _ => Invert(); return true;
            case ("POST", "/selection/all"): handler = _ => All(); return true;
        }
        return false;
    }

    // TODO: ActiveDocumentWorkspace.Selection is internal in PDN 5.x; the public
    // surface moved through several refactors. All of these helpers go through
    // reflection until the SDK exposes a stable selection API.
    private object? GetSelection()
    {
        var dw = _bridge.GetActiveDocumentWorkspace();
        return dw?.GetType()
            .GetProperty("Selection",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(dw);
    }

    private Task<ApiResult> Rect(string body) => UiInvoker.InvokeAsync(() =>
    {
        var req = JsonSerializer.Deserialize<RectRequest>(body);
        if (req is null) return ApiResult.Bad("bad body");
        var sel = GetSelection();
        if (sel is null) return ApiResult.Bad("no selection object");
        // TODO: call Selection.PerformChanging(), Reset(), SetContinuation with
        // a rectangle via Selection.AddRectangle or PdnGraphicsPath, then PerformChanged().
        var rect = new Rectangle(req.X, req.Y, req.Width, req.Height);
        return ApiResult.Bad($"rect {rect} — selection API not wired yet");
    });

    private Task<ApiResult> Clear()
    {
        var sel = GetSelection();
        if (sel is null) return Task.FromResult(ApiResult.Bad("no selection object"));
        // TODO: Selection.Reset() or ActiveDocumentWorkspace.DeselectSelection().
        return Task.FromResult(ApiResult.Ok());
    }

    private Task<ApiResult> Invert()
    {
        // TODO: InvertSelectionFunction exists; invoke via
        // ActiveDocumentWorkspace.ExecuteFunction(new InvertSelectionFunction()).
        return Task.FromResult(ApiResult.Ok());
    }

    private Task<ApiResult> All()
    {
        // TODO: SelectAllFunction, same pattern as Invert.
        return Task.FromResult(ApiResult.Ok());
    }

    private sealed record RectRequest(int X, int Y, int Width, int Height);
}
