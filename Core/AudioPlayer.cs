using NAudio.Wave;

namespace TerminalAnimation.Core;

/// <summary>
/// Wraps NAudio to play an audio file in the background with
/// seek, pause, resume, and volume control.
/// </summary>
public sealed class AudioPlayer : IDisposable
{
    private AudioFileReader?  _audioFile;
    private WaveOutEvent?     _outputDevice;
    private bool              _disposed;

    public bool IsPlaying => _outputDevice?.PlaybackState == PlaybackState.Playing;
    public bool IsPaused  => _outputDevice?.PlaybackState == PlaybackState.Paused;

    /// <summary>
    /// Loads the audio file so it's ready to play.
    /// Returns false if the file cannot be opened.
    /// </summary>
    public bool Load(string audioPath)
    {
        if (!File.Exists(audioPath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[!] Audio file not found: {audioPath}");
            Console.ResetColor();
            return false;
        }

        try
        {
            _audioFile    = new AudioFileReader(audioPath);
            _outputDevice = new WaveOutEvent { DesiredLatency = 80 };
            _outputDevice.Init(_audioFile);
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[!] Could not load audio: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    /// <summary>Starts playback from the beginning (or current position).</summary>
    public void Play() => _outputDevice?.Play();

    /// <summary>Pauses audio playback.</summary>
    public void Pause() => _outputDevice?.Pause();

    /// <summary>Resumes paused playback.</summary>
    public void Resume() => _outputDevice?.Play();

    /// <summary>Stops and rewinds to the beginning.</summary>
    public void Stop()
    {
        _outputDevice?.Stop();
        if (_audioFile is not null)
            _audioFile.Position = 0;
    }

    /// <summary>
    /// Seeks audio to match the given frame index at the specified fps.
    /// Used when the user scrubs frames while paused.
    /// </summary>
    public void SeekToFrame(int frameIndex, double fps)
    {
        if (_audioFile is null) return;

        double seconds = frameIndex / fps;
        long targetPos = (long)(seconds * _audioFile.WaveFormat.AverageBytesPerSecond);

        // Align to block boundary
        int blockAlign = _audioFile.WaveFormat.BlockAlign;
        targetPos = (targetPos / blockAlign) * blockAlign;
        targetPos = Math.Clamp(targetPos, 0, _audioFile.Length);

        _audioFile.Position = targetPos;
    }

    /// <summary>Volume between 0.0 and 1.0.</summary>
    public float Volume
    {
        get => _outputDevice?.Volume ?? 1f;
        set { if (_outputDevice is not null) _outputDevice.Volume = Math.Clamp(value, 0f, 1f); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _outputDevice?.Stop();
        _outputDevice?.Dispose();
        _audioFile?.Dispose();
    }
}
