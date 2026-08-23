namespace SimCallouts
{
    /// <summary>
    /// Resolves each fixed callout to a user-provided MP3 under assets\Sounds. Only covers
    /// the 13 fixed callout phrases (not the departure/arrival briefings, which are built from
    /// live flight data and can't be a single static recording) - MainForm falls back to
    /// whatever text-based engine is configured whenever a callout has no matching file.
    /// </summary>
    public static class RecordedSoundEngine
    {
        private static readonly Dictionary<Callout, string> FileNames = new()
        {
            [Callout.V1] = "V1.mp3",
            [Callout.Rotate] = "Rotate.mp3",
            [Callout.PositiveRate] = "Positive_rate.mp3",
            [Callout.ThrustReduction] = "Climb_thrust.mp3",
            [Callout.Accel] = "Bug_up.mp3",
            [Callout.TenThousandFt] = "10000_feet.mp3",
            [Callout.TransitionAltitude] = "Transition_altitude.mp3",
            [Callout.TransitionLevel] = "transition_level.mp3",
            [Callout.EightyKnots] = "80 knots.mp3",
            [Callout.HundredKnots] = "100_knots.mp3",
            [Callout.OneThousandFeet] = "1000_feet.mp3",
            [Callout.FiveHundredFeet] = "500 feet.mp3",
            [Callout.Minimums] = "Minimums.mp3",
        };

        private static string SoundsDir => Path.Combine(AppContext.BaseDirectory, "assets", "Sounds");

        /// <summary>True only if every one of the 13 callouts has a matching file - used to
        /// warn in Settings if the folder is missing or incomplete, rather than silently
        /// falling back to another engine call by call.</summary>
        public static bool HasAllFiles => FileNames.Values.All(f => File.Exists(Path.Combine(SoundsDir, f)));

        public static bool TryGetPath(Callout callout, out string path)
        {
            path = "";
            if (!FileNames.TryGetValue(callout, out string? fileName)) return false;
            string candidate = Path.Combine(SoundsDir, fileName);
            if (!File.Exists(candidate)) return false;
            path = candidate;
            return true;
        }
    }
}
