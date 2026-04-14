using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using PaintNetMacroApi.Api.Endpoints;
using PaintNetMacroApi.Core;
using PaintNetMacroApi.Macros;

namespace PaintNetMacroApi.Api;

public readonly record struct ApiResult(int Status, string Json)
{
    public static ApiResult Ok(object? payload = null)
        => new(200, JsonSerializer.Serialize(payload ?? new { ok = true }));
    public static ApiResult NotFound()
        => new(404, "{\"error\":\"not found\"}");
    public static ApiResult Bad(string msg)
        => new(400, JsonSerializer.Serialize(new { error = msg }));
}

public sealed class Router
{
    private readonly PaintDotNetBridge _bridge;
    private readonly List<IEndpointGroup> _groups;

    public MacroRecorder Recorder { get; } = new();

    public Router(PaintDotNetBridge bridge)
    {
        _bridge = bridge;
        _groups = new List<IEndpointGroup>
        {
            new DocumentEndpoints(bridge),
            new LayerEndpoints(bridge),
            new SelectionEndpoints(bridge),
            new EffectEndpoints(bridge),
            new DrawEndpoints(bridge),
        };
    }

    public async Task<ApiResult> DispatchAsync(string method, string path, string body)
    {
        foreach (var g in _groups)
        {
            if (g.TryMatch(method, path, out var handler))
            {
                var call = new ApiCall(method, path, body);
                Recorder.Observe(call);
                return await handler(body).ConfigureAwait(false);
            }
        }
        return ApiResult.NotFound();
    }
}

public interface IEndpointGroup
{
    bool TryMatch(string method, string path, out Func<string, Task<ApiResult>> handler);
}
