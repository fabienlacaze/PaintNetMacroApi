using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using PaintDotNet;
using PaintNetMacroApi.Core;

namespace PaintNetMacroApi.Api.Endpoints;

public sealed class DocumentEndpoints : IEndpointGroup
{
    private readonly PaintDotNetBridge _bridge;
    public DocumentEndpoints(PaintDotNetBridge bridge) => _bridge = bridge;

    public bool TryMatch(string method, string path, out Func<string, Task<ApiResult>> handler)
    {
        handler = null!;
        switch ((method, path))
        {
            case ("GET", "/document"): handler = _ => Get(); return true;
            case ("POST", "/document/new"): handler = New; return true;
            case ("POST", "/document/open"): handler = Open; return true;
            case ("POST", "/document/save"): handler = Save; return true;
            case ("POST", "/document/flatten"): handler = _ => Flatten(); return true;
        }
        return false;
    }

    private Task<ApiResult> Get() => UiInvoker.InvokeAsync(() =>
    {
        var doc = _bridge.GetActiveDocument();
        if (doc is null) return ApiResult.Bad("no active document");
        var info = new DocumentInfo(doc.Width, doc.Height, doc.Layers.Count,
            _bridge.GetActiveLayerIndex(), null, doc.DpuX, doc.DpuY);
        return ApiResult.Ok(info);
    });

    private Task<ApiResult> New(string body) => UiInvoker.InvokeAsync(() =>
    {
        var req = JsonSerializer.Deserialize<NewDocRequest>(body) ?? new NewDocRequest(800, 600);
        // TODO: AppWorkspace.CreateBlankDocumentInNewWorkspace is internal.
        // Reflect the method on the live AppWorkspace instance.
        var ws = _bridge.GetAppWorkspace();
        if (ws is null) return ApiResult.Bad("no AppWorkspace");
        var mi = ws.GetType().GetMethod("CreateBlankDocumentInNewWorkspace",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (mi is null) return ApiResult.Bad("CreateBlankDocumentInNewWorkspace not found");
        mi.Invoke(ws, new object[] { new System.Drawing.Size(req.Width, req.Height), new MeasurementUnit(), 96.0, true });
        return ApiResult.Ok();
    });

    private Task<ApiResult> Open(string body) => UiInvoker.InvokeAsync(() =>
    {
        var req = JsonSerializer.Deserialize<PathRequest>(body);
        if (req is null || string.IsNullOrWhiteSpace(req.Path)) return ApiResult.Bad("missing path");
        if (!File.Exists(req.Path)) return ApiResult.Bad("file not found");

        // TODO: DocumentWorkspace.OpenFile / AppWorkspace.OpenFileInNewWorkspace
        // are internal; signatures differ across versions. Reflect to call them.
        var ws = _bridge.GetAppWorkspace();
        var mi = ws?.GetType().GetMethod("OpenFileInNewWorkspace",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (ws is null || mi is null) return ApiResult.Bad("OpenFileInNewWorkspace not found");
        mi.Invoke(ws, new object[] { req.Path });
        return ApiResult.Ok();
    });

    private Task<ApiResult> Save(string body) => UiInvoker.InvokeAsync(() =>
    {
        var req = JsonSerializer.Deserialize<PathRequest>(body);
        if (req is null || string.IsNullOrWhiteSpace(req.Path)) return ApiResult.Bad("missing path");
        var doc = _bridge.GetActiveDocument();
        if (doc is null) return ApiResult.Bad("no active document");

        // TODO: Document.SaveToStream exists, but format selection (PDN/PNG/JPG)
        // uses FileType plugins resolved via FileTypes.GetFileTypes(). Add a real
        // format picker once the FileType API is confirmed for target PDN build.
        using var fs = File.Create(req.Path);
        doc.SaveToStream(fs);
        return ApiResult.Ok();
    });

    private Task<ApiResult> Flatten() => UiInvoker.InvokeAsync(() =>
    {
        var doc = _bridge.GetActiveDocument();
        if (doc is null) return ApiResult.Bad("no active document");
        // TODO: Document.Flatten returns a new Surface/BitmapLayer but does not
        // replace Document.Layers. A HistoryMemento must be pushed. Reflect on
        // ActiveDocumentWorkspace.ExecuteFunction with FlattenFunction.
        return ApiResult.Bad("flatten not yet wired — needs FlattenFunction");
    });

    private sealed record NewDocRequest(int Width, int Height);
    private sealed record PathRequest(string Path);
}
