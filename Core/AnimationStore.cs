using System.Text.Json;
using TerminalAnimation.Models;

namespace TerminalAnimation.Core;

/// <summary>
/// Serializes and deserializes <see cref="AsciiAnimation"/> to/from disk.
/// Uses a custom binary-ish format: a JSON header file + a flat frames file
/// to keep memory usage reasonable for long videos.
/// </summary>
public static class AnimationStore
{
    private const string MetaFileName  = "animation.meta.json";
    private const string FramesFileName = "animation.frames";
    private const string FrameSeparator = "\x1E"; // ASCII Record Separator

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Saves the animation to <paramref name="outputPath"/>.
    /// If <paramref name="outputPath"/> is a directory, the files are placed inside it.
    /// If it ends with ".json", a single-file JSON is written (may be large).
    /// Otherwise a directory with meta + frames files is created.
    /// </summary>
    public static async Task SaveAsync(AsciiAnimation animation, string outputPath)
    {
        bool singleFile = outputPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        if (singleFile)
        {
            await SaveSingleFileAsync(animation, outputPath);
        }
        else
        {
            await SaveDirectoryAsync(animation, outputPath);
        }
    }

    /// <summary>
    /// Loads an animation from a path. 
    /// If the path is a directory, looks for the meta + frames files.
    /// If it's a <c>.json</c> file, loads a single-file JSON.
    /// </summary>
    public static async Task<AsciiAnimation> LoadAsync(string path)
    {
        if (File.Exists(path) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return await LoadSingleFileAsync(path);

        if (Directory.Exists(path))
            return await LoadDirectoryAsync(path);

        throw new FileNotFoundException($"Cannot find animation at: {path}");
    }

    // ── Single-file JSON ────────────────────────────────────────────────────

    private static async Task SaveSingleFileAsync(AsciiAnimation animation, string path)
    {
        Console.WriteLine($"[*] Writing single-file JSON to: {path}");
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, animation, JsonOptions);
        Console.WriteLine($"[✓] Saved {animation.Frames.Count} frames to {path}");
    }

    private static async Task<AsciiAnimation> LoadSingleFileAsync(string path)
    {
        await using var fs = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AsciiAnimation>(fs, JsonOptions)
               ?? throw new InvalidDataException("Failed to deserialize animation JSON.");
    }

    // ── Directory format (meta + frames) ───────────────────────────────────

    private static async Task SaveDirectoryAsync(AsciiAnimation animation, string dir)
    {
        Directory.CreateDirectory(dir);

        // Save metadata without frames array (to keep the meta file small)
        var meta = new AsciiAnimation
        {
            Version     = animation.Version,
            SourceFile  = animation.SourceFile,
            Width       = animation.Width,
            Height      = animation.Height,
            Fps         = animation.Fps,
            TotalFrames = animation.TotalFrames,
            CellWidth   = animation.CellWidth,
            CellHeight  = animation.CellHeight,
            CharColumns = animation.CharColumns,
            CharRows    = animation.CharRows,
            HasAudio    = animation.HasAudio,    // ← fix: kasama na ngayon
            AudioFile   = animation.AudioFile,   // ← fix: kasama na ngayon
            Frames      = [] // excluded from meta file
        };

        string metaPath   = Path.Combine(dir, MetaFileName);
        string framesPath = Path.Combine(dir, FramesFileName);

        Console.WriteLine($"[*] Writing metadata to: {metaPath}");
        await using (var metaFs = File.Create(metaPath))
            await JsonSerializer.SerializeAsync(metaFs, meta, JsonOptions);

        Console.WriteLine($"[*] Writing {animation.Frames.Count} frames to: {framesPath}");
        await using var framesFs = File.Create(framesPath);
        await using var writer   = new StreamWriter(framesFs, System.Text.Encoding.UTF8);

        for (int i = 0; i < animation.Frames.Count; i++)
        {
            await writer.WriteAsync(animation.Frames[i]);
            await writer.WriteAsync(FrameSeparator);
        }

        Console.WriteLine($"[✓] Saved animation to directory: {dir}");
    }

    private static async Task<AsciiAnimation> LoadDirectoryAsync(string dir)
    {
        string metaPath   = Path.Combine(dir, MetaFileName);
        string framesPath = Path.Combine(dir, FramesFileName);

        if (!File.Exists(metaPath))
            throw new FileNotFoundException($"Meta file not found: {metaPath}");
        if (!File.Exists(framesPath))
            throw new FileNotFoundException($"Frames file not found: {framesPath}");

        AsciiAnimation animation;
        await using (var fs = File.OpenRead(metaPath))
            animation = await JsonSerializer.DeserializeAsync<AsciiAnimation>(fs, JsonOptions)
                        ?? throw new InvalidDataException("Failed to deserialize meta file.");

        Console.WriteLine($"[*] Loading frames from: {framesPath}");
        string allFrames = await File.ReadAllTextAsync(framesPath, System.Text.Encoding.UTF8);
        var frames = allFrames.Split(FrameSeparator, StringSplitOptions.RemoveEmptyEntries);
        animation.Frames = [.. frames];

        Console.WriteLine($"[✓] Loaded {animation.Frames.Count} frames.");
        return animation;
    }
}
