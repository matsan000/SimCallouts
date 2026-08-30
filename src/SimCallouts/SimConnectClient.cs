using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;

namespace SimCallouts
{
    /// <summary>
    /// Thin SimConnect wrapper that polls indicated airspeed and ground contact once per
    /// sim-second. The sim may not be running yet (or may close/reopen while SimCallouts
    /// stays open) so connecting is lazy and retried on a timer rather than attempted once
    /// at startup - same pattern as SimPrinter's SimConnectClient.
    /// </summary>
    public sealed class SimConnectClient : IDisposable
    {
        private const string AppName = "SimCallouts";
        private const int WM_USER_SIMCONNECT = 0x0402;
        private const int ReconnectIntervalMs = 5000;

        public event Action<SimFlightState>? FlightStateUpdated;
        public event Action? Connected;
        public event Action? Disconnected;

        private enum Definitions { FlightData }
        private enum Requests { FlightData }
        private enum Events { Sim, SimStart, SimStop, FlightLoaded }

        // SimConnect starts streaming "user aircraft" data the moment the connection opens,
        // which is as soon as the sim process itself is up - well before a flight is actually
        // loaded and spawned in. During the loading screen/main menu that data is meaningless
        // (RADIO HEIGHT in particular has been seen reporting a transient value well above the
        // "Positive rate" AGL threshold while the world is still streaming in), and it can
        // swing through several different bogus readings as the load progresses - each one
        // potentially re-triggering CalloutTracker's ground-rearm-then-refire cycle, which is
        // what caused "Positive rate" to repeat itself while loading in - and, it turns out,
        // when returning to the main menu after a flight too (landing rearms the tracker while
        // the flight is still genuinely on the ground, then the menu's own bogus data re-fires
        // it - same underlying cause as the loading-screen case, just at the other end).
        //
        // Three signals gated together (all must say "really flying"), not just one - "Sim" and
        // "SimStart"/"SimStop" turned out to both be unreliable here on their own:
        //  - "Sim" is documented as 1 = "the user is in control of the aircraft", 0 = "the user
        //    is navigating the UI" - but MSFS 2024's main menu renders a live establishing-shot
        //    flight behind the menu overlay, and that's been observed to still leave "Sim"
        //    reporting 1 there, defeating this gate on its own.
        //  - "SimStart"/"SimStop" are documented more strictly ("the user is actively
        //    controlling the aircraft, typically on the ground or in the air") and fire
        //    specifically around a real flight beginning/ending, not the menu's background
        //    render - but the main menu turns out to load its own background scene as a real
        //    flight file (see "FlightLoaded" below), which can fire its own SimStart and defeat
        //    this gate too. Also known to double-fire during a flight reset/load (SimStop,
        //    SimStart, SimStop, SimStart) - harmless here since this only tracks the latest
        //    state, not counting transitions.
        //  - "FlightLoaded" is the one signal that's actually reliable for this: MSFS loads a
        //    real flight file named "MainMenu.FLT" behind the main menu, and this event fires
        //    with that file's path whenever ANY flight loads (a real one or the menu's). So
        //    unlike the other two, it can tell the menu's background flight apart from an
        //    actual one by name, rather than just observing that *some* flight is "running".
        private bool _simEventRunning;
        private bool _flightStarted;
        private bool _atMainMenu;
        private bool SimRunning => _simEventRunning && _flightStarted && !_atMainMenu;

        [StructLayout(LayoutKind.Sequential)]
        private struct FlightData
        {
            public double AirspeedKts;
            public double OnGround;
            public double AltitudeFt;
            public double RadioAltitudeFt;
        }

        private readonly MessageWindow _window;
        private readonly System.Windows.Forms.Timer _reconnectTimer;
        private SimConnect? _simConnect;

        public bool IsConnected => _simConnect != null;

        public SimConnectClient()
        {
            _window = new MessageWindow(this);
            _reconnectTimer = new System.Windows.Forms.Timer { Interval = ReconnectIntervalMs };
            _reconnectTimer.Tick += (_, _) => TryConnect();
            _reconnectTimer.Start();
            TryConnect();
        }

