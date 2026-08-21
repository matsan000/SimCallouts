using System.Text;

namespace SimCallouts
{
    /// <summary>
    /// Builds the spoken text for the departure/arrival briefing buttons, combining the
    /// loaded SimBrief flight plan with the user's configured V-speeds and altitudes. Only
    /// speaks values actually known - it never invents a SID/STAR, runway, or approach that
    /// wasn't in the OFP, since a wrong specific is worse than a generic statement.
    /// </summary>
    public static class BriefingBuilder
    {
        public static string BuildDeparture(SimBriefFlightPlan plan, Preferences prefs)
        {
            var sb = new StringBuilder();

            sb.Append("Let's do the departure briefing. ");

            if (plan.OriginRunway != "N/A")
                sb.Append($"Planned departure runway {plan.OriginRunway}. ");

            if (plan.CruiseAltitude != "N/A")
                sb.Append($"Climbing to {plan.CruiseAltitude}. ");

            if (prefs.V1Kts > 0)
                sb.Append($"V1 is {prefs.V1Kts:0}. ");
            if (prefs.RotateKts > 0)
                sb.Append($"Rotate is {prefs.RotateKts:0}. ");
            if (prefs.ThrustReductionAltFt > 0)
                sb.Append($"Thrust reduction at {prefs.ThrustReductionAltFt:0} feet. ");
            if (prefs.AccelAltFt > 0)
                sb.Append($"Acceleration altitude {prefs.AccelAltFt:0} feet. ");
            if (prefs.TransitionAltFt > 0)
                sb.Append($"Transition altitude {prefs.TransitionAltFt:0} feet. ");

            sb.Append("Below V1, any failure and I will reject the takeoff. ");
            sb.Append("Above V1, we continue, clean up on schedule, and handle it airborne. ");
            sb.Append("Positive rate, gear up, flaps on schedule.");

            return sb.ToString();
        }

        public static string BuildArrival(SimBriefFlightPlan plan, Preferences prefs)
        {
            var sb = new StringBuilder();

            sb.Append("Let's do the arrival briefing. ");

            if (plan.DestRunway != "N/A")
                sb.Append($"Planned landing runway {plan.DestRunway}. ");

            if (prefs.TransitionLevelFt > 0)
                sb.Append($"Transition level {prefs.TransitionLevelFt:0} feet. ");

            sb.Append("We'll review the approach and missed approach as we get closer in. ");
            sb.Append("Standard calls on the way down: positive rate on a go-around, minimums, and landing.");

            return sb.ToString();
        }
    }
}
