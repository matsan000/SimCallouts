using System.Text.Json;

namespace SimCallouts
{
    /// <summary>
    /// A flattened, briefing-ready subset of a SimBrief OFP (flight plan). Parsing is
    /// defensive: any missing/renamed field just falls back to "N/A" instead of throwing,
    /// since SimBrief's JSON schema has some undocumented quirks - modeled on SimPrinter's
    /// SimBriefFlightPlan, trimmed to just what a departure/arrival briefing needs.
    /// </summary>
    public class SimBriefFlightPlan
    {
        public string Callsign { get; set; } = "N/A";
        public string AircraftName { get; set; } = "N/A";

        public string OriginIcao { get; set; } = "N/A";
        public string OriginName { get; set; } = "N/A";
        public string OriginRunway { get; set; } = "N/A";

        public string DestIcao { get; set; } = "N/A";
        public string DestName { get; set; } = "N/A";
        public string DestRunway { get; set; } = "N/A";

        public string CruiseAltitude { get; set; } = "N/A";

        public static SimBriefFlightPlan FromJson(JsonDocument doc)
        {
            var root = doc.RootElement;
            var fp = new SimBriefFlightPlan
            {
                AircraftName = GetProp(root, "aircraft", "name"),

                OriginIcao = GetProp(root, "origin", "icao_code"),
                OriginName = GetProp(root, "origin", "name"),
                OriginRunway = GetProp(root, "origin", "plan_rwy"),

                DestIcao = GetProp(root, "destination", "icao_code"),
                DestName = GetProp(root, "destination", "name"),
                DestRunway = GetProp(root, "destination", "plan_rwy"),
            };

            var atcCallsign = GetProp(root, "atc", "callsign");
            var airlineIcao = GetProp(root, "general", "icao_airline");
            var flightNumber = GetProp(root, "general", "flight_number");
            fp.Callsign = atcCallsign != "N/A" ? atcCallsign : $"{airlineIcao}{flightNumber}";

            fp.CruiseAltitude = FormatAltitude(GetProp(root, "general", "initial_altitude"));

            return fp;
        }

        private static string GetProp(JsonElement root, params string[] path)
        {
            JsonElement current = root;
            foreach (var p in path)
            {
                // SimBrief returns some sections as a one-or-more-element array rather than a
                // single object; use the first entry in that case.
                if (current.ValueKind == JsonValueKind.Array)
                {
                    if (current.GetArrayLength() == 0) return "N/A";
                    current = current[0];
                }

                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(p, out var next))
                    return "N/A";
                current = next;
            }

            return current.ValueKind switch
            {
                JsonValueKind.String => current.GetString() ?? "N/A",
                JsonValueKind.Number => current.ToString(),
                _ => "N/A"
            };
        }

        private static string FormatAltitude(string feetStr)
        {
            if (int.TryParse(feetStr, out int feet) && feet > 0)
                return feet >= 18000 ? $"flight level {feet / 100}" : $"{feet} feet";
            return "N/A";
        }
    }
}
