using System.Text.Json;

namespace SimCallouts
{
    public class Preferences
    {
        public double V1Kts { get; set; } = 0;
        public double RotateKts { get; set; } = 0;
        public double ThrustReductionAltFt { get; set; } = 0;
        public double AccelAltFt { get; set; } = 0;
        public double TransitionAltFt { get; set; } = 0;
        public double TransitionLevelFt { get; set; } = 0;
        public double MinimumsAglFt { get; set; } = 0;
        public bool EnableV1 { get; set; } = true;
        public bool EnableRotate { get; set; } = true;
        public bool EnablePositiveRate { get; set; } = true;
        public bool EnableThrustReduction { get; set; } = true;
        public bool EnableAccel { get; set; } = true;
        public bool EnableTenThousandFt { get; set; } = true;
        public bool EnableTransitionAltitude { get; set; } = true;
        public bool EnableTransitionLevel { get; set; } = true;
        public bool EnableEightyKnots { get; set; } = false;
        public bool EnableHundredKnots { get; set; } = false;
        public bool EnableOneThousandFeet { get; set; } = false;
        public bool EnableFiveHundredFeet { get; set; } = false;
        public bool EnableMinimums { get; set; } = false;
        public string? VoiceName { get; set; }
        // Applies to every engine (SAPI, recorded sounds, ElevenLabs) - 100 is the original,
        // unadjusted volume every one of them already played at before this setting existed,
        // so upgrading users hear no change until they touch the slider themselves.
        public int VolumePercent { get; set; } = 100;
        // Recorded MP3s (assets\Sounds, see RecordedSoundEngine) - only covers the 13 fixed
        // callouts, so it stacks with the other engines rather than replacing them outright.
        public bool UseRecordedSounds { get; set; } = false;
        // ElevenLabs API (see ElevenLabsSpeechEngine) - covers callouts and briefings alike,
        // with every generation cached to disk so it's only ever paid for once per phrase.
        public bool UseElevenLabs { get; set; } = false;
        public string? ElevenLabsApiKey { get; set; }
        public string? ElevenLabsVoiceId { get; set; }
        public bool DarkMode { get; set; } = true;
        public string SimBriefId { get; set; } = "";
        public bool EnableBrowserImport { get; set; } = false;

        // Read-only local web dashboard (see DashboardServer) - status only, nothing it serves
        // can trigger a callout or change a setting, so this doesn't carry the same weight
        // EnableBrowserImport does, but it's still off by default like every other local
        // listener here (nothing opens a port without being asked to).
        public bool EnableWebDashboard { get; set; } = false;
        public int WebDashboardPort { get; set; } = 39920;

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimCallouts", "preferences.json");

        public static Preferences Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var prefs = JsonSerializer.Deserialize<Preferences>(json);
                    if (prefs != null) return prefs;
                }
            }
            catch
            {
                // Corrupt or unreadable preferences file: fall back to defaults instead of crashing startup.
            }
            return new Preferences();
        }

        public void Save()
        {
            string dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
    }
}
