using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PaintDotNet;
using PaintNetMacroApi.Core;

namespace PaintNetMacroApi.Api.Endpoints;

public sealed class LayerEndpoints : IEndpointGroup
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly PaintDotNetBridge _bridge;
    private static readonly Regex IndexRx   = new(@"^/layer/(\d+)$", RegexOptions.Compiled);
    private static readonly Regex MoveRx    = new(@"^/layer/(\d+)/move$", RegexOptions.Compiled);
    private static readonly Regex ReplaceRx = new(@"^/layer/(\d+)/replace$", RegexOptions.Compiled);
    private static readonly Regex SnapRx    = new(@"^/layer/(\d+)/snapshot$", RegexOptions.Compiled);

    public LayerEndpoints(PaintDotNetBridge bridge) => _bridge = bridge;

    public bool TryMatch(string method, string path, out Func<string, Task<ApiResult>> handler)
    {
        handler = null!;
        if (method == "GET" && path == "/layers") { handler = _ => List(); return true; }
        if (method == "POST" && path == "/layer") { handler = Add; return true; }

        var m = MoveRx.Match(path);
        if (m.Success && method == "POST")
        {
            var idx = int.Parse(m.Groups[1].Value);
            handler = body => Move(idx, body);
            return true;
        }

        m = ReplaceRx.Match(path);
        if (m.Success && method == "POST")
        {
            var idx = int.Parse(m.Groups[1].Value);
            handler = body => Replace(idx, body);
            return true;
        }

        m = SnapRx.Match(path);
        if (m.Success && method == "GET")
        {
            var idx = int.Parse(m.Groups[1].Value);
            handler = _ => Snapshot(idx);
            return true;
        }

        m = IndexRx.Match(path);
        if (m.Success)
        {
            var idx = int.Parse(m.Groups[1].Value);
            if (method == "DELETE") { handler = _ => Delete(idx); return true; }
            if (method == "PATCH") { handler = body => Patch(idx, body); return true; }
        }
        return false;
    }

    private Task<ApiResult> List() => UiInvoker.InvokeAsync(() =>
    {
        var doc = _bridge.GetActiveDocument();
        if (doc is null) return ApiResult.Bad("no active document");
        var list = new List<LayerInfo>();
        for (int i = 0; i < doc.Layers.Count; i++)
        {
            var layer = doc.Layers[i];
            var bl = layer as BitmapLayer;
            var blend = bl?.BlendMode.ToString() ?? "Normal";
            list.Add(new LayerInfo(i, layer.Name, layer.Visible, layer.Opacity,
                blend, layer.Width, layer.Height));
        }
        return ApiResult.Ok(list);
    });

    private Task<ApiResult> Add(string body) => UiInvoker.InvokeAsync(() =>
    {
        var doc = _bridge.GetActiveDocument();
        if (doc is null) return ApiResult.Bad("no active document");
        var req = JsonSerializer.Deserialize<AddLayerRequest>(body, Json) ?? new AddLayerRequest(null);
        var layer = new BitmapLayer(doc.Width, doc.Height) { Name = req.Name ?? $"Layer {doc.Layers.Count + 1}" };
        // TODO: must wrap this in a NewLayerHistoryMemento so Undo works;
        // currently modifies the document in-place without undo support.
        doc.Layers.Add(layer);
        return ApiResult.Ok(new { index = doc.Layers.Count - 1 });
    });

    private Task<ApiResult> Delete(int index) => UiInvoker.InvokeAsync(() =>
    {
        var doc = _bridge.GetActiveDocument();
        if (doc is null) return ApiResult.Bad("no active document");
        if (index < 0 || index >= doc.Layers.Count) return ApiResult.Bad("index out of range");
        if (doc.Layers.Count <= 1) return ApiResult.Bad("cannot delete the last layer");
        // TODO: DeleteLayerHistoryMemento for proper undo integration.
        doc.Layers.RemoveAt(index);
        return ApiResult.Ok();
    });

    private Task<ApiResult> Move(int index, string body) => UiInvoker.InvokeAsync(() =>
    {
        var doc = _bridge.GetActiveDocument();
        if (doc is null) return ApiResult.Bad("no active document");
        var req = JsonSerializer.Deserialize<MoveLayerRequest>(body, Json);
        if (req is null) return ApiResult.Bad("bad body");
        if (index < 0 || index >= doc.Layers.Count) return ApiResult.Bad("index out of range");
        if (req.To < 0 || req.To >= doc.Layers.Count) return ApiResult.Bad("target out of range");
        var layer = doc.Layers[index];
        doc.Layers.RemoveAt(index);
        doc.Layers.Insert(req.To, layer);
        return ApiResult.Ok();
    });

    private Task<ApiResult> Patch(int index, string body) => UiInvoker.InvokeAsync(() =>
    {
        var doc = _bridge.GetActiveDocument();
        if (doc is null) return ApiResult.Bad("no active document");
        if (index < 0 || index >= doc.Layers.Count) return ApiResult.Bad("index out of range");
        var req = JsonSerializer.Deserialize<PatchLayerRequest>(body, Json);
        if (req is null) return ApiResult.Bad("bad body");
        var layer = doc.Layers[index];
        if (req.Name is not null) layer.Name = req.Name;
        if (req.Visible is not null) layer.Visible = req.Visible.Value;
        if (req.Opacity is not null) layer.Opacity = (byte)Math.Clamp(req.Opacity.Value, 0, 255);
        if (req.BlendMode is not null && layer is BitmapLayer bl)
        {
            // TODO: map the blend string ("Normal", "Multiply", …) to a
            // UserBlendOp via UserBlendOps.GetBlendOps(). Requires runtime lookup.
        }
        return ApiResult.Ok();
    });

    private Task<ApiResult> Snapshot(int index) => UiInvoker.InvokeAsync(() =>
    {
        var doc = _bridge.GetActiveDocument();
        if (doc is null) return ApiResult.Bad("no active document");
        if (index < 0 || index >= doc.Layers.Count) return ApiResult.Bad("index out of range");
        if (doc.Layers[index] is not BitmapLayer bl) return ApiResult.Bad("not a bitmap layer");
        using var bmp = bl.Surface.CreateAliasedBitmap();
        using var ms = new System.IO.MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ApiResult.Ok(new { png = Convert.ToBase64String(ms.ToArray()) });
    });

    private Task<ApiResult> Replace(int index, string body) => UiInvoker.InvokeAsync(() =>
    {
        var doc = _bridge.GetActiveDocument();
        if (doc is null) return ApiResult.Bad("no active document");
        if (index < 0 || index >= doc.Layers.Count) return ApiResult.Bad("index out of range");
        if (doc.Layers[index] is not BitmapLayer bl) return ApiResult.Bad("not a bitmap layer");
        var req = JsonSerializer.Deserialize<ReplaceRequest>(body, Json);
        if (req is null || string.IsNullOrEmpty(req.Png)) return ApiResult.Bad("missing png");
        try
        {
            var bytes = Convert.FromBase64String(req.Png);
            using var ms = new System.IO.MemoryStream(bytes);
            using var img = System.Drawing.Image.FromStream(ms);
            using var bmp = new System.Drawing.Bitmap(img);
            var surf = bl.Surface;
            int w = Math.Min(surf.Width, bmp.Width);
            int h = Math.Min(surf.Height, bmp.Height);
            var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, w, h),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                unsafe
                {
                    for (int y = 0; y < h; y++)
                    {
                        var src = (byte*)data.Scan0 + y * data.Stride;
                        for (int x = 0; x < w; x++)
                        {
                            // ARGB in bitmap memory is stored BGRA on little-endian → matches ColorBgra layout
                            byte b = src[x * 4 + 0];
                            byte g = src[x * 4 + 1];
                            byte r = src[x * 4 + 2];
                            byte a = src[x * 4 + 3];
                            surf[x, y] = ColorBgra.FromBgra(b, g, r, a);
                        }
                    }
                }
            }
            finally { bmp.UnlockBits(data); }
            bl.Invalidate();
            _bridge.RefreshActiveDocument();
            return ApiResult.Ok();
        }
        catch (Exception ex) { return ApiResult.Bad("decode failed: " + ex.Message); }
    });

    private sealed record AddLayerRequest(string? Name);
    private sealed record MoveLayerRequest(int To);
    private sealed record PatchLayerRequest(string? Name, bool? Visible, int? Opacity, string? BlendMode);
    private sealed record ReplaceRequest(string Png);
}
