using System.Text.Json.Serialization;

namespace TerminalAnimation.Models;

/// <summary>
/// Metadata and frame data for a stored ASCII animation.
/// </summary>
public class AsciiAnimation
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("sourceFile")]
    public string SourceFile { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("fps")]
    public double Fps { get; set; }

    [JsonPropertyName("totalFrames")]
    public int TotalFrames { get; set; }

    [JsonPropertyName("cellWidth")]
    public int CellWidth { get; set; }

    [JsonPropertyName("cellHeight")]
    public int CellHeight { get; set; }

    [JsonPropertyName("charColumns")]
    public int CharColumns { get; set; }

    [JsonPropertyName("charRows")]
    public int CharRows { get; set; }

    /// <summary>True when an audio track was extracted alongside the frames.</summary>
    [JsonPropertyName("hasAudio")]
    public bool HasAudio { get; set; }

    /// <summary>
    /// Relative or absolute path to the extracted audio file.
    /// Only meaningful when <see cref="HasAudio"/> is true.
    /// </summary>
    [JsonPropertyName("audioFile")]
    public string AudioFile { get; set; } = string.Empty;

    [JsonPropertyName("frames")]
    public List<string> Frames { get; set; } = [];
}
