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
        public bool EnableV1 { get; set; } = true;
        public bool EnableRotate { get; set; } = true;
        public bool EnablePositiveRate { get; set; } = true;
        public bool EnableThrustReduction { get; set; } = true;
        public bool EnableAccel { get; set; } = true;
        public bool EnableTenThousandFt { get; set; } = true;
        public bool EnableTransitionAltitude { get; set; } = true;
        public bool EnableTransitionLevel { get; set; } = true;
        public string? VoiceName { get; set; }
        public bool DarkMode { get; set; } = true;
        public string SimBriefId { get; set; } = "";
        public bool EnableBrowserImport { get; set; } = false;

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
