using System.Diagnostics;
using TerminalAnimation.Models;

namespace TerminalAnimation.Core;

/// <summary>
/// Plays a stored <see cref="AsciiAnimation"/> in the terminal at its native frame rate,
/// with optional synchronized background audio via <see cref="AudioPlayer"/>.
/// </summary>
public class AsciiPlayer
{
    private readonly AsciiAnimation _animation;
    private readonly string?        _animationDir;

    public AsciiPlayer(AsciiAnimation animation, string? animationDir = null)
    {
        _animation    = animation;
        _animationDir = animationDir;
    }

    /// <summary>
    /// Plays all frames (and audio if available), looping if requested.
    /// Q/Esc = quit, Space = pause/resume, ◄/► = scrub, ↑/↓ = volume.
    /// </summary>
    public async Task PlayAsync(bool loop = false, CancellationToken cancellationToken = default)
    {
        if (_animation.Frames.Count == 0)
        {
            Console.WriteLine("[!] No frames to play.");
            return;
        }

        double msPerFrame = 1000.0 / _animation.Fps;

        // ── Setup audio ────────────────────────────────────────────────────
        using var audio = new AudioPlayer();
        bool hasAudio = false;

        if (_animation.HasAudio && !string.IsNullOrEmpty(_animation.AudioFile))
        {
            string audioPath = _animation.AudioFile;
            if (!Path.IsPathRooted(audioPath) && _animationDir is not null)
                audioPath = Path.Combine(_animationDir, audioPath);
            hasAudio = audio.Load(audioPath);
        }

        // ── Setup terminal ─────────────────────────────────────────────────
        Console.CursorVisible = false;
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Clear();

        const int headerRows = 2;

        DrawHeader(hasAudio, audio.Volume);

        bool paused     = false;
        bool quit       = false;
        int  frameIndex = 0;

        // ── Keyboard input task ────────────────────────────────────────────
        var inputTask = Task.Run(() =>
        {
            while (!quit && !cancellationToken.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    switch (key.Key)
                    {
                        case ConsoleKey.Q:
                        case ConsoleKey.Escape:
                            quit = true;
                            break;

                        case ConsoleKey.Spacebar:
                            paused = !paused;
                            if (paused) audio.Pause();
                            else        audio.Resume();
                            break;

                        case ConsoleKey.LeftArrow when paused:
                            frameIndex = Math.Max(0, frameIndex - 2);
                            audio.SeekToFrame(frameIndex, _animation.Fps);
                            break;

                        case ConsoleKey.RightArrow when paused:
                            frameIndex = Math.Min(_animation.Frames.Count - 1, frameIndex + 1);
                            audio.SeekToFrame(frameIndex, _animation.Fps);
                            break;

                        case ConsoleKey.UpArrow:
                            audio.Volume = Math.Min(1f, audio.Volume + 0.05f);
                            DrawHeader(hasAudio, audio.Volume);
                            break;

                        case ConsoleKey.DownArrow:
                            audio.Volume = Math.Max(0f, audio.Volume - 0.05f);
                            DrawHeader(hasAudio, audio.Volume);
                            break;
                    }
                }
                Thread.Sleep(10);
            }
        }, cancellationToken);

        // ── Playback loop ──────────────────────────────────────────────────
        var  stopwatch   = Stopwatch.StartNew();
        long nextFrameMs = 0;

