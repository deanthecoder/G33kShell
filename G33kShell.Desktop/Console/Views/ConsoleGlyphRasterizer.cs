// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
// purpose.

using SkiaSharp;

namespace G33kShell.Desktop.Console.Views;

/// <summary>
/// Rasterizes a console glyph to its fixed 8x16 cell mask.
/// </summary>
internal static class ConsoleGlyphRasterizer
{
    private const int CharWidth = 8;
    private const int CharHeight = 16;
    private const int Padding = 2;

    /// <summary>
    /// Produces an alpha mask without clipping glyphs which touch a cell edge.
    /// </summary>
    public static byte[] Rasterize(SKPaint paint, char ch)
    {
        var imageInfo = new SKImageInfo(
            CharWidth + Padding * 2,
            CharHeight + Padding * 2,
            SKColorType.Alpha8,
            SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        // The font's ascent defines the baseline for the top of a 16-pixel cell.
        // Drawing into a padded bitmap means edge glyphs (such as █ and │) cannot
        // be clipped before we extract the actual cell.
        var baseline = Padding - paint.FontMetrics.Ascent;
        canvas.DrawText(ch.ToString(), Padding, baseline, paint);

        var pixels = bitmap.Bytes;
        var coverage = new byte[CharWidth * CharHeight];
        for (var y = 0; y < CharHeight; y++)
        {
            for (var x = 0; x < CharWidth; x++)
                coverage[y * CharWidth + x] = pixels[(y + Padding) * bitmap.RowBytes + x + Padding];
        }

        return coverage;
    }
}
