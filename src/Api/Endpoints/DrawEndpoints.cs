using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PaintDotNet;
using PaintNetMacroApi.Core;

namespace PaintNetMacroApi.Api.Endpoints;

public sealed class DrawEndpoints : IEndpointGroup
{
    private readonly PaintDotNetBridge _bridge;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public DrawEndpoints(PaintDotNetBridge bridge) => _bridge = bridge;

    public bool TryMatch(string method, string path, out Func<string, Task<ApiResult>> handler)
    {
        handler = null!;
        if (method != "POST") return false;
        switch (path)
        {
            case "/draw/line":    handler = Line;    return true;
            case "/draw/rect":    handler = Rect;    return true;
            case "/draw/ellipse": handler = Ellipse; return true;
            case "/draw/pixels":  handler = Pixels;  return true;
        }
        return false;
    }

    private BitmapLayer? ActiveBitmap() => _bridge.GetActiveLayer() as BitmapLayer;

    private Task<ApiResult> Line(string body) => UiInvoker.InvokeAsync(() =>
    {
        var r = JsonSerializer.Deserialize<LineRequest>(body, Json);
        var layer = ActiveBitmap();
        if (r is null || layer is null) return ApiResult.Bad("need bitmap layer + body");
        DrawOnSurface(layer, g =>
        {
            using var pen = new Pen(ParseColor(r.Color), r.Width <= 0 ? 1f : r.Width);
            g.DrawLine(pen, r.X1, r.Y1, r.X2, r.Y2);
        });
        return ApiResult.Ok();
    });

    private Task<ApiResult> Rect(string body) => UiInvoker.InvokeAsync(() =>
    {
        var r = JsonSerializer.Deserialize<ShapeRequest>(body, Json);
        var layer = ActiveBitmap();
        if (r is null || layer is null) return ApiResult.Bad("need bitmap layer + body");
        DrawOnSurface(layer, g =>
        {
            var rect = new Rectangle(r.X, r.Y, r.W, r.H);
            if (r.Fill)
            { using var b = new SolidBrush(ParseColor(r.Color)); g.FillRectangle(b, rect); }
            else
            { using var p = new Pen(ParseColor(r.Color), r.StrokeWidth <= 0 ? 1f : r.StrokeWidth); g.DrawRectangle(p, rect); }
        });
        return ApiResult.Ok();
    });

    private Task<ApiResult> Ellipse(string body) => UiInvoker.InvokeAsync(() =>
    {
        var r = JsonSerializer.Deserialize<ShapeRequest>(body, Json);
        var layer = ActiveBitmap();
        if (r is null || layer is null) return ApiResult.Bad("need bitmap layer + body");
        DrawOnSurface(layer, g =>
        {
            var rect = new Rectangle(r.X, r.Y, r.W, r.H);
            if (r.Fill)
            { using var b = new SolidBrush(ParseColor(r.Color)); g.FillEllipse(b, rect); }
            else
            { using var p = new Pen(ParseColor(r.Color), r.StrokeWidth <= 0 ? 1f : r.StrokeWidth); g.DrawEllipse(p, rect); }
        });
        return ApiResult.Ok();
    });

    private Task<ApiResult> Pixels(string body) => UiInvoker.InvokeAsync(() =>
    {
        var r = JsonSerializer.Deserialize<PixelsRequest>(body, Json);
        var layer = ActiveBitmap();
        if (r is null || layer is null) return ApiResult.Bad("need bitmap layer + body");
        var surf = layer.Surface;
        foreach (var px in r.Pixels)
        {
            if (px.X < 0 || px.Y < 0 || px.X >= surf.Width || px.Y >= surf.Height) continue;
            surf[px.X, px.Y] = ColorBgra.FromBgra((byte)px.B, (byte)px.G, (byte)px.R, (byte)(px.A == 0 ? 255 : px.A));
        }
        layer.Invalidate();
        _bridge.RefreshActiveDocument();
        return ApiResult.Ok();
    });

    // Draws using GDI+ on a Bitmap, then blits back into the PDN surface (BGRA).
    private void DrawOnSurface(BitmapLayer layer, Action<Graphics> draw)
    {
        var surf = layer.Surface;
        using var bmp = surf.CreateAliasedBitmap();
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            draw(g);
        }
        // CreateAliasedBitmap shares memory with the surface, so the draw is already
        // visible. Just invalidate and refresh.
        layer.Invalidate();
        _bridge.RefreshActiveDocument();
    }

    private static Color ParseColor(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return Color.Black;
        s = s.Trim();
        if (s.StartsWith("#")) s = s[1..];
        if (s.Length == 6 && uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return Color.FromArgb(255, (int)((rgb >> 16) & 0xFF), (int)((rgb >> 8) & 0xFF), (int)(rgb & 0xFF));
        if (s.Length == 8 && uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var argb))
            return Color.FromArgb((int)((argb >> 24) & 0xFF), (int)((argb >> 16) & 0xFF), (int)((argb >> 8) & 0xFF), (int)(argb & 0xFF));
        try { return ColorTranslator.FromHtml(s); } catch { return Color.Black; }
    }

    private sealed record LineRequest(int X1, int Y1, int X2, int Y2, float Width, string? Color);
    private sealed record ShapeRequest(int X, int Y, int W, int H, bool Fill, float StrokeWidth, string? Color);
    private sealed record PixelRef(int X, int Y, int R, int G, int B, int A);
    private sealed record PixelsRequest(PixelRef[] Pixels);
}
