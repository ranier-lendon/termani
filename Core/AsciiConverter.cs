using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text;

namespace TerminalAnimation.Core;

/// <summary>
/// Converts grayscale images into ASCII art strings by sampling a cell grid
/// and mapping average brightness to a character density ramp.
/// </summary>
public class AsciiConverter
{
    // Density ramp: index 0 = darkest (fully filled), last = lightest (space)
    private static readonly char[] DensityRamp =
        "@%#*+=-:'. ".ToCharArray();

    // Extended ramp for finer gradation (70 shades)
    private static readonly string ExtendedRamp =
        "$@B%8&WM#*oahkbdpqwmZO0QLCJUYXzcvunxrjft/\\|()1{}[]?-_+~<>i!lI;:,\"^`'. ";

    private readonly int _cellWidth;
    private readonly int _cellHeight;
    private readonly bool _useExtendedRamp;

    /// <summary>Number of character columns produced per frame.</summary>
    public int CharColumns { get; private set; }

    /// <summary>Number of character rows produced per frame.</summary>
    public int CharRows { get; private set; }

    public AsciiConverter(int cellWidth = 8, int cellHeight = 16, bool useExtendedRamp = false)
    {
        _cellWidth = cellWidth;
        _cellHeight = cellHeight;
        _useExtendedRamp = useExtendedRamp;
    }

    /// <summary>
    /// Computes character grid dimensions for a given image size.
    /// </summary>
    public void ComputeGridDimensions(int imageWidth, int imageHeight)
    {
        CharColumns = imageWidth / _cellWidth;
        CharRows = imageHeight / _cellHeight;
    }

    /// <summary>
    /// Converts an RGBA32 image (should already be grayscale) to an ASCII frame string.
    /// Each row of characters is separated by a newline.
    /// </summary>
    public string Convert(Image<Rgba32> image)
    {
        ComputeGridDimensions(image.Width, image.Height);

        var sb = new StringBuilder(CharColumns * CharRows + CharRows);
        var ramp = _useExtendedRamp ? ExtendedRamp : new string(DensityRamp);

        for (int row = 0; row < CharRows; row++)
        {
            for (int col = 0; col < CharColumns; col++)
            {
                double avgBrightness = ComputeCellBrightness(image, col, row);

                // Map brightness to ramp: 1.0 (white) → index 0 → dense char (@, #)
                //                         0.0 (black) → last index → space
                int index = Math.Clamp((int)(avgBrightness * (ramp.Length - 1)), 0, ramp.Length - 1);
                char c = ramp[ramp.Length - 1 - index]; // invert: bright = dense, dark = space
                sb.Append(c);
            }
            sb.Append('\n');
        }

        return sb.ToString();
    }

    private double ComputeCellBrightness(Image<Rgba32> image, int col, int row)
    {
        int startX = col * _cellWidth;
        int startY = row * _cellHeight;
        int endX = Math.Min(startX + _cellWidth, image.Width);
        int endY = Math.Min(startY + _cellHeight, image.Height);

        long total = 0;
        int count = 0;

        // ProcessPixelRows provides zero-allocation access to pixel rows in ImageSharp 3.x
        image.ProcessPixelRows(accessor =>
        {
            for (int y = startY; y < endY; y++)
            {
                var pixelRow = accessor.GetRowSpan(y);
                for (int x = startX; x < endX; x++)
                {
                    // Since the image is grayscale, R = G = B; use R channel
                    total += pixelRow[x].R;
                    count++;
                }
            }
        });

        return count == 0 ? 0 : total / (double)(count * 255);
    }
}