        do
        {
            frameIndex = 0;
            stopwatch.Restart();
            nextFrameMs = 0;

            if (hasAudio) { audio.Stop(); audio.Play(); }

            while (frameIndex < _animation.Frames.Count && !quit && !cancellationToken.IsCancellationRequested)
            {
                if (paused)
                {
                    RenderFrame(_animation.Frames[frameIndex], headerRows);
                    DrawStatusBar(frameIndex, _animation.Frames.Count, paused, hasAudio, audio.Volume);
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                long now = stopwatch.ElapsedMilliseconds;
                if (now < nextFrameMs)
                {
                    int sleepMs = (int)(nextFrameMs - now);
                    if (sleepMs > 0)
                        await Task.Delay(sleepMs, cancellationToken).ConfigureAwait(false);
                }

                RenderFrame(_animation.Frames[frameIndex], headerRows);
                DrawStatusBar(frameIndex, _animation.Frames.Count, paused, hasAudio, audio.Volume);

                nextFrameMs += (long)msPerFrame;
                frameIndex++;
            }

        } while (loop && !quit && !cancellationToken.IsCancellationRequested);

        // ── Teardown ───────────────────────────────────────────────────────
        quit = true;
        audio.Stop();
        await inputTask.ConfigureAwait(false);

        Console.CursorVisible = true;
        // Always move to last safe row — never exceed WindowHeight-1
        SafeSetCursor(0, Console.WindowHeight - 1);
        Console.WriteLine();
        Console.WriteLine("[Playback ended]");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>SetCursorPosition clamped to the current window bounds — never throws.</summary>
    private static void SafeSetCursor(int left, int top)
    {
        int maxRow = Console.WindowHeight - 1;
        int maxCol = Console.WindowWidth  - 1;
        if (maxRow < 0 || maxCol < 0) return;
        Console.SetCursorPosition(
            Math.Clamp(left, 0, maxCol),
            Math.Clamp(top,  0, maxRow));
    }

    private void DrawHeader(bool hasAudio, float volume)
    {
        SafeSetCursor(0, 0);
        Console.ForegroundColor = ConsoleColor.Cyan;

        string audioLabel = hasAudio ? $"🔊 {(int)(volume * 100)}%" : "🔇 (no audio)";
        string header = $"▶ ASCII Player  |  {_animation.SourceFile}  |  " +
                        $"{_animation.Fps:F2} fps  |  {_animation.CharColumns}x{_animation.CharRows}  |  {audioLabel}";

        int w = Console.WindowWidth;
        Console.Write(header.Length >= w ? header[..w] : header.PadRight(w));

        if (Console.WindowHeight > 1)
        {
            SafeSetCursor(0, 1);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(new string('─', Math.Min(w, 100)));
        }
        Console.ResetColor();
    }

    private void DrawStatusBar(int frameIndex, int totalFrames, bool paused, bool hasAudio, float volume)
    {
        // Status bar goes one row BELOW the last content row, clamped to window
        int contentRows = Math.Min(_animation.CharRows, Console.WindowHeight - 3);
        int statusRow   = Math.Min(2 + contentRows, Console.WindowHeight - 1);

        SafeSetCursor(0, statusRow);
        Console.ForegroundColor = ConsoleColor.DarkGray;

        string pauseStr = paused ? "[PAUSED] " : "";
        double pct      = totalFrames == 0 ? 0 : (double)(frameIndex + 1) / totalFrames;
        int    barWidth = Math.Max(5, Math.Min(40, Console.WindowWidth - 50));
        int    filled   = (int)(pct * barWidth);
        string bar      = new string('█', filled) + new string('░', barWidth - filled);
        string controls = hasAudio
            ? "Q=Quit  Spc=Pause  ◄►=Scrub  ↑↓=Vol"
            : "Q=Quit  Spc=Pause  ◄►=Scrub";

        string status = $"  {pauseStr}[{bar}] {frameIndex + 1}/{totalFrames}  |  {controls}   ";
        int w = Console.WindowWidth;
        Console.Write(status.Length >= w ? status[..w] : status);
        Console.ResetColor();
    }

    private static void RenderFrame(string frame, int startRow)
    {
        ReadOnlySpan<char> span       = frame.AsSpan();
        int                row        = startRow;
        int                maxRow     = Console.WindowHeight - 1;
        int                windowWidth = Console.WindowWidth;

        while (!span.IsEmpty && row <= maxRow)
        {
            int nl = span.IndexOf('\n');
            ReadOnlySpan<char> lineSpan = nl >= 0 ? span[..nl] : span;

            SafeSetCursor(0, row);

            string line = lineSpan.Length >= windowWidth
                ? lineSpan[..windowWidth].ToString()
                : lineSpan.ToString();

            Console.Write(line);

            int remaining = windowWidth - line.Length;
            if (remaining > 0)
                Console.Write(new string(' ', remaining));

            row++;

            if (nl < 0) break;
            span = span[(nl + 1)..];
        }
    }
}
