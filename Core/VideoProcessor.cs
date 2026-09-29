using FFMpegCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TerminalAnimation.Models;

namespace TerminalAnimation.Core;

/// <summary>
/// Handles video frame extraction using FFmpeg and orchestrates
/// grayscale conversion + ASCII art generation.
/// </summary>
public class VideoProcessor
{
    private readonly AsciiConverter _converter;

    public VideoProcessor(AsciiConverter converter)
    {
        _converter = converter;
    }

    /// <summary>
    /// Converts a video file to an <see cref="AsciiAnimation"/> object.
    /// Frames are extracted via FFmpeg, converted to grayscale, then to ASCII.
    /// Audio is also extracted and stored alongside the frames.
    /// </summary>
    /// <param name="videoPath">Absolute path to the source video.</param>
    /// <param name="outputDir">Directory where the animation + audio will be saved.</param>
    /// <param name="progress">Optional progress callback (current frame, total frames).</param>
    /// <param name="maxWidth">Max pixel width (constrained to terminal cols × cellW).</param>
    /// <param name="maxHeight">Max pixel height (constrained to terminal rows × cellH). 0 = no limit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<AsciiAnimation> ProcessAsync(
        string videoPath,
        string outputDir,
        Action<int, int>? progress = null,
        int maxWidth  = 480,
        int maxHeight = 0,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(videoPath))
            throw new FileNotFoundException($"Video not found: {videoPath}");

        // Probe the video for metadata
        Console.WriteLine("[*] Probing video metadata...");
        var mediaInfo = await FFProbe.AnalyseAsync(videoPath, cancellationToken: cancellationToken);
        var videoStream = mediaInfo.VideoStreams.FirstOrDefault()
            ?? throw new InvalidOperationException("No video stream found in file.");

        double fps = videoStream.FrameRate;
        int totalFrames = (int)(mediaInfo.Duration.TotalSeconds * fps);

        // Determine target frame dimensions — fit within both width AND height bounds
        int srcWidth  = videoStream.Width;
        int srcHeight = videoStream.Height;

        // Start by constraining width
        int targetWidth  = (maxWidth  > 0 && srcWidth  > maxWidth)  ? maxWidth  : srcWidth;
        int targetHeight = (int)(srcHeight * ((double)targetWidth / srcWidth));

        // Also constrain height if a max is set (preserves aspect ratio)
        if (maxHeight > 0 && targetHeight > maxHeight)
        {
            targetHeight = maxHeight;
            targetWidth  = (int)(srcWidth * ((double)targetHeight / srcHeight));
        }

        // Snap to cell-size multiples to avoid partial cells
        targetHeight = Math.Max(8,  (targetHeight / 8) * 8);
        targetWidth  = Math.Max(4,  (targetWidth  / 4) * 4);

        bool hasAudio = mediaInfo.AudioStreams.Any();

        Console.WriteLine($"[*] Video: {srcWidth}x{srcHeight} @ {fps:F2} fps  |  Duration: {mediaInfo.Duration:hh\\:mm\\:ss}");
        Console.WriteLine($"[*] ASCII grid target: {targetWidth}x{targetHeight}  |  Estimated frames: {totalFrames}");
        Console.WriteLine($"[*] Audio track: {(hasAudio ? "found ✓" : "not found")}");
        Console.WriteLine("[*] Extracting and converting frames...");

        var animation = new AsciiAnimation
        {
            SourceFile = Path.GetFileName(videoPath),
            Width      = targetWidth,
            Height     = targetHeight,
            Fps        = fps,
            TotalFrames = totalFrames,
            CellWidth  = 4,
            CellHeight = 8,
        };

        _converter.ComputeGridDimensions(targetWidth, targetHeight);
        animation.CharColumns = _converter.CharColumns;
        animation.CharRows    = _converter.CharRows;

        // ── Step 1: Extract audio track ────────────────────────────────────
        if (hasAudio)
        {
            Directory.CreateDirectory(outputDir);
            string audioPath = Path.Combine(outputDir, "audio.mp3");
            Console.WriteLine("[*] Extracting audio track...");

            bool audioOk = await FFMpegArguments
                .FromFileInput(videoPath)
                .OutputToFile(audioPath, overwrite: true, options => options
                    .WithCustomArgument("-vn")          // no video
                    .WithCustomArgument("-acodec libmp3lame")
                    .WithCustomArgument("-q:a 4"))       // good quality, fast encode
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously(throwOnError: false);

            if (audioOk && File.Exists(audioPath))
            {
                animation.HasAudio  = true;
                animation.AudioFile = "audio.mp3"; // stored relative to output dir
                Console.WriteLine($"[✓] Audio extracted: {audioPath}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[!] Audio extraction failed — playback will be silent.");
                Console.ResetColor();
            }
        }

        // ── Step 2: Extract + convert frames ───────────────────────────────
        var frameList  = new List<string>();
        int frameCount = 0;
        string tempDir = Path.Combine(Path.GetTempPath(), $"termani_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string framePattern = Path.Combine(tempDir, "frame_%06d.png");

            // Scale to target size and convert to grayscale in one -vf pass
            await FFMpegArguments
                .FromFileInput(videoPath)
                .OutputToFile(framePattern, overwrite: true, options => options
                    .WithCustomArgument($"-vf \"scale={targetWidth}:{targetHeight},format=gray\"")
                    .WithCustomArgument("-start_number 1"))
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously(throwOnError: false);

            var frameFiles = Directory.GetFiles(tempDir, "frame_*.png")
                                      .OrderBy(f => f)
                                      .ToArray();

            totalFrames = frameFiles.Length;
            animation.TotalFrames = totalFrames;

            Console.WriteLine($"[*] Extracted {totalFrames} frames. Converting to ASCII...");

            for (int i = 0; i < frameFiles.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var image = await Image.LoadAsync<Rgba32>(frameFiles[i], cancellationToken);

                // Safety grayscale pass (FFmpeg already did it)
                image.Mutate(x => x.Grayscale());

                string asciiFrame = _converter.Convert(image);
                frameList.Add(asciiFrame);
                frameCount++;

                progress?.Invoke(frameCount, totalFrames);

                if (frameCount % 50 == 0 || frameCount == totalFrames)
                    PrintProgressBar(frameCount, totalFrames);
            }
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }

        animation.Frames = frameList;
        Console.WriteLine($"\n[✓] Conversion complete. Total frames: {animation.Frames.Count}");

        return animation;
    }

    private static void PrintProgressBar(int current, int total, int width = 40)
    {
        double pct    = total == 0 ? 1.0 : (double)current / total;
        int    filled = (int)(pct * width);
        string bar    = new string('█', filled) + new string('░', width - filled);
        Console.Write($"\r  [{bar}] {current}/{total} ({pct:P0})   ");
    }
}
