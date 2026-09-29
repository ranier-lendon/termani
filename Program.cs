using FFMpegCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;
using TerminalAnimation.Core;
using TerminalAnimation.Models;

// ─── Banner ────────────────────────────────────────────────────────────────
PrintBanner();

// ─── Parse Arguments ───────────────────────────────────────────────────────
if (args.Length < 2)
{
    PrintUsage();
    return 1;
}

string command = args[0].ToLowerInvariant();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\n[!] Cancellation requested...");
};

int exitCode = command switch
{
    "convert" => await RunConvert(args[1..], cts.Token),
    "play"    => await RunPlay(args[1..], cts.Token),
    "info"    => await RunInfo(args[1..]),
    _         => PrintUsageAndReturn()
};

return exitCode;

// ─── Convert Command ────────────────────────────────────────────────────────
async Task<int> RunConvert(string[] convertArgs, CancellationToken token)
{
    string videoPath   = convertArgs[0];
    string outputPath  = convertArgs.Length > 1 ? convertArgs[1]
                       : Path.ChangeExtension(videoPath, null);
    bool   singleFile  = outputPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    int    maxWidth    = -1;    // -1 = auto-detect from terminal size
    bool   extendedRamp = false;
    int    cellW = 4, cellH = 8;

    // Parse optional flags
    for (int i = 2; i < convertArgs.Length; i++)
    {
        if (convertArgs[i] == "--width" && i + 1 < convertArgs.Length)
            maxWidth = int.Parse(convertArgs[++i]);
        else if (convertArgs[i] == "--extended-ramp")
            extendedRamp = true;
        else if (convertArgs[i] == "--cell" && i + 2 < convertArgs.Length)
        {
            cellW = int.Parse(convertArgs[++i]);
            cellH = int.Parse(convertArgs[++i]);
        }
        else if (convertArgs[i] == "--single-file")
        {
            outputPath = Path.ChangeExtension(videoPath, ".ascii.json");
        }
    }

    // ── Auto-detect terminal size ──────────────────────────────────────────
    // Do this AFTER parsing flags so --cell is already set before computing width
    int maxHeight = 0; // 0 = no height constraint (used when --width is set manually)
    if (maxWidth < 0)
    {
        const int headerRows = 2;  // top header + divider
        const int statusRows = 2;  // status bar + bottom padding
        const int colMargin  = 2;  // avoid terminal line-wrap artefacts
        const int rowMargin  = 1;  // one spare row at the bottom

        int termCols = Console.WindowWidth;
        int termRows = Console.WindowHeight;

        int targetCharCols = Math.Max(20, termCols - colMargin);
        int targetCharRows = Math.Max(5,  termRows - headerRows - statusRows - rowMargin);

        maxWidth  = targetCharCols * cellW;
        maxHeight = targetCharRows * cellH;

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"[*] Auto-size: terminal {termCols}x{termRows} → " +
                          $"{targetCharCols} cols × {targetCharRows} rows " +
                          $"(pixel target: {maxWidth}x{maxHeight})");
        Console.ResetColor();
    }

    // Verify FFmpeg is available
    if (!await VerifyFfmpegAsync())
        return 1;


    var converter = new AsciiConverter(cellW, cellH, extendedRamp);
    var processor = new VideoProcessor(converter);

    var sw = Stopwatch.StartNew();
    AsciiAnimation animation;

    try
    {
        animation = await processor.ProcessAsync(
            videoPath,
            outputDir: outputPath,
            maxWidth:  maxWidth,
            maxHeight: maxHeight,
            cancellationToken: token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("\n[!] Conversion cancelled.");
        return 1;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n[✗] Conversion failed: {ex.Message}");
        Console.ResetColor();
        return 1;
    }

    sw.Stop();
    Console.WriteLine($"[*] Conversion took {sw.Elapsed:mm\\:ss\\.ff}");

    try
    {
        await AnimationStore.SaveAsync(animation, outputPath);
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[✗] Save failed: {ex.Message}");
        Console.ResetColor();
        return 1;
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n[✓] Done! Output: {outputPath}");
    Console.ResetColor();
    Console.WriteLine($"    To play:  TerminalAnimation play \"{outputPath}\"");
    return 0;
}

// ─── Play Command ───────────────────────────────────────────────────────────
async Task<int> RunPlay(string[] playArgs, CancellationToken token)
{
    string path = playArgs[0];
    bool   loop = playArgs.Contains("--loop");

    AsciiAnimation animation;
    try
    {
        animation = await AnimationStore.LoadAsync(path);
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[✗] Could not load animation: {ex.Message}");
        Console.ResetColor();
        return 1;
    }

    // Warn if terminal is too small
    int neededW = animation.CharColumns;
    int neededH = animation.CharRows + 5; // header + status
    if (Console.WindowWidth < neededW || Console.WindowHeight < neededH)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[!] Warning: Terminal is {Console.WindowWidth}x{Console.WindowHeight} " +
                          $"but animation needs {neededW}x{neededH} chars.");
        Console.WriteLine("    Resize your terminal for best results. Press any key to continue...");
        Console.ResetColor();
        Console.ReadKey(intercept: true);
    }

    // Resolve the animation directory for audio file lookup
    string? animDir = Directory.Exists(path) ? path
                    : File.Exists(path)       ? Path.GetDirectoryName(path)
                    : null;

    var player = new AsciiPlayer(animation, animDir);

    try
    {
        await player.PlayAsync(loop, token);
    }
    catch (OperationCanceledException) { }

    return 0;
}

