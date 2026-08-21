namespace SimCallouts
{
    public enum Callout
    {
        V1, Rotate, PositiveRate, ThrustReduction, Accel, TenThousandFt,
        TransitionAltitude, TransitionLevel, EightyKnots, HundredKnots,
        OneThousandFeet, FiveHundredFeet, Minimums
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
        // altitudes. Fires once climbing through it and once descending back through it,
        // independently, using the same crossing-direction check as transition altitude/level.
        private const double TenThousandFtMsl = 10000.0;

        // Airspeed cross-check calls during the takeoff roll ("80 knots", "100 knots") - fixed
        // round-number checkpoints some operators brief regardless of that flight's actual
        // V-speeds, so like Positive rate/10,000 feet these aren't user-configurable values.
        // Off by default since it's a less universal SOP than the others.
        private const double EightyKtsThreshold = 80.0;
        private const double HundredKtsThreshold = 100.0;

        // Approach altitude gate calls (AGL, via radio altitude) - "1,000 feet" then
        // "500 feet" descending toward the runway. Direction-aware like transition
        // altitude/level: only counts a *descending* crossing, so the same altitudes passed
        // through while climbing out after takeoff don't falsely trigger these. That does mean
        // a low pass over terrain well before the actual approach could consume the one-shot
        // early, same known trade-off as transition level over mountainous cruise terrain.
        private const double OneThousandAglFt = 1000.0;
        private const double FiveHundredAglFt = 500.0;

        // Once any approach gate call (1,000/500 feet, Minimums) has fired and the aircraft
        // then climbs back above this AGL, those three re-arm on their own - covers a
        // go-around/missed approach followed by a second approach in the same flight, which
        // otherwise wouldn't announce anything the second time since everything else in this
        // tracker only fires once per flight cycle. Comfortably above the highest of the
        // three (1,000ft) so a normal climb-out on the go-around clears it well before cruise.
        private const double GoAroundRearmAglFt = 2500.0;

        public double V1Kts { get; private set; }
        public double RotateKts { get; private set; }
        public double ThrustReductionAltFt { get; private set; }
        public double AccelAltFt { get; private set; }
        public double TransitionAltFt { get; private set; }
        public double TransitionLevelFt { get; private set; }
        public double MinimumsAglFt { get; private set; }

        public bool V1Enabled { get; private set; } = true;
        public bool RotateEnabled { get; private set; } = true;
        public bool PositiveRateEnabled { get; private set; } = true;
        public bool ThrustReductionEnabled { get; private set; } = true;
        public bool AccelEnabled { get; private set; } = true;
        public bool TenThousandFtEnabled { get; private set; } = true;
        public bool TransitionAltitudeEnabled { get; private set; } = true;
        public bool TransitionLevelEnabled { get; private set; } = true;
        public bool EightyKnotsEnabled { get; private set; } = false;
        public bool HundredKnotsEnabled { get; private set; } = false;
        public bool OneThousandFeetEnabled { get; private set; } = false;
        public bool FiveHundredFeetEnabled { get; private set; } = false;

        // Off by default - most add-on aircraft already call their own "Minimums" off the
        // FMC/radio altimeter, so this would just double up for most users. It's here for
        // aircraft that don't, or for anyone who wants SimCallouts to do it uniformly.
        public bool MinimumsEnabled { get; private set; } = false;

        public bool V1Called { get; private set; }
        public bool RotateCalled { get; private set; }
        public bool PositiveRateCalled { get; private set; }
        public bool ThrustReductionCalled { get; private set; }
        public bool AccelCalled { get; private set; }
        public bool TenThousandFtClimbCalled { get; private set; }
        public bool TenThousandFtDescentCalled { get; private set; }
        public bool TransitionAltitudeCalled { get; private set; }
        public bool TransitionLevelCalled { get; private set; }
        public bool EightyKnotsCalled { get; private set; }
        public bool HundredKnotsCalled { get; private set; }
        public bool OneThousandFeetCalled { get; private set; }
        public bool FiveHundredFeetCalled { get; private set; }
        public bool MinimumsCalled { get; private set; }

        // Needed to tell climb-through from descent-through for 10,000 feet and transition
        // altitude/level - unlike the other thresholds, those only count in one direction.
        private double? _lastAltitudeFt;

        // Same idea as _lastAltitudeFt but for radio altitude (AGL), used by the 1,000/500
        // feet approach calls.
        private double? _lastRadioAltitudeFt;

        public event Action<Callout>? CalloutReached;

        public void Configure(double v1Kts, double rotateKts, double thrustReductionAltFt, double accelAltFt,
            double transitionAltFt, double transitionLevelFt, double minimumsAglFt)
        {
            V1Kts = v1Kts;
            RotateKts = rotateKts;
            ThrustReductionAltFt = thrustReductionAltFt;
            AccelAltFt = accelAltFt;
            TransitionAltFt = transitionAltFt;
            TransitionLevelFt = transitionLevelFt;
            MinimumsAglFt = minimumsAglFt;
        }

        public void ConfigureEnabled(bool v1, bool rotate, bool positiveRate, bool thrustReduction,
            bool accel, bool tenThousandFt, bool transitionAltitude, bool transitionLevel,
            bool eightyKnots, bool hundredKnots, bool oneThousandFeet, bool fiveHundredFeet,
            bool minimums)
        {
            V1Enabled = v1;
            RotateEnabled = rotate;
            PositiveRateEnabled = positiveRate;
            ThrustReductionEnabled = thrustReduction;
            AccelEnabled = accel;
            TenThousandFtEnabled = tenThousandFt;
            TransitionAltitudeEnabled = transitionAltitude;
            TransitionLevelEnabled = transitionLevel;
            EightyKnotsEnabled = eightyKnots;
            HundredKnotsEnabled = hundredKnots;
            OneThousandFeetEnabled = oneThousandFeet;
            FiveHundredFeetEnabled = fiveHundredFeet;
            MinimumsEnabled = minimums;
        }

        public void Reset()
        {
            V1Called = false;
            RotateCalled = false;
            PositiveRateCalled = false;
            ThrustReductionCalled = false;
            AccelCalled = false;
            TenThousandFtClimbCalled = false;
            TenThousandFtDescentCalled = false;
            TransitionAltitudeCalled = false;
            TransitionLevelCalled = false;
            EightyKnotsCalled = false;
            HundredKnotsCalled = false;
            OneThousandFeetCalled = false;
            FiveHundredFeetCalled = false;
            MinimumsCalled = false;
            _lastAltitudeFt = null;
            _lastRadioAltitudeFt = null;
        }

        public void Update(SimFlightState s)
        {
            double? prevAltitudeFt = _lastAltitudeFt;
            _lastAltitudeFt = s.AltitudeFt;
            double? prevRadioAltitudeFt = _lastRadioAltitudeFt;
            _lastRadioAltitudeFt = s.RadioAltitudeFt;

            if (s.OnGround && s.AirspeedKts < RearmSpeedThresholdKts)
            {
                Reset();
                return;
            }

            if (EightyKnotsEnabled && !EightyKnotsCalled && s.AirspeedKts >= EightyKtsThreshold)
            {
                EightyKnotsCalled = true;
                CalloutReached?.Invoke(Callout.EightyKnots);
            }

            if (HundredKnotsEnabled && !HundredKnotsCalled && s.AirspeedKts >= HundredKtsThreshold)
            {
                HundredKnotsCalled = true;
                CalloutReached?.Invoke(Callout.HundredKnots);
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

            if (TenThousandFtEnabled && !TenThousandFtClimbCalled && prevAltitudeFt is double paForTenKClimb &&
                paForTenKClimb < TenThousandFtMsl && s.AltitudeFt >= TenThousandFtMsl)
            {
                TenThousandFtClimbCalled = true;
                CalloutReached?.Invoke(Callout.TenThousandFt);
            }

            if (TenThousandFtEnabled && !TenThousandFtDescentCalled && prevAltitudeFt is double paForTenKDescent &&
                paForTenKDescent > TenThousandFtMsl && s.AltitudeFt <= TenThousandFtMsl)
            {
                TenThousandFtDescentCalled = true;
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

            if (OneThousandFeetEnabled && !OneThousandFeetCalled && prevRadioAltitudeFt is double praFor1000 &&
                praFor1000 > OneThousandAglFt && s.RadioAltitudeFt <= OneThousandAglFt)
            {
                OneThousandFeetCalled = true;
                CalloutReached?.Invoke(Callout.OneThousandFeet);
            }

            if (FiveHundredFeetEnabled && !FiveHundredFeetCalled && prevRadioAltitudeFt is double praFor500 &&
                praFor500 > FiveHundredAglFt && s.RadioAltitudeFt <= FiveHundredAglFt)
            {
                FiveHundredFeetCalled = true;
                CalloutReached?.Invoke(Callout.FiveHundredFeet);
            }

            if (MinimumsEnabled && !MinimumsCalled && MinimumsAglFt > 0 && prevRadioAltitudeFt is double praForMin &&
                praForMin > MinimumsAglFt && s.RadioAltitudeFt <= MinimumsAglFt)
            {
                MinimumsCalled = true;
                CalloutReached?.Invoke(Callout.Minimums);
            }

            // Go-around re-arm: if we got low enough for one of the approach gate calls to
            // have fired, and have since climbed back well clear of them, they're available
            // again for the next approach - see GoAroundRearmAglFt above.
            if ((OneThousandFeetCalled || FiveHundredFeetCalled || MinimumsCalled) &&
                s.RadioAltitudeFt >= GoAroundRearmAglFt)
            {
                OneThousandFeetCalled = false;
                FiveHundredFeetCalled = false;
                MinimumsCalled = false;
            }
        }
    }
}
