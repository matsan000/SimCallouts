using System.Globalization;
using System.Text.RegularExpressions;

namespace SimCallouts
{
    /// <summary>
    /// Pulls V1 and VR out of the raw text SimBrief's takeoff performance calculator shows
    /// (the same text the SimPrinter browser extension captures and prints as-is). SimBrief
    /// always separates a label from its value with at least one space - relied on here
    /// instead of trying to reconstruct SimBrief's two-column layout.
    /// </summary>
    public static class PerformanceCalcParser
    {
        private static readonly Regex V1Pattern = new(@"\bV1\b\s*:?\s*(\d{2,3}(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex VrPattern = new(@"\bVR\b\s*:?\s*(\d{2,3}(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool TryParseVSpeeds(string text, out double v1Kts, out double rotateKts)
        {
            v1Kts = 0;
            rotateKts = 0;

            var v1Match = V1Pattern.Match(text);
            var vrMatch = VrPattern.Match(text);

            if (v1Match.Success)
                v1Kts = double.Parse(v1Match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (vrMatch.Success)
                rotateKts = double.Parse(vrMatch.Groups[1].Value, CultureInfo.InvariantCulture);

            return v1Match.Success || vrMatch.Success;
        }
    }
}
