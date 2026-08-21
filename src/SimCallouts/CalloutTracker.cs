namespace SimCallouts
{
    public enum Callout
    {
        V1, Rotate, PositiveRate, ThrustReduction, Accel, TenThousandFt,
        TransitionAltitude, TransitionLevel
    }

    /// <summary>
    /// Watches indicated airspeed and altitude against user-set thresholds and fires each
    /// callout exactly once per takeoff. Re-arms automatically once the aircraft is back on
    /// the ground and slowed down, so the same instance keeps working takeoff after takeoff
    /// without the user having to reset anything - modeled on OoOiTracker's state machine
    /// in SimPrinter.
    /// </summary>
    public sealed class CalloutTracker
    {
        // Below this ground speed while on the ground, the tracker re-arms for the next
        // takeoff. Keeps it from re-arming mid-rollout on landing (still fast) but ready
        // again by the time the aircraft has slowed for taxi.
        private const double RearmSpeedThresholdKts = 30.0;

        // AGL height at which "Positive rate" fires. Real crews call it off a positive VSI
        // trend right at liftoff, not a fixed height, but a small AGL threshold approximates
        // that reasonably: high enough that gear-strut bounce or a rejected-takeoff wheel
        // skip near 0 ft AGL won't false-trigger it, low enough it still fires within a
        // second or two of breaking ground. Not user-configurable since it isn't something
        // pilots brief a specific value for, unlike V1/VR/thrust reduction/accel altitude.
        private const double PositiveRateAglFt = 50.0;

        // Sterile-cockpit marker altitude (MSL) - a fixed regulatory value, not something
        // pilots configure per flight, so it isn't exposed as a setting like the other
        // altitudes. Only fires on the way up through it; a descent-through call would need
        // separate handling this tracker doesn't do.
        private const double TenThousandFtMsl = 10000.0;

        public double V1Kts { get; private set; }
        public double RotateKts { get; private set; }
        public double ThrustReductionAltFt { get; private set; }
        public double AccelAltFt { get; private set; }
        public double TransitionAltFt { get; private set; }
        public double TransitionLevelFt { get; private set; }

        public bool V1Enabled { get; private set; } = true;
        public bool RotateEnabled { get; private set; } = true;
        public bool PositiveRateEnabled { get; private set; } = true;
        public bool ThrustReductionEnabled { get; private set; } = true;
        public bool AccelEnabled { get; private set; } = true;
        public bool TenThousandFtEnabled { get; private set; } = true;
        public bool TransitionAltitudeEnabled { get; private set; } = true;
        public bool TransitionLevelEnabled { get; private set; } = true;

        public bool V1Called { get; private set; }
        public bool RotateCalled { get; private set; }
        public bool PositiveRateCalled { get; private set; }
        public bool ThrustReductionCalled { get; private set; }
        public bool AccelCalled { get; private set; }
        public bool TenThousandFtCalled { get; private set; }
        public bool TransitionAltitudeCalled { get; private set; }
        public bool TransitionLevelCalled { get; private set; }

        // Needed to tell climb-through from descent-through for transition altitude/level -
        // unlike the other thresholds, those two only count in one direction.
        private double? _lastAltitudeFt;

        public event Action<Callout>? CalloutReached;

        public void Configure(double v1Kts, double rotateKts, double thrustReductionAltFt, double accelAltFt,
            double transitionAltFt, double transitionLevelFt)
        {
            V1Kts = v1Kts;
            RotateKts = rotateKts;
            ThrustReductionAltFt = thrustReductionAltFt;
            AccelAltFt = accelAltFt;
            TransitionAltFt = transitionAltFt;
            TransitionLevelFt = transitionLevelFt;
        }

        public void ConfigureEnabled(bool v1, bool rotate, bool positiveRate, bool thrustReduction,
            bool accel, bool tenThousandFt, bool transitionAltitude, bool transitionLevel)
        {
            V1Enabled = v1;
            RotateEnabled = rotate;
            PositiveRateEnabled = positiveRate;
            ThrustReductionEnabled = thrustReduction;
            AccelEnabled = accel;
            TenThousandFtEnabled = tenThousandFt;
            TransitionAltitudeEnabled = transitionAltitude;
            TransitionLevelEnabled = transitionLevel;
        }

        public void Reset()
        {
            V1Called = false;
            RotateCalled = false;
            PositiveRateCalled = false;
            ThrustReductionCalled = false;
            AccelCalled = false;
            TenThousandFtCalled = false;
            TransitionAltitudeCalled = false;
            TransitionLevelCalled = false;
            _lastAltitudeFt = null;
        }

        public void Update(SimFlightState s)
        {
            double? prevAltitudeFt = _lastAltitudeFt;
            _lastAltitudeFt = s.AltitudeFt;

            if (s.OnGround && s.AirspeedKts < RearmSpeedThresholdKts)
            {
                Reset();
                return;
            }

            if (V1Enabled && !V1Called && V1Kts > 0 && s.AirspeedKts >= V1Kts)
            {
                V1Called = true;
                CalloutReached?.Invoke(Callout.V1);
            }

            if (RotateEnabled && !RotateCalled && RotateKts > 0 && s.AirspeedKts >= RotateKts)
            {
                RotateCalled = true;
                CalloutReached?.Invoke(Callout.Rotate);
            }

            if (PositiveRateEnabled && !PositiveRateCalled && s.RadioAltitudeFt >= PositiveRateAglFt)
            {
                PositiveRateCalled = true;
                CalloutReached?.Invoke(Callout.PositiveRate);
            }

            if (ThrustReductionEnabled && !ThrustReductionCalled && ThrustReductionAltFt > 0 && s.AltitudeFt >= ThrustReductionAltFt)
            {
                ThrustReductionCalled = true;
                CalloutReached?.Invoke(Callout.ThrustReduction);
            }

            if (AccelEnabled && !AccelCalled && AccelAltFt > 0 && s.AltitudeFt >= AccelAltFt)
            {
                AccelCalled = true;
                CalloutReached?.Invoke(Callout.Accel);
            }

            if (TenThousandFtEnabled && !TenThousandFtCalled && s.AltitudeFt >= TenThousandFtMsl)
            {
                TenThousandFtCalled = true;
                CalloutReached?.Invoke(Callout.TenThousandFt);
            }

            if (TransitionAltitudeEnabled && !TransitionAltitudeCalled && TransitionAltFt > 0 && prevAltitudeFt is double paForClimb &&
                paForClimb < TransitionAltFt && s.AltitudeFt >= TransitionAltFt)
            {
                TransitionAltitudeCalled = true;
                CalloutReached?.Invoke(Callout.TransitionAltitude);
            }

            if (TransitionLevelEnabled && !TransitionLevelCalled && TransitionLevelFt > 0 && prevAltitudeFt is double paForDescent &&
                paForDescent > TransitionLevelFt && s.AltitudeFt <= TransitionLevelFt)
            {
                TransitionLevelCalled = true;
                CalloutReached?.Invoke(Callout.TransitionLevel);
            }
        }
    }
}
