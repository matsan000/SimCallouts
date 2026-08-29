using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SimCallouts
{
    /// <summary>Everything the dashboard page shows, snapshotted fresh on each request - see
    /// DashboardServer.GetStatus in MainForm.</summary>
    public sealed record DashboardStatus(
        bool SimConnected,
        string? FlightCallsign,
        string? FlightOrigin,
        string? FlightDest,
        double V1Kts,
        double RotateKts,
        bool ImportServerEnabled,
        IReadOnlyList<CalloutLogEntry> RecentCallouts);

    public sealed record CalloutLogEntry(DateTime TimeUtc, string Text);

    /// <summary>
    /// Optional localhost-only web dashboard - a read-only status view (connection, current
    /// flight, briefed V1/VR, recent callouts spoken) meant to be added as a Website App in
    /// RealEFB so SimCallouts' status is visible without alt-tabbing out to its own window. Off
    /// by default, same opt-in pattern as LocalImportServer; unlike that one this doesn't need
    /// to be loopback-only for security (nothing here can be triggered remotely, it's
    /// read-only), but stays 127.0.0.1-bound anyway since there's no reason for it to be
    /// reachable over the LAN. File-for-file the same as SimPrinter's own DashboardServer -
    /// deliberately kept in sync rather than shared, same as SimBriefClient in both projects.
    /// </summary>
    public sealed class DashboardServer : IDisposable
    {
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private int _port;

        public bool IsRunning => _listener != null;
        public int Port => _port;

        /// <summary>Called fresh for every GET /api/status - the caller (MainForm) owns all the
        /// actual state, this just snapshots it into a DTO on demand.</summary>
        public Func<DashboardStatus>? GetStatus { get; set; }

        /// <summary>Everything under /api/action/* and /api/settings - MainForm.DashboardApi.cs
        /// owns the actual routing/logic; this class only knows how to serve the static page and
        /// /api/status itself, and hands anything else off here. Returns false (unhandled) for a
        /// path this doesn't recognize, which becomes a 404.</summary>
        public Func<HttpListenerContext, Task<bool>>? OnApiRequest { get; set; }

        public void Start(int port)
        {
            if (_listener != null) return;

            _port = port;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
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

                _ = Task.Run(() => HandleRequestAsync(ctx));
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext ctx)
        {
            try
            {
                var path = ctx.Request.Url?.AbsolutePath ?? "/";

                if (path == "/api/status")
                {
                    var status = GetStatus?.Invoke();
                    var json = JsonSerializer.Serialize(status, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        // The dashboard's own name/description text is plain ASCII, but a
                        // callsign/registration/ICAO could in principle carry other characters -
                        // UnsafeRelaxedJsonEscaping keeps those readable instead of \uXXXX-
                        // escaped, same tradeoff RealEFB's own JSON responses make.
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    });
                    WriteResponse(ctx, 200, "application/json", json);
                }
                else if (path == "/" || path == "/index.html")
                {
                    WriteResponse(ctx, 200, "text/html; charset=utf-8", DashboardHtml.Page);
                }
                else if (OnApiRequest != null && await OnApiRequest(ctx))
                {
                    // Handled (and responded to) entirely by the callback - see
                    // MainForm.DashboardApi.cs.
                }
                else
                {
                    WriteResponse(ctx, 404, "text/plain", "Not found");
                }
            }
            catch
            {
                try { ctx.Response.StatusCode = 500; } catch { /* response already sent/closed */ }
                try { ctx.Response.Close(); } catch { /* already closed */ }
            }
        }

        public static void WriteResponse(HttpListenerContext ctx, int statusCode, string contentType, string body)
        {
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = contentType;
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        public void Dispose() => Stop();
    }
}
