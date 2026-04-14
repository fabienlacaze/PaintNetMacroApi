using System;
using System.Drawing;
using System.Text.Json;
using System.Threading.Tasks;
using PaintDotNet;
using PaintDotNet.Effects;
using PaintNetMacroApi.Core;

namespace PaintNetMacroApi.Api.Endpoints;

public sealed class EffectEndpoints : IEndpointGroup
{
    private readonly PaintDotNetBridge _bridge;
    public EffectEndpoints(PaintDotNetBridge bridge) => _bridge = bridge;

    public bool TryMatch(string method, string path, out Func<string, Task<ApiResult>> handler)
    {
        handler = null!;
        switch ((method, path))
        {
            case ("GET", "/effects"): handler = _ => List(); return true;
            case ("POST", "/effect/apply"): handler = Apply; return true;
        }
        return false;
    }

    private Task<ApiResult> List()
    {
        var effects = EffectRegistry.Enumerate();
        return Task.FromResult(ApiResult.Ok(effects));
    }

    private Task<ApiResult> Apply(string body) => UiInvoker.InvokeAsync(() =>
    {
        var req = JsonSerializer.Deserialize<ApplyRequest>(body);
        if (req is null || string.IsNullOrWhiteSpace(req.Type)) return ApiResult.Bad("missing type");

        var type = EffectRegistry.ResolveType(req.Type);
        if (type is null) return ApiResult.Bad($"effect type not found: {req.Type}");

        Effect? effect;
        try { effect = (Effect?)Activator.CreateInstance(type); }
        catch (Exception ex) { return ApiResult.Bad($"ctor failed: {ex.Message}"); }
        if (effect is null) return ApiResult.Bad("effect ctor returned null");

        var doc = _bridge.GetActiveDocument();
        var layer = _bridge.GetActiveLayer() as BitmapLayer;
        if (doc is null || layer is null) return ApiResult.Bad("need an active BitmapLayer");

        EffectConfigToken? token = null;
        if (effect is Effect<EffectConfigToken>)
        {
            // TODO: need the concrete token type. PDN stores it on the effect via
            // Effect.CreateDefaultConfigToken() (protected); call via reflection.
            var mi = type.GetMethod("CreateDefaultConfigToken",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var defaultToken = mi?.Invoke(effect, null) as EffectConfigToken;
            if (defaultToken is not null && req.Token.ValueKind == JsonValueKind.Object)
            {
                token = TokenSerializer.Deserialize(defaultToken.GetType(), req.Token) ?? defaultToken;
            }
            else token = defaultToken;
        }

        // TODO: PDN renders effects via BackgroundEffectRenderer with tile ROIs
        // and progress callbacks. For a synchronous macro replay we can call
        // Effect.SetRenderInfo + Render with a single ROI == layer.Bounds, but
        // this skips multi-threading and history. Real integration should push
        // an ApplyEffectHistoryMemento through ActiveDocumentWorkspace.
        try
        {
            var src = layer.Surface;
            using var dst = new Surface(src.Width, src.Height);
            dst.CopySurface(src);
            var srcArgs = new RenderArgs(src);
            var dstArgs = new RenderArgs(dst);
            var rois = new[] { new Rectangle(0, 0, src.Width, src.Height) };

            var setRender = typeof(Effect).GetMethod("SetRenderInfo",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            setRender?.Invoke(effect, new object?[] { token, dstArgs, srcArgs });

            effect.Render(token!, dstArgs, srcArgs, rois, 0, rois.Length);
            src.CopySurface(dst);
        }
        catch (Exception ex) { return ApiResult.Bad($"render failed: {ex.Message}"); }

        return ApiResult.Ok();
    });

    private sealed record ApplyRequest(string Type, JsonElement Token);
}
