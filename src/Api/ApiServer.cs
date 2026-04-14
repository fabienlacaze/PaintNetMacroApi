using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PaintNetMacroApi.Core;

namespace PaintNetMacroApi.Api;

public sealed class ApiServer
{
    private readonly HttpListener _listener = new();
    private readonly Router _router;
    private readonly PaintDotNetBridge _bridge;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public ApiServer(PaintDotNetBridge bridge, int port)
    {
        _bridge = bridge;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _router = new Router(bridge);
    }

    public Router Router => _router;
    public PaintDotNetBridge Bridge => _bridge;

    public void Start()
    {
        _listener.Start();
        _loop = Task.Run(AcceptLoop);
    }

    public void Stop()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
    }

    private async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync().ConfigureAwait(false); }
            catch { break; }

            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            string body;
            using (var r = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                body = await r.ReadToEndAsync().ConfigureAwait(false);

            var result = await _router.DispatchAsync(
                ctx.Request.HttpMethod,
                ctx.Request.Url!.AbsolutePath,
                body).ConfigureAwait(false);

            ctx.Response.StatusCode = result.Status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            var bytes = Encoding.UTF8.GetBytes(result.Json);
            await ctx.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            var err = JsonSerializer.Serialize(new { error = ex.Message });
            var bytes = Encoding.UTF8.GetBytes(err);
            await ctx.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        }
        finally
        {
            ctx.Response.OutputStream.Close();
        }
    }
}
