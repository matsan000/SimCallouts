using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SimCallouts
{
    /// <summary>
    /// Routes for the web dashboard's action buttons and settings form (everything under
    /// /api/action/* and /api/settings - see DashboardServer.OnApiRequest). Every action here
    /// calls the exact same Execute*/_preferences members the native buttons call - see
    /// MainForm.cs - so the dashboard can never do anything the native UI couldn't already do.
    /// </summary>
    public partial class MainForm
    {
        // HttpListener serves each request on its own background thread, but every action here
        // ultimately touches WinForms Controls/fields (_currentPlan, _lblStatus, ...) that are
        // only safe to touch from the UI thread. BeginInvoke marshals the work there; the
        // TaskCompletionSource is what lets this method still be awaited for a result despite
        // BeginInvoke itself being fire-and-forget.
        private Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> func)
        {
            var tcs = new TaskCompletionSource<T>();
            BeginInvoke(new Action(async () =>
            {
                try { tcs.SetResult(await func()); }
                catch (Exception ex) { tcs.SetException(ex); }
            }));
            return tcs.Task;
        }

        private async Task<bool> HandleApiRequestAsync(HttpListenerContext ctx)
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "";
            var method = ctx.Request.HttpMethod;

            if (method == "POST" && path == "/api/action/import-flight")
            {
                var outcome = await RunOnUiThreadAsync(ExecuteImportFlightAsync);
                WriteOutcome(ctx, outcome);
                return true;
            }

            if (method == "POST" && path == "/api/action/departure-briefing")
            {
                var outcome = await RunOnUiThreadAsync(() => Task.FromResult(ExecuteDepartureBriefing()));
                WriteOutcome(ctx, outcome);
                return true;
            }

            if (method == "POST" && path == "/api/action/arrival-briefing")
            {
                var outcome = await RunOnUiThreadAsync(() => Task.FromResult(ExecuteArrivalBriefing()));
                WriteOutcome(ctx, outcome);
                return true;
            }

            if (method == "POST" && path == "/api/action/save-speeds")
            {
                var req = await ReadJsonBodyAsync<SaveSpeedsRequest>(ctx);
                if (req is null)
                {
                    DashboardServer.WriteResponse(ctx, 400, "application/json", """{"success":false,"message":"Invalid request body."}""");
                    return true;
                }
                var outcome = await RunOnUiThreadAsync(() => Task.FromResult(ExecuteSaveSpeeds(
                    req.V1Kts, req.RotateKts, req.ThrustReductionAltFt, req.AccelAltFt,
                    req.TransitionAltFt, req.TransitionLevelFt, req.MinimumsAglFt)));
                WriteOutcome(ctx, outcome);
                return true;
            }

            if (method == "GET" && path == "/api/settings")
            {
                var settings = await RunOnUiThreadAsync(() => Task.FromResult(BuildSettingsResponse()));
                DashboardServer.WriteResponse(ctx, 200, "application/json", JsonSerializer.Serialize(settings, JsonOpts));
                return true;
            }

            if (method == "POST" && path == "/api/settings")
            {
                var req = await ReadJsonBodyAsync<SettingsUpdateRequest>(ctx);
                if (req is null)
                {
                    DashboardServer.WriteResponse(ctx, 400, "application/json", """{"success":false,"message":"Invalid request body."}""");
                    return true;
                }
                var outcome = await RunOnUiThreadAsync(() => Task.FromResult(ApplySettingsUpdate(req)));
                WriteOutcome(ctx, outcome);
                // Deliberately AFTER the response above is fully written - ApplySettingsSideEffects
                // stops and restarts this very listener when the dashboard's own port/enabled
                // state changed, which would cut off this response if it ran first.
                if (outcome.Success)
                    BeginInvoke(new Action(ApplySettingsSideEffects));
                return true;
            }

            return false;
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private static void WriteOutcome(HttpListenerContext ctx, ActionOutcome outcome) =>
            DashboardServer.WriteResponse(ctx, 200, "application/json", JsonSerializer.Serialize(outcome, JsonOpts));

        private static async Task<T?> ReadJsonBodyAsync<T>(HttpListenerContext ctx)
        {
            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            string body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body)) return default;
            try { return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { return default; }
        }

        private sealed record SaveSpeedsRequest(
            double V1Kts, double RotateKts, double ThrustReductionAltFt, double AccelAltFt,
            double TransitionAltFt, double TransitionLevelFt, double MinimumsAglFt);

        // Mirrors every field ConfigForm's own Save button (plus the main screen's speeds Save)
        // writes to Preferences - the web settings form always submits all of them together
        // (like a normal HTML form), same as ConfigForm always writes every field off the
        // dialog's current UI state, so there's no partial-update case to handle here.
        public sealed record SettingsUpdateRequest(
            double V1Kts, double RotateKts, double ThrustReductionAltFt, double AccelAltFt,
            double TransitionAltFt, double TransitionLevelFt, double MinimumsAglFt,
            bool EnableV1, bool EnableRotate, bool EnablePositiveRate, bool EnableThrustReduction, bool EnableAccel,
            bool EnableTenThousandFt, bool EnableTransitionAltitude, bool EnableTransitionLevel,
            bool EnableEightyKnots, bool EnableHundredKnots, bool EnableOneThousandFeet, bool EnableFiveHundredFeet,
            bool EnableMinimums, string? VoiceName, int VolumePercent, bool UseRecordedSounds,
            bool UseElevenLabs, string? ElevenLabsApiKey, string? ElevenLabsVoiceId, bool DarkMode,
            string SimBriefId, bool EnableBrowserImport, bool EnableWebDashboard, int WebDashboardPort);

        // Same fields as the request above, plus the live installed-voices list (AvailableVoices)
        // the web form needs to render a proper dropdown.
        public sealed record SettingsResponse(
            double V1Kts, double RotateKts, double ThrustReductionAltFt, double AccelAltFt,
            double TransitionAltFt, double TransitionLevelFt, double MinimumsAglFt,
            bool EnableV1, bool EnableRotate, bool EnablePositiveRate, bool EnableThrustReduction, bool EnableAccel,
            bool EnableTenThousandFt, bool EnableTransitionAltitude, bool EnableTransitionLevel,
            bool EnableEightyKnots, bool EnableHundredKnots, bool EnableOneThousandFeet, bool EnableFiveHundredFeet,
            bool EnableMinimums, string? VoiceName, int VolumePercent, bool UseRecordedSounds,
            bool UseElevenLabs, string? ElevenLabsApiKey, string? ElevenLabsVoiceId, bool DarkMode,
            string SimBriefId, bool EnableBrowserImport, bool EnableWebDashboard, int WebDashboardPort,
            string[] AvailableVoices);

        private SettingsResponse BuildSettingsResponse() => new(
            V1Kts: _preferences.V1Kts, RotateKts: _preferences.RotateKts,
            ThrustReductionAltFt: _preferences.ThrustReductionAltFt, AccelAltFt: _preferences.AccelAltFt,
            TransitionAltFt: _preferences.TransitionAltFt, TransitionLevelFt: _preferences.TransitionLevelFt,
            MinimumsAglFt: _preferences.MinimumsAglFt,
            EnableV1: _preferences.EnableV1, EnableRotate: _preferences.EnableRotate,
            EnablePositiveRate: _preferences.EnablePositiveRate, EnableThrustReduction: _preferences.EnableThrustReduction,
            EnableAccel: _preferences.EnableAccel, EnableTenThousandFt: _preferences.EnableTenThousandFt,
            EnableTransitionAltitude: _preferences.EnableTransitionAltitude, EnableTransitionLevel: _preferences.EnableTransitionLevel,
            EnableEightyKnots: _preferences.EnableEightyKnots, EnableHundredKnots: _preferences.EnableHundredKnots,
            EnableOneThousandFeet: _preferences.EnableOneThousandFeet, EnableFiveHundredFeet: _preferences.EnableFiveHundredFeet,
            EnableMinimums: _preferences.EnableMinimums,
            VoiceName: _preferences.VoiceName, VolumePercent: _preferences.VolumePercent,
            UseRecordedSounds: _preferences.UseRecordedSounds, UseElevenLabs: _preferences.UseElevenLabs,
            ElevenLabsApiKey: _preferences.ElevenLabsApiKey, ElevenLabsVoiceId: _preferences.ElevenLabsVoiceId,
            DarkMode: _preferences.DarkMode, SimBriefId: _preferences.SimBriefId,
            EnableBrowserImport: _preferences.EnableBrowserImport, EnableWebDashboard: _preferences.EnableWebDashboard,
            WebDashboardPort: _preferences.WebDashboardPort,
            AvailableVoices: _speech.GetInstalledVoices().Select(v => v.VoiceInfo.Name).ToArray());

        /// <summary>Same restart-servers step ApplySettingsSideEffects() runs after a native
        /// save - the web form goes through the identical path, not a shortcut around it.</summary>
        private ActionOutcome ApplySettingsUpdate(SettingsUpdateRequest req)
        {
            if (req.WebDashboardPort is <= 0 or > 65535)
                return ActionOutcome.Fail("Dashboard port must be between 1 and 65535.");

            _preferences.V1Kts = req.V1Kts;
            _preferences.RotateKts = req.RotateKts;
            _preferences.ThrustReductionAltFt = req.ThrustReductionAltFt;
            _preferences.AccelAltFt = req.AccelAltFt;
            _preferences.TransitionAltFt = req.TransitionAltFt;
            _preferences.TransitionLevelFt = req.TransitionLevelFt;
            _preferences.MinimumsAglFt = req.MinimumsAglFt;
            _preferences.EnableV1 = req.EnableV1;
            _preferences.EnableRotate = req.EnableRotate;
            _preferences.EnablePositiveRate = req.EnablePositiveRate;
            _preferences.EnableThrustReduction = req.EnableThrustReduction;
            _preferences.EnableAccel = req.EnableAccel;
            _preferences.EnableTenThousandFt = req.EnableTenThousandFt;
            _preferences.EnableTransitionAltitude = req.EnableTransitionAltitude;
            _preferences.EnableTransitionLevel = req.EnableTransitionLevel;
            _preferences.EnableEightyKnots = req.EnableEightyKnots;
            _preferences.EnableHundredKnots = req.EnableHundredKnots;
            _preferences.EnableOneThousandFeet = req.EnableOneThousandFeet;
            _preferences.EnableFiveHundredFeet = req.EnableFiveHundredFeet;
            _preferences.EnableMinimums = req.EnableMinimums;
            _preferences.VoiceName = req.VoiceName;
            _preferences.VolumePercent = Math.Clamp(req.VolumePercent, 0, 200);
            _preferences.UseRecordedSounds = req.UseRecordedSounds;
            _preferences.UseElevenLabs = req.UseElevenLabs;
            _preferences.ElevenLabsApiKey = req.ElevenLabsApiKey?.Trim();
            _preferences.ElevenLabsVoiceId = req.ElevenLabsVoiceId?.Trim();
            _preferences.DarkMode = req.DarkMode;
            _preferences.SimBriefId = req.SimBriefId.Trim();
            _preferences.EnableBrowserImport = req.EnableBrowserImport;
            _preferences.EnableWebDashboard = req.EnableWebDashboard;
            _preferences.WebDashboardPort = req.WebDashboardPort;
            _preferences.Save();

            // Keeps the native window's own speeds fields in sync too - same reasoning as
            // ExecuteSaveSpeeds. ApplySettingsSideEffects() (restarting the print/dashboard
            // servers, re-applying tracker/voice config) is deliberately NOT called here - see
            // the caller in HandleApiRequestAsync for why.
            _txtV1.Text = req.V1Kts > 0 ? req.V1Kts.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
            _txtRotate.Text = req.RotateKts > 0 ? req.RotateKts.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
            _txtThrustReductionAlt.Text = req.ThrustReductionAltFt > 0 ? req.ThrustReductionAltFt.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
            _txtAccelAlt.Text = req.AccelAltFt > 0 ? req.AccelAltFt.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
            _txtTransitionAlt.Text = req.TransitionAltFt > 0 ? req.TransitionAltFt.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
            _txtTransitionLevel.Text = req.TransitionLevelFt > 0 ? req.TransitionLevelFt.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
            _txtMinimums.Text = req.MinimumsAglFt > 0 ? req.MinimumsAglFt.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";

            _lblStatus.ForeColor = UiStyle.SuccessColor;
            _lblStatus.Text = "Settings saved from web dashboard.";
            return ActionOutcome.Ok("Settings saved.");
        }
    }
}
