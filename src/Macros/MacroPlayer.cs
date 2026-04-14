using System;
using System.Threading.Tasks;
using PaintNetMacroApi.Api;
using PaintNetMacroApi.Core;

namespace PaintNetMacroApi.Macros;

public sealed class MacroPlayer
{
    private readonly Router _router;
    public MacroPlayer(Router router) => _router = router;

    public event Action<int, MacroStep>? StepStarting;
    public event Action<int, MacroStep, ApiResult>? StepFinished;

    public async Task<ApiResult> PlayStepAsync(MacroStep step)
    {
        var space = step.Op.IndexOf(' ');
        if (space <= 0) return ApiResult.Bad($"bad op: {step.Op}");
        var method = step.Op[..space];
        var path = step.Op[(space + 1)..];

        // History snapshot: blit each captured layer back via /layer/{i}/replace.
        if (method == "HISTORY" && path == "/history/snapshot")
        {
            if (step.Args.ValueKind != System.Text.Json.JsonValueKind.Object) return ApiResult.Ok();
            if (!step.Args.TryGetProperty("layers", out var layers) || layers.ValueKind != System.Text.Json.JsonValueKind.Array)
                return ApiResult.Ok();
            foreach (var entry in layers.EnumerateArray())
            {
                if (!entry.TryGetProperty("index", out var idxEl) || !entry.TryGetProperty("png", out var pngEl)) continue;
                if (idxEl.ValueKind != System.Text.Json.JsonValueKind.Number) continue;
                if (pngEl.ValueKind != System.Text.Json.JsonValueKind.String) continue;
                var idx = idxEl.GetInt32();
                var png = pngEl.GetString();
                if (string.IsNullOrEmpty(png)) continue;
                var body = System.Text.Json.JsonSerializer.Serialize(new { png });
                var res = await _router.DispatchAsync("POST", $"/layer/{idx}/replace", body).ConfigureAwait(false);
                if (res.Status >= 400) return res;
            }
            return ApiResult.Ok();
        }

        if (method == "HISTORY" || method == "INTERNAL")
        {
            // Other history entries are informational only; skip silently.
            return ApiResult.Ok();
        }

        var raw = step.Args.ValueKind == System.Text.Json.JsonValueKind.Undefined ? "" : step.Args.GetRawText();
        return await _router.DispatchAsync(method, path, raw).ConfigureAwait(false);
    }

    public async Task PlayAsync(Macro macro, bool stopOnError = true)
    {
        for (int i = 0; i < macro.Steps.Count; i++)
        {
            var step = macro.Steps[i];
            StepStarting?.Invoke(i, step);
            var res = await PlayStepAsync(step).ConfigureAwait(false);
            StepFinished?.Invoke(i, step, res);
            if (stopOnError && res.Status >= 400)
                throw new InvalidOperationException($"step {i} ({step.Op}) failed: {res.Json}");
        }
    }
}

// JsonValueKind shim — actual System.Text.Json.JsonValueKind.Undefined check
// depends on how JsonElement defaults; we alias to keep PlayStepAsync readable.
file static class JsonValueKind
{
    public const System.Text.Json.JsonValueKind Undefined = System.Text.Json.JsonValueKind.Undefined;
}
