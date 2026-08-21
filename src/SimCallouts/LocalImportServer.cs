using System.Net;
using System.Text;

namespace SimCallouts
{
    /// <summary>
    /// Minimal localhost-only HTTP server that receives SimBrief takeoff performance text
    /// forwarded by SimPrinter (see SimPrinter's LocalPrintServer, which relays whatever its
    /// browser extension posts to it on to here). Not reachable from the network, only from
    /// processes on this machine. Opt-in via Settings, off by default.
    /// </summary>
    public sealed class LocalImportServer : IDisposable
    {
        // One above SimPrinter's LocalPrintServer.Port (39901) - the extension already talks
        // exclusively to that port, so this app can't bind it too; SimPrinter relays here instead.
        public const int Port = 39902;

        private HttpListener? _listener;
        private CancellationTokenSource? _cts;

        /// <summary>Raised with the request body whenever POST /import-text is received.</summary>
        public Action<string>? OnTextReceived { get; set; }

        public void Start()
        {
            if (_listener != null) return;

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ListenLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
            _listener = null;
            _cts = null;
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener!.GetContextAsync();
                }
                catch
                {
                    break; // listener was stopped/disposed
                }

                _ = Task.Run(() => HandleRequest(ctx));
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
                ctx.Response.Headers["Access-Control-Allow-Methods"] = "POST, OPTIONS";
                ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";

                if (ctx.Request.HttpMethod == "OPTIONS")
                {
                    ctx.Response.StatusCode = 204;
                    return;
                }

                if (ctx.Request.HttpMethod == "POST" && ctx.Request.Url?.AbsolutePath == "/import-text")
                {
                    using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                    string body = reader.ReadToEnd();

                    if (string.IsNullOrWhiteSpace(body))
                    {
                        ctx.Response.StatusCode = 400;
                        return;
                    }

                    OnTextReceived?.Invoke(body);
                    ctx.Response.StatusCode = 200;
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                }
            }
            catch
            {
                try { ctx.Response.StatusCode = 500; } catch { /* response already sent/closed */ }
            }
            finally
            {
                ctx.Response.Close();
            }
        }

        public void Dispose() => Stop();
    }
}
