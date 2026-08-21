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

        private void OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            if ((Requests)data.dwRequestID != Requests.FlightData) return;
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
