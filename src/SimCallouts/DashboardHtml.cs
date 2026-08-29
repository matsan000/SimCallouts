namespace SimCallouts
{
    /// <summary>The single static page DashboardServer serves at "/" - self-contained (inline
    /// CSS/JS, no separate files to publish/copy) since it's small enough that a whole wwwroot
    /// folder would be more ceremony than the page itself. Status polls GET /api/status every 2s
    /// rather than pushing updates - simplest thing that works for a status view nobody's
    /// staring at pixel-by-pixel. Every button here posts to the same /api/action/* routes the
    /// native buttons ultimately call into (see MainForm.DashboardApi.cs) - nothing on this page
    /// can do anything the native app couldn't already do. Same visual language as SimPrinter's
    /// own dashboard - deliberately kept in sync rather than shared, same as SimBriefClient in
    /// both projects.</summary>
    internal static class DashboardHtml
    {
        public const string Page = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8" />
<title>SimCallouts</title>
<meta name="viewport" content="width=device-width, initial-scale=1.0" />
<style>
  :root {
    --bg: #f4f5f7;
    --card: #fff;
    --border: #e0e2e6;
    --text: #212529;
    --muted: #6c757d;
    --accent: #2563eb;
    --accent-hover: #1d4ed8;
    --success: #1a7431;
    --error: #b91c1c;
  }
  * { box-sizing: border-box; }
  body {
    margin: 0;
    background: var(--bg);
    color: var(--text);
    font-family: "Segoe UI", system-ui, sans-serif;
    padding: 20px;
  }
  header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 18px;
  }
  h1 { font-size: 20px; margin: 0; }
  .pill {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 5px 12px;
    border-radius: 999px;
    font-size: 13px;
    font-weight: 600;
  }
  .pill-dot { width: 8px; height: 8px; border-radius: 50%; }
  .pill.connected { background: rgba(26, 116, 49, 0.12); color: var(--success); }
  .pill.connected .pill-dot { background: var(--success); }
  .pill.disconnected { background: rgba(185, 28, 28, 0.1); color: var(--error); }
  .pill.disconnected .pill-dot { background: var(--error); }
  .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; margin-bottom: 14px; }
  @media (max-width: 560px) { .grid { grid-template-columns: 1fr; } }
  .card {
    background: var(--card);
    border: 1px solid var(--border);
    border-radius: 14px;
    padding: 16px 18px;
  }
  .card h2 { font-size: 13px; text-transform: uppercase; letter-spacing: 0.03em; color: var(--muted); margin: 0 0 10px; }
  .card-header-row { display: flex; align-items: center; justify-content: space-between; cursor: pointer; }
  .card-header-row h2 { margin: 0; }
  .chevron { color: var(--muted); transition: transform 0.15s ease; font-size: 12px; }
  .collapsed .chevron { transform: rotate(-90deg); }
  .collapsible-body { margin-top: 10px; }
  .collapsed .collapsible-body { display: none; }
  .flight-callsign { font-size: 18px; font-weight: 700; }
  .flight-route { color: var(--muted); font-size: 14px; margin-top: 2px; }
  .vspeed-row { display: flex; justify-content: space-around; gap: 8px; }
  .vspeed-item { text-align: center; flex: 1; }
  .vspeed-label { font-size: 11px; color: var(--muted); text-transform: uppercase; }
  .vspeed-value { font-size: 20px; font-weight: 700; font-variant-numeric: tabular-nums; }
  .status-line { font-size: 14px; }
  .status-line.on { color: var(--success); font-weight: 600; }
  .status-line.off { color: var(--muted); }
  .callout-list { list-style: none; margin: 0; padding: 0; max-height: 220px; overflow-y: auto; }
  .callout-item { display: flex; justify-content: space-between; gap: 10px; padding: 7px 0; border-bottom: 1px solid var(--border); font-size: 13px; }
  .callout-item:last-child { border-bottom: none; }
  .callout-item .time { color: var(--muted); white-space: nowrap; }
  .empty { color: var(--muted); font-size: 13px; }
  .full-width { grid-column: 1 / -1; }

  label.field-label { display: block; font-size: 12px; font-weight: 700; color: var(--muted); text-transform: uppercase; letter-spacing: 0.02em; margin: 12px 0 4px; }
  label.field-label:first-child { margin-top: 0; }
  input[type=text], input[type=number], input[type=password], select {
    width: 100%;
    padding: 9px 10px;
    border: 1px solid var(--border);
    border-radius: 8px;
    font-size: 14px;
    font-family: inherit;
    color: var(--text);
    background: #fff;
  }
  .btn {
    padding: 9px 16px;
    border-radius: 8px;
    border: none;
    background: var(--accent);
    color: #fff;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
  }
  .btn:hover { background: var(--accent-hover); }
  .btn:disabled { opacity: 0.5; cursor: default; }
  .btn-secondary { background: #eef1f6; color: var(--accent); border: 1px solid var(--border); }
  .btn-secondary:hover { background: #e2e6ec; }
  .btn-row { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 10px; }
  .btn-row .btn { flex: 1 1 auto; }
  .action-msg { font-size: 13px; margin-top: 10px; min-height: 18px; }
  .action-msg.ok { color: var(--success); }
  .action-msg.err { color: var(--error); }
  .speeds-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0 14px; }
  .toggle-row { display: flex; align-items: center; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid var(--border); }
  .toggle-row:last-child { border-bottom: none; }
  .toggle-row span { font-size: 14px; }
  .switch { position: relative; width: 40px; height: 22px; flex-shrink: 0; }
  .switch input { opacity: 0; width: 0; height: 0; }
  .slider { position: absolute; inset: 0; background: #cbd5e1; border-radius: 999px; cursor: pointer; transition: 0.15s; }
  .slider::before { content: ""; position: absolute; width: 16px; height: 16px; left: 3px; top: 3px; background: #fff; border-radius: 50%; transition: 0.15s; }
  input:checked + .slider { background: var(--accent); }
  input:checked + .slider::before { transform: translateX(18px); }
  .settings-note { font-size: 12px; color: var(--muted); margin-top: 4px; }
  .settings-save-row { display: flex; align-items: center; gap: 12px; margin-top: 16px; }
  .settings-subhead { font-size: 12px; font-weight: 700; color: var(--accent); text-transform: uppercase; letter-spacing: 0.02em; margin: 18px 0 4px; border-top: 1px solid var(--border); padding-top: 14px; }
  .settings-subhead:first-child { border-top: none; padding-top: 0; margin-top: 0; }
</style>
</head>
<body>
  <header>
    <h1>SimCallouts</h1>
    <span class="pill disconnected" id="conn-pill"><span class="pill-dot"></span><span id="conn-text">Checking...</span></span>
  </header>

  <div class="grid">
    <div class="card">
      <h2>Current Flight</h2>
      <div id="flight-body"><p class="empty">No flight loaded.</p></div>
    </div>
    <div class="card">
      <h2>Briefed Speeds</h2>
      <div class="vspeed-row">
        <div class="vspeed-item"><div class="vspeed-label">V1</div><div class="vspeed-value" id="v1-value">--</div></div>
        <div class="vspeed-item"><div class="vspeed-label">Rotate</div><div class="vspeed-value" id="vr-value">--</div></div>
      </div>
    </div>
    <div class="card">
      <h2>Browser Import</h2>
      <p class="status-line" id="import-server-status">Checking...</p>
    </div>
    <div class="card full-width">
      <h2>Recent Callouts</h2>
      <ul class="callout-list" id="callout-list"><li class="empty">Nothing called out yet this session.</li></ul>
    </div>

    <div class="card full-width">
      <h2>Actions</h2>

      <button class="btn" id="btn-import-flight" style="width:100%">Import Flight from SimBrief</button>
      <div class="btn-row">
        <button class="btn btn-secondary" id="btn-departure-briefing" disabled>Departure Briefing</button>
        <button class="btn btn-secondary" id="btn-arrival-briefing" disabled>Arrival Briefing</button>
      </div>

      <div class="settings-subhead">Briefed Speeds/Altitudes</div>
      <div class="speeds-grid">
        <div id="sp-field-v1"><label class="field-label">V1 (kts)</label><input type="number" id="sp-v1" /></div>
        <div id="sp-field-vr"><label class="field-label">Rotate (kts)</label><input type="number" id="sp-vr" /></div>
        <div id="sp-field-thrust"><label class="field-label">Thrust Reduction Alt (ft)</label><input type="number" id="sp-thrust" /></div>
        <div id="sp-field-accel"><label class="field-label">Accel Alt (ft)</label><input type="number" id="sp-accel" /></div>
        <div id="sp-field-transalt"><label class="field-label">Transition Alt (ft)</label><input type="number" id="sp-transalt" /></div>
        <div id="sp-field-translevel"><label class="field-label">Transition Level (ft)</label><input type="number" id="sp-translevel" /></div>
        <div id="sp-field-minimums"><label class="field-label">Minimums AGL (ft)</label><input type="number" id="sp-minimums" /></div>
      </div>
      <p class="settings-note" id="speeds-hint" style="display:none">Fields for disabled callouts are hidden here - turn them on in Settings below to edit them.</p>
      <div class="btn-row">
        <button class="btn btn-secondary" id="btn-save-speeds">Save Speeds</button>
      </div>

      <p class="action-msg" id="action-msg"></p>
    </div>

    <div class="card full-width">
      <div class="card-header-row" id="settings-toggle">
        <h2>Settings</h2>
        <span class="chevron">&#9660;</span>
      </div>
      <div class="collapsible-body" id="settings-body">
        <label class="field-label">SimBrief Username or Pilot ID</label>
        <input type="text" id="set-simbrief-id" />

        <div class="settings-subhead">Voice</div>
        <label class="field-label">Voice</label>
        <select id="set-voice"></select>
        <label class="field-label">Volume (%)</label>
        <input type="number" id="set-volume" min="0" max="200" />
        <div class="toggle-row">
          <span>Use recorded sound files for callouts</span>
          <label class="switch"><input type="checkbox" id="set-recorded-sounds" /><span class="slider"></span></label>
        </div>
        <div class="toggle-row">
          <span>Use ElevenLabs for callouts and briefings</span>
          <label class="switch"><input type="checkbox" id="set-elevenlabs" /><span class="slider"></span></label>
        </div>
        <label class="field-label">ElevenLabs API Key</label>
        <input type="password" id="set-elevenlabs-key" />
        <label class="field-label">ElevenLabs Voice ID</label>
        <input type="text" id="set-elevenlabs-voice" />

        <div class="settings-subhead">Callouts Enabled</div>
        <div class="toggle-row"><span>V1</span><label class="switch"><input type="checkbox" id="set-en-v1" /><span class="slider"></span></label></div>
        <div class="toggle-row"><span>Rotate</span><label class="switch"><input type="checkbox" id="set-en-rotate" /><span class="slider"></span></label></div>
        <div class="toggle-row"><span>Positive Rate</span><label class="switch"><input type="checkbox" id="set-en-positiverate" /><span class="slider"></span></label></div>
        <div class="toggle-row"><span>Thrust Reduction</span><label class="switch"><input type="checkbox" id="set-en-thrust" /><span class="slider"></span></label></div>
        <div class="toggle-row"><span>Bug Up (Accel)</span><label class="switch"><input type="checkbox" id="set-en-accel" /><span class="slider"></span></label></div>
        <div class="toggle-row"><span>10,000 feet</span><label class="switch"><input type="checkbox" id="set-en-tenk" /><span class="slider"></span></label></div>
        <div class="toggle-row"><span>Transition Altitude</span><label class="switch"><input type="checkbox" id="set-en-transalt" /><span class="slider"></span></label></div>
        <div class="toggle-row"><span>Transition Level</span><label class="switch"><input type="checkbox" id="set-en-translevel" /><span class="slider"></span></label></div>
        <div class="toggle-row"><span>80 knots</span><label class="switch"><input type="checkbox" id="set-en-80kt" /><span class="slider"></span></label></div>
        <div class="toggle-row"><span>100 knots</span><label class="switch"><input type="checkbox" id="set-en-100kt" /><span class="slider"></span></label></div>
        <div class="toggle-row"><span>1,000 feet (approach)</span><label class="switch"><input type="checkbox" id="set-en-1000ft" /><span class="slider"></span></label></div>
        <div class="toggle-row"><span>500 feet (approach)</span><label class="switch"><input type="checkbox" id="set-en-500ft" /><span class="slider"></span></label></div>
        <div class="toggle-row"><span>Minimums</span><label class="switch"><input type="checkbox" id="set-en-minimums" /><span class="slider"></span></label></div>

        <div class="settings-subhead">General</div>
        <div class="toggle-row">
          <span>Auto-fill V1/Rotate from SimBrief performance calculations</span>
          <label class="switch"><input type="checkbox" id="set-browser-import" /><span class="slider"></span></label>
        </div>
        <div class="toggle-row">
          <span>Dark theme (restart SimCallouts to apply)</span>
          <label class="switch"><input type="checkbox" id="set-dark-mode" /><span class="slider"></span></label>
        </div>
        <div class="toggle-row">
          <span>Enable this web dashboard</span>
          <label class="switch"><input type="checkbox" id="set-dashboard-enabled" /><span class="slider"></span></label>
        </div>
        <label class="field-label">Dashboard Port</label>
        <input type="number" id="set-dashboard-port" min="1" max="65535" />
        <p class="settings-note">Changing the port or disabling the dashboard takes effect immediately - reload this page at the new address afterward.</p>

        <div class="settings-save-row">
          <button class="btn" id="btn-save-settings">Save Settings</button>
          <span class="action-msg" id="settings-msg" style="margin:0"></span>
        </div>
      </div>
    </div>
  </div>

<script>
  function fmtTime(iso) {
    const d = new Date(iso);
    return d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" });
  }

  // ============================== Status polling ==============================

  async function refreshStatus() {
    let s;
    try {
      const res = await fetch("/api/status", { cache: "no-store" });
      if (!res.ok) throw new Error();
      s = await res.json();
    } catch {
      document.getElementById("conn-pill").className = "pill disconnected";
      document.getElementById("conn-text").textContent = "Dashboard unreachable";
      return;
    }

    const pill = document.getElementById("conn-pill");
    pill.className = "pill " + (s.simConnected ? "connected" : "disconnected");
    document.getElementById("conn-text").textContent = s.simConnected ? "Connected to MSFS" : "Not connected";

    const flightBody = document.getElementById("flight-body");
    const hasFlight = !!s.flightCallsign;
    flightBody.innerHTML = hasFlight
      ? `<div class="flight-callsign">${s.flightCallsign}</div><div class="flight-route">${s.flightOrigin || "----"} &rarr; ${s.flightDest || "----"}</div>`
      : `<p class="empty">No flight loaded.</p>`;

    document.getElementById("btn-departure-briefing").disabled = !hasFlight;
    document.getElementById("btn-arrival-briefing").disabled = !hasFlight;

    document.getElementById("v1-value").textContent = s.v1Kts > 0 ? Math.round(s.v1Kts) : "--";
    document.getElementById("vr-value").textContent = s.rotateKts > 0 ? Math.round(s.rotateKts) : "--";

    const importEl = document.getElementById("import-server-status");
    importEl.textContent = s.importServerEnabled ? "Enabled - listening for SimPrinter's browser extension relay." : "Disabled - turn on in Settings to auto-fill V1/VR from SimBrief.";
    importEl.className = "status-line " + (s.importServerEnabled ? "on" : "off");

    const list = document.getElementById("callout-list");
    if (!s.recentCallouts || s.recentCallouts.length === 0) {
      list.innerHTML = `<li class="empty">Nothing called out yet this session.</li>`;
    } else {
      list.innerHTML = s.recentCallouts
        .map((c) => `<li class="callout-item"><span>${c.text}</span><span class="time">${fmtTime(c.timeUtc)}</span></li>`)
        .join("");
    }
  }

  refreshStatus();
  setInterval(refreshStatus, 2000);

  // ============================== Actions ==============================

  function showActionMsg(text, ok) {
    const el = document.getElementById("action-msg");
    el.textContent = text;
    el.className = "action-msg " + (ok ? "ok" : "err");
  }

  async function runAction(url, body, btn) {
    if (btn) btn.disabled = true;
    showActionMsg("Working...", true);
    try {
      const res = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body || {}),
      });
      const result = await res.json().catch(() => ({ success: false, message: "Unexpected response." }));
      showActionMsg(result.message || (result.success ? "Done." : "Failed."), result.success);
    } catch {
      showActionMsg("Could not reach SimCallouts.", false);
    } finally {
      if (btn) btn.disabled = false;
      refreshStatus();
    }
  }

  document.getElementById("btn-import-flight").addEventListener("click", (e) => runAction("/api/action/import-flight", {}, e.target));
  document.getElementById("btn-departure-briefing").addEventListener("click", (e) => runAction("/api/action/departure-briefing", {}, e.target));
  document.getElementById("btn-arrival-briefing").addEventListener("click", (e) => runAction("/api/action/arrival-briefing", {}, e.target));

  function num(id) { return parseFloat(document.getElementById(id).value) || 0; }

  document.getElementById("btn-save-speeds").addEventListener("click", (e) =>
    runAction("/api/action/save-speeds", {
      v1Kts: num("sp-v1"), rotateKts: num("sp-vr"), thrustReductionAltFt: num("sp-thrust"),
      accelAltFt: num("sp-accel"), transitionAltFt: num("sp-transalt"),
      transitionLevelFt: num("sp-translevel"), minimumsAglFt: num("sp-minimums"),
    }, e.target));

  // ============================== Settings ==============================

  document.getElementById("settings-toggle").addEventListener("click", () => {
    document.getElementById("settings-toggle").parentElement.classList.toggle("collapsed");
  });
  // Starts collapsed - this card is the least likely thing someone opens the dashboard for.
  document.getElementById("settings-toggle").parentElement.classList.add("collapsed");

  function fillSelect(select, options, current) {
    select.innerHTML = options.map((o) => `<option value="${o}"${o === current ? " selected" : ""}>${o}</option>`).join("")
      || `<option value="">(none found)</option>`;
  }

  // Positive Rate has no entry here - unlike the other callouts it fires off a fixed 50ft AGL
  // (see CalloutTracker.PositiveRateAglFt), not a user-set value, so there's no field for it to
  // begin with either way.
  const SPEED_FIELD_TO_TOGGLE = {
    "sp-field-v1": "set-en-v1",
    "sp-field-vr": "set-en-rotate",
    "sp-field-thrust": "set-en-thrust",
    "sp-field-accel": "set-en-accel",
    "sp-field-transalt": "set-en-transalt",
    "sp-field-translevel": "set-en-translevel",
    "sp-field-minimums": "set-en-minimums",
  };

  // Hides a speeds-card field the moment its callout is switched off in Settings below (and
  // shows it again the moment it's switched back on) - a value nobody can act on isn't worth a
  // field, and grid auto-placement reflows the remaining ones cleanly either way. Live off the
  // Settings checkboxes' current (possibly unsaved) state, not the last-saved status, so this
  // updates immediately as you toggle rather than only after Save Settings.
  function updateSpeedsVisibility() {
    let anyHidden = false;
    for (const [fieldId, toggleId] of Object.entries(SPEED_FIELD_TO_TOGGLE)) {
      const enabled = document.getElementById(toggleId).checked;
      document.getElementById(fieldId).style.display = enabled ? "" : "none";
      if (!enabled) anyHidden = true;
    }
    document.getElementById("speeds-hint").style.display = anyHidden ? "" : "none";
  }
  for (const toggleId of Object.values(SPEED_FIELD_TO_TOGGLE)) {
    document.getElementById(toggleId).addEventListener("change", updateSpeedsVisibility);
  }

  async function loadSettings() {
    let s;
    try {
      const res = await fetch("/api/settings", { cache: "no-store" });
      if (!res.ok) throw new Error();
      s = await res.json();
    } catch {
      document.getElementById("settings-msg").textContent = "Could not load settings.";
      document.getElementById("settings-msg").className = "action-msg err";
      return;
    }

    // Speeds card doubles as both the status-page display fields AND the settings snapshot -
    // filled here too so opening the page shows real values immediately, not just after a save.
    document.getElementById("sp-v1").value = s.v1Kts || "";
    document.getElementById("sp-vr").value = s.rotateKts || "";
    document.getElementById("sp-thrust").value = s.thrustReductionAltFt || "";
    document.getElementById("sp-accel").value = s.accelAltFt || "";
    document.getElementById("sp-transalt").value = s.transitionAltFt || "";
    document.getElementById("sp-translevel").value = s.transitionLevelFt || "";
    document.getElementById("sp-minimums").value = s.minimumsAglFt || "";

    document.getElementById("set-simbrief-id").value = s.simBriefId || "";
    fillSelect(document.getElementById("set-voice"), s.availableVoices || [], s.voiceName);
    document.getElementById("set-volume").value = s.volumePercent;
    document.getElementById("set-recorded-sounds").checked = s.useRecordedSounds;
    document.getElementById("set-elevenlabs").checked = s.useElevenLabs;
    document.getElementById("set-elevenlabs-key").value = s.elevenLabsApiKey || "";
    document.getElementById("set-elevenlabs-voice").value = s.elevenLabsVoiceId || "";

    document.getElementById("set-en-v1").checked = s.enableV1;
    document.getElementById("set-en-rotate").checked = s.enableRotate;
    document.getElementById("set-en-positiverate").checked = s.enablePositiveRate;
    document.getElementById("set-en-thrust").checked = s.enableThrustReduction;
    document.getElementById("set-en-accel").checked = s.enableAccel;
    document.getElementById("set-en-tenk").checked = s.enableTenThousandFt;
    document.getElementById("set-en-transalt").checked = s.enableTransitionAltitude;
    document.getElementById("set-en-translevel").checked = s.enableTransitionLevel;
    document.getElementById("set-en-80kt").checked = s.enableEightyKnots;
    document.getElementById("set-en-100kt").checked = s.enableHundredKnots;
    document.getElementById("set-en-1000ft").checked = s.enableOneThousandFeet;
    document.getElementById("set-en-500ft").checked = s.enableFiveHundredFeet;
    document.getElementById("set-en-minimums").checked = s.enableMinimums;

    document.getElementById("set-browser-import").checked = s.enableBrowserImport;
    document.getElementById("set-dark-mode").checked = s.darkMode;
    document.getElementById("set-dashboard-enabled").checked = s.enableWebDashboard;
    document.getElementById("set-dashboard-port").value = s.webDashboardPort;

    updateSpeedsVisibility();
  }

  document.getElementById("btn-save-settings").addEventListener("click", async (e) => {
    e.target.disabled = true;
    const msg = document.getElementById("settings-msg");
    msg.textContent = "Saving...";
    msg.className = "action-msg";

    const body = {
      v1Kts: num("sp-v1"), rotateKts: num("sp-vr"), thrustReductionAltFt: num("sp-thrust"),
      accelAltFt: num("sp-accel"), transitionAltFt: num("sp-transalt"),
      transitionLevelFt: num("sp-translevel"), minimumsAglFt: num("sp-minimums"),
      enableV1: document.getElementById("set-en-v1").checked,
      enableRotate: document.getElementById("set-en-rotate").checked,
      enablePositiveRate: document.getElementById("set-en-positiverate").checked,
      enableThrustReduction: document.getElementById("set-en-thrust").checked,
      enableAccel: document.getElementById("set-en-accel").checked,
      enableTenThousandFt: document.getElementById("set-en-tenk").checked,
      enableTransitionAltitude: document.getElementById("set-en-transalt").checked,
      enableTransitionLevel: document.getElementById("set-en-translevel").checked,
      enableEightyKnots: document.getElementById("set-en-80kt").checked,
      enableHundredKnots: document.getElementById("set-en-100kt").checked,
      enableOneThousandFeet: document.getElementById("set-en-1000ft").checked,
      enableFiveHundredFeet: document.getElementById("set-en-500ft").checked,
      enableMinimums: document.getElementById("set-en-minimums").checked,
      voiceName: document.getElementById("set-voice").value || null,
      volumePercent: parseInt(document.getElementById("set-volume").value, 10) || 100,
      useRecordedSounds: document.getElementById("set-recorded-sounds").checked,
      useElevenLabs: document.getElementById("set-elevenlabs").checked,
      elevenLabsApiKey: document.getElementById("set-elevenlabs-key").value,
      elevenLabsVoiceId: document.getElementById("set-elevenlabs-voice").value,
      darkMode: document.getElementById("set-dark-mode").checked,
      simBriefId: document.getElementById("set-simbrief-id").value,
      enableBrowserImport: document.getElementById("set-browser-import").checked,
      enableWebDashboard: document.getElementById("set-dashboard-enabled").checked,
      webDashboardPort: parseInt(document.getElementById("set-dashboard-port").value, 10),
    };

    try {
      const res = await fetch("/api/settings", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      const result = await res.json().catch(() => ({ success: false, message: "Unexpected response." }));
      msg.textContent = result.message || (result.success ? "Saved." : "Failed.");
      msg.className = "action-msg " + (result.success ? "ok" : "err");
    } catch {
      msg.textContent = "Could not reach SimCallouts.";
      msg.className = "action-msg err";
    } finally {
      e.target.disabled = false;
    }
  });

  loadSettings();
</script>
</body>
</html>
""";
    }
}
