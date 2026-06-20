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
}