// ─── Info Command ───────────────────────────────────────────────────────────
async Task<int> RunInfo(string[] infoArgs)
{
    if (infoArgs.Length == 0)
    {
        Console.WriteLine("[!] Usage: info <path>");
        return 1;
    }

    try
    {
        var animation = await AnimationStore.LoadAsync(infoArgs[0]);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n── Animation Info ───────────────────────────────────");
        Console.ResetColor();
        Console.WriteLine($"  Source:      {animation.SourceFile}");
        Console.WriteLine($"  Dimensions:  {animation.Width}x{animation.Height} px → {animation.CharColumns}x{animation.CharRows} chars");
        Console.WriteLine($"  Cell size:   {animation.CellWidth}x{animation.CellHeight} px");
        Console.WriteLine($"  Frame rate:  {animation.Fps:F2} fps");
        Console.WriteLine($"  Total frames:{animation.TotalFrames}");
        Console.WriteLine($"  Duration:    {TimeSpan.FromSeconds(animation.TotalFrames / animation.Fps):hh\\:mm\\:ss}");
        Console.WriteLine($"  Loaded frames: {animation.Frames.Count}");
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[✗] {ex.Message}");
        Console.ResetColor();
        return 1;
    }

    return 0;
}

// ─── Helpers ────────────────────────────────────────────────────────────────
async Task<bool> VerifyFfmpegAsync()
{
    try
    {
        // Try to run ffmpeg -version to check availability
        var psi = new ProcessStartInfo("ffmpeg", "-version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        using var proc = Process.Start(psi);
        if (proc is null) throw new Exception();
        await proc.WaitForExitAsync();
        return true;
    }
    catch
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("[✗] FFmpeg not found. Please install FFmpeg and ensure it is on your PATH.");
        Console.WriteLine("    Download: https://ffmpeg.org/download.html");
        Console.ResetColor();
        return false;
    }
}

void PrintBanner()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(@"
  ████████╗███████╗██████╗ ███╗   ███╗ █████╗ ███╗   ██╗██╗
  ╚══██╔══╝██╔════╝██╔══██╗████╗ ████║██╔══██╗████╗  ██║██║
     ██║   █████╗  ██████╔╝██╔████╔██║███████║██╔██╗ ██║██║
     ██║   ██╔══╝  ██╔══██╗██║╚██╔╝██║██╔══██║██║╚██╗██║██║
     ██║   ███████╗██║  ██║██║ ╚═╝ ██║██║  ██║██║ ╚████║██║
     ╚═╝   ╚══════╝╚═╝  ╚═╝╚═╝     ╚═╝╚═╝  ╚═╝╚═╝  ╚═══╝╚═╝
");
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.WriteLine("  ASCII Video Player  —  Convert & play any video in your terminal\n");
    Console.ResetColor();
}

void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine("  TerminalAnimation convert <video> [output] [options]");
    Console.WriteLine("  TerminalAnimation play    <path>  [--loop]");
    Console.WriteLine("  TerminalAnimation info    <path>");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  convert   Extract frames, apply grayscale + ASCII conversion, save to disk");
    Console.WriteLine("  play      Play a previously converted ASCII animation");
    Console.WriteLine("  info      Show metadata about a saved animation");
    Console.WriteLine();
    Console.WriteLine("Convert options:");
    Console.WriteLine("  --width <N>          Max frame width in pixels (default: 480)");
    Console.WriteLine("  --cell <W> <H>       Cell size in pixels (default: 4 8)");
    Console.WriteLine("  --extended-ramp      Use 70-character density ramp for finer detail");
    Console.WriteLine("  --single-file        Save as a single .json file instead of a directory");
    Console.WriteLine();
    Console.WriteLine("Play controls:");
    Console.WriteLine("  Q / Escape   Quit");
    Console.WriteLine("  Space        Pause / Resume");
    Console.WriteLine("  ◄ / ►        Scrub frames while paused");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  TerminalAnimation convert myvideo.mp4");
    Console.WriteLine("  TerminalAnimation convert myvideo.mp4 output --width 120 --extended-ramp");
    Console.WriteLine("  TerminalAnimation play myvideo_ascii --loop");
    Console.WriteLine("  TerminalAnimation play myvideo.ascii.json");
    Console.ResetColor();
}

int PrintUsageAndReturn()
{
    PrintUsage();
    return 1;
}