        private void TryConnect()
        {
            if (_simConnect != null) return;

            try
            {
                var sc = new SimConnect(AppName, _window.Handle, WM_USER_SIMCONNECT, null, 0);
                sc.OnRecvOpen += (_, _) => Connected?.Invoke();
                sc.OnRecvQuit += (_, _) => HandleDisconnect();
                sc.OnRecvException += (_, _) => { };
                sc.OnRecvSimobjectData += OnRecvSimobjectData;
                sc.OnRecvEvent += OnRecvEvent;
                sc.OnRecvEventFilename += OnRecvEventFilename;

                // False/true (respectively) until the events themselves say otherwise - so a
                // fresh connection made while still sitting at the main menu/loading screen
                // starts out silent too, not just reconnects. _atMainMenu starts true rather
                // than false for the same reason: assume "not really flying" until a
                // FlightLoaded event proves otherwise, rather than assuming the opposite.
                _simEventRunning = false;
                _flightStarted = false;
                _atMainMenu = true;
                sc.SubscribeToSystemEvent(Events.Sim, "Sim");
                sc.SubscribeToSystemEvent(Events.SimStart, "SimStart");
                sc.SubscribeToSystemEvent(Events.SimStop, "SimStop");
                sc.SubscribeToSystemEvent(Events.FlightLoaded, "FlightLoaded");

                sc.AddToDataDefinition(Definitions.FlightData, "AIRSPEED INDICATED", "knots",
                    SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
                sc.AddToDataDefinition(Definitions.FlightData, "SIM ON GROUND", "bool",
                    SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
                // True MSL altitude, not the altimeter-affected "INDICATED ALTITUDE" - so
                // thrust reduction/acceleration altitude callouts aren't thrown off by QNH.
                sc.AddToDataDefinition(Definitions.FlightData, "PLANE ALTITUDE", "feet",
                    SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
                // AGL height for the "Positive rate" callout - using this instead of MSL
                // altitude means it doesn't need a per-airport elevation to compare against.
                sc.AddToDataDefinition(Definitions.FlightData, "RADIO HEIGHT", "feet",
                    SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
                sc.RegisterDataDefineStruct<FlightData>(Definitions.FlightData);

                sc.RequestDataOnSimObject(Requests.FlightData, Definitions.FlightData,
                    SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.SECOND,
                    SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);

                _simConnect = sc;
            }
            catch (COMException)
            {
                // Sim isn't running (or isn't ready yet) - retry on the next timer tick.
                _simConnect = null;
            }
        }

        private void OnRecvEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
        {
            switch ((Events)data.uEventID)
            {
                case Events.Sim:
                    _simEventRunning = data.dwData != 0;
                    break;
                case Events.SimStart:
                    _flightStarted = true;
                    break;
                case Events.SimStop:
                    _flightStarted = false;
                    break;
            }
        }

        private void OnRecvEventFilename(SimConnect sender, SIMCONNECT_RECV_EVENT_FILENAME data)
        {
            if ((Events)data.uEventID != Events.FlightLoaded) return;
            // The main menu's own background scene loads as "MainMenu.FLT" - anything else
            // loading is a real flight. See the big comment above SimRunning for why this is
            // needed alongside Sim/SimStart/SimStop rather than instead of them.
            _atMainMenu = data.szFileName?.Contains("MainMenu", StringComparison.OrdinalIgnoreCase) ?? true;
        }

        private void OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            if ((Requests)data.dwRequestID != Requests.FlightData) return;
            if (!SimRunning) return; // still loading/at the main menu/paused - see SimRunning above
            var value = (FlightData)data.dwData[0];

            FlightStateUpdated?.Invoke(new SimFlightState(
                AirspeedKts: value.AirspeedKts,
                OnGround: value.OnGround != 0,
                AltitudeFt: value.AltitudeFt,
                RadioAltitudeFt: value.RadioAltitudeFt));
        }

        internal void ReceiveMessage()
        {
            try
            {
                _simConnect?.ReceiveMessage();
            }
            catch (COMException)
            {
                HandleDisconnect();
            }
        }

        private void HandleDisconnect()
        {
            if (_simConnect == null) return;
            _simConnect.Dispose();
            _simConnect = null;
            Disconnected?.Invoke();
        }

        public void Dispose()
        {
            _reconnectTimer.Stop();
            _reconnectTimer.Dispose();
            _simConnect?.Dispose();
            _simConnect = null;
            _window.DestroyHandle();
        }

        /// <summary>Message-only native window that receives the WM_USER message SimConnect
        /// posts when new data is ready to be pulled via ReceiveMessage().</summary>
        private sealed class MessageWindow : System.Windows.Forms.NativeWindow
        {
            private readonly SimConnectClient _owner;

            public MessageWindow(SimConnectClient owner)
            {
                _owner = owner;
                CreateHandle(new System.Windows.Forms.CreateParams());
            }

            protected override void WndProc(ref System.Windows.Forms.Message m)
            {
                if (m.Msg == WM_USER_SIMCONNECT)
                {
                    _owner.ReceiveMessage();
                    return;
                }
                base.WndProc(ref m);
            }
        }
    }

    /// <summary>One sim-second snapshot of the flight data CalloutTracker needs.</summary>
    public readonly record struct SimFlightState(double AirspeedKts, bool OnGround, double AltitudeFt, double RadioAltitudeFt);
}
