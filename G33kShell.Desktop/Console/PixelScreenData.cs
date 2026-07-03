// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
// purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using DTC.Core;
using DTC.Core.Extensions;

namespace G33kShell.Desktop.Console;

/// <summary>
/// Represents a low-resolution, palette-indexed pixel surface.
/// </summary>
public class PixelScreenData
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
    public Rgb[] Palette { get; }

    public PixelScreenData(int width, int height, Rgb[] palette)
    {
        if (width <= 0)
            throw new ArgumentException("Width must be greater than zero.", nameof(width));
        if (height <= 0)
            throw new ArgumentException("Height must be greater than zero.", nameof(height));
        if (palette == null || palette.Length == 0 || palette.Length > 256)
            throw new ArgumentException("Palette must contain between 1 and 256 colors.", nameof(palette));

        Width = width;
        Height = height;
        Pixels = new byte[width * height];
        Palette = palette;
    }

    public void Clear(byte colorIndex = 0) =>
        Array.Fill(Pixels, colorIndex);

    public void SetPalette(Rgb[] palette)
    {
        if (palette == null || palette.Length != Palette.Length)
            throw new ArgumentException("Palette must contain the same number of colors as the current palette.", nameof(palette));
        Array.Copy(palette, Palette, palette.Length);
    }

    public static Rgb[] CreateGreyscalePalette(Rgb[] sourcePalette, Rgb background, Rgb foreground)
    {
        if (sourcePalette == null)
            throw new ArgumentNullException(nameof(sourcePalette));

        var palette = new Rgb[sourcePalette.Length];
        for (var i = 0; i < sourcePalette.Length; i++)
            palette[i] = sourcePalette[i].Luminosity().Lerp(background, foreground);
        return palette;
    }

    public void SetPixel(int x, int y, byte colorIndex)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return;
        Pixels[y * Width + x] = colorIndex;
    }

    public void DrawLine(int x0, int y0, int x1, int y1, byte colorIndex)
    {
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;

        while (true)
        {
            SetPixel(x0, y0, colorIndex);
            if (x0 == x1 && y0 == y1)
                return;

            var e2 = 2 * error;
            if (e2 >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    public void DrawAntialiasedLine(double x0, double y0, double x1, double y1, byte colorIndex, bool blend = true)
    {
        var steep = Math.Abs(y1 - y0) > Math.Abs(x1 - x0);
        if (steep)
        {
            (x0, y0) = (y0, x0);
            (x1, y1) = (y1, x1);
        }

        if (x0 > x1)
        {
            (x0, x1) = (x1, x0);
            (y0, y1) = (y1, y0);
        }

        var dx = x1 - x0;
        var dy = y1 - y0;
        if (dx < 0.001)
        {
            DrawAntialiasedVerticalLine(steep, x0, y0, y1, colorIndex, blend);
            return;
        }

        var gradient = dy / dx;
        var firstX = Round(x0);
        var firstY = y0 + gradient * (firstX - x0);
        var firstCoverage = ReverseFractionalPart(x0 + 0.5);
        PlotAntialiasedLinePair(steep, firstX, firstY, firstCoverage, colorIndex, blend);

        var interY = firstY + gradient;
        var lastX = Round(x1);
        var lastY = y1 + gradient * (lastX - x1);
        var lastCoverage = FractionalPart(x1 + 0.5);

        for (var x = firstX + 1; x < lastX; x++)
        {
            PlotAntialiasedLinePair(steep, x, interY, 1.0, colorIndex, blend);
            interY += gradient;
        }

        PlotAntialiasedLinePair(steep, lastX, lastY, lastCoverage, colorIndex, blend);
    }

    private void DrawAntialiasedVerticalLine(bool steep, double x, double y0, double y1, byte colorIndex, bool blend)
    {
        if (y0 > y1)
            (y0, y1) = (y1, y0);

        var ix = Round(x);
        var firstY = Round(y0);
        var lastY = Round(y1);
        for (var y = firstY; y <= lastY; y++)
            PlotAntialiasedPixel(steep, ix, y, 1.0, colorIndex, blend);
    }

    private void PlotAntialiasedLinePair(bool steep, int x, double y, double coverage, byte colorIndex, bool blend)
    {
        var wholeY = (int)Math.Floor(y);
        PlotAntialiasedPixel(steep, x, wholeY, ReverseFractionalPart(y) * coverage, colorIndex, blend);
        PlotAntialiasedPixel(steep, x, wholeY + 1, FractionalPart(y) * coverage, colorIndex, blend);
    }

    private void PlotAntialiasedPixel(bool steep, int x, int y, double coverage, byte colorIndex, bool blend)
    {
        if (coverage <= 0.0)
            return;

        if (steep)
            (x, y) = (y, x);

        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return;

        var index = y * Width + x;
        Pixels[index] = blend ? BlendColorIndex(Pixels[index], colorIndex, coverage) : colorIndex;
    }

    private static byte BlendColorIndex(byte currentColorIndex, byte targetColorIndex, double coverage) =>
        (byte)Math.Clamp(
            (int)Math.Round(currentColorIndex + (targetColorIndex - currentColorIndex) * Math.Clamp(coverage, 0.0, 1.0)),
            byte.MinValue,
            byte.MaxValue);

    private static int Round(double value) =>
        (int)Math.Floor(value + 0.5);

    private static double FractionalPart(double value) =>
        value - Math.Floor(value);

    private static double ReverseFractionalPart(double value) =>
        1.0 - FractionalPart(value);
}
