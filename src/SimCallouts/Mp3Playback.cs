using NAudio.Wave;

namespace SimCallouts
{
    /// <summary>
    /// Plays a single MP3 file at a time via NAudio - the only built-in .NET option
    /// (System.Media.SoundPlayer) can't read MP3, only WAV. Shared by both
    /// RecordedSoundEngine (user-provided files) and ElevenLabsSpeechEngine (cached API
    /// responses), so there's one playback device instead of each engine managing its own.
    /// </summary>
    public sealed class Mp3Playback : IDisposable
    {
        private WaveOutEvent? _output;
        private AudioFileReader? _reader;

        public void PlayFile(string path)
        {
            Stop();
            _reader = new AudioFileReader(path);
            _output = new WaveOutEvent();
            _output.Init(_reader);
            _output.Play();
        }

        /// <summary>Same as PlayFile, but completes once playback actually finishes - for
        /// callers that need to play several files back to back (e.g. testing "V1" then
        /// "Rotate") without them overlapping or cutting each other off.</summary>
        public Task PlayFileAsync(string path)
        {
            Stop();
            var tcs = new TaskCompletionSource();
            _reader = new AudioFileReader(path);
            _output = new WaveOutEvent();
            _output.PlaybackStopped += (_, _) => tcs.TrySetResult();
            _output.Init(_reader);
            _output.Play();
            return tcs.Task;
        }

        public void Stop()
        {
            try { _output?.Stop(); } catch { /* already stopped/disposed */ }
            _output?.Dispose();
            _reader?.Dispose();
            _output = null;
            _reader = null;
        }

        public void Dispose() => Stop();
    }
}
