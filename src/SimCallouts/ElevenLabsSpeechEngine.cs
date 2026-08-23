using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace SimCallouts
{
    /// <summary>
    /// Text-to-speech via the user's own ElevenLabs API key. Every result is cached to disk
    /// keyed by (voice, text) - a callout like "V1" sounds identical every flight, so there's
    /// no reason to pay for and wait on the same generation more than once. The cache is
    /// content-addressed, so it works the same way for the dynamic departure/arrival briefing
    /// text too: it just won't hit very often, since that text usually differs flight to flight.
    /// </summary>
    public sealed class ElevenLabsSpeechEngine
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
        private readonly string _cacheDir;

        public ElevenLabsSpeechEngine()
        {
            _cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimCallouts", "ElevenLabsCache");
            Directory.CreateDirectory(_cacheDir);
        }

        /// <summary>Returns a local file path with the spoken audio for this exact text on
        /// this exact voice - served from cache when available, otherwise fetched from the
        /// API and cached for next time. Null on any failure (bad key, no network, etc.).</summary>
        public async Task<string?> GetOrFetchAudioAsync(string apiKey, string voiceId, string text)
        {
            string cachePath = CachePathFor(voiceId, text);
            if (File.Exists(cachePath)) return cachePath;

            byte[]? audio = await FetchFromApiAsync(apiKey, voiceId, text);
            if (audio is null) return null;

            await File.WriteAllBytesAsync(cachePath, audio);
            return cachePath;
        }

        private string CachePathFor(string voiceId, string text)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(voiceId + "|" + text));
            return Path.Combine(_cacheDir, Convert.ToHexString(hash) + ".mp3");
        }

        private static async Task<byte[]?> FetchFromApiAsync(string apiKey, string voiceId, string text)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"https://api.elevenlabs.io/v1/text-to-speech/{Uri.EscapeDataString(voiceId)}");
            request.Headers.Add("xi-api-key", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));
            request.Content = JsonContent.Create(new
            {
                text,
                model_id = "eleven_turbo_v2_5",
                voice_settings = new { stability = 0.5, similarity_boost = 0.75 },
            });

            try
            {
                using HttpResponseMessage response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return null;
            }
        }
    }
}
