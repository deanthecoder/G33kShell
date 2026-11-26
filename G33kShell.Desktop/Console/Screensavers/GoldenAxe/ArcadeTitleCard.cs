// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// for any purpose. The software is provided as is, without warranty of any kind.

using System;
using System.Collections.Generic;
using System.IO;
using DTC.Core;
using SkiaSharp;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>Loads and draws an opening image on the indexed surface.</summary>
internal sealed record ArcadeTitleCard(byte[] Pixels, Rgb[] Palette)
{
    internal const int DurationTicks = 150;

    internal static ArcadeTitleCard Load(string fileName, int paletteSize) =>
        TryLoad(Path.Combine(ArcadeData.LocalAssetDirectory, fileName), paletteSize)
        ?? TryLoad(Path.Combine(ArcadeData.AssetDirectory, fileName), paletteSize);

    internal void Draw(PixelScreenData screen, Rgb[] palette, double brightness)
    {
        screen.Clear();
        screen.SetPalette(palette);
        ApplyBrightness(screen, brightness);
        Pixels.CopyTo(screen.Pixels, 0);
    }

    internal static void ApplyBrightness(PixelScreenData screen, double brightness)
    {
        if (brightness < 1)
        {
            for (var i = 0; i < screen.Palette.Length; i++)
            {
                var color = screen.Palette[i];
                screen.Palette[i] = new Rgb((byte)(color.R * brightness),
                    (byte)(color.G * brightness), (byte)(color.B * brightness));
            }
        }
    }

    internal static ArcadeTitleCard TryLoad(string path, int paletteSize, int height = ArcadeData.Height)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var codec = SKCodec.Create(stream);
            if (codec == null || codec.Info.Width != ArcadeData.Width || codec.Info.Height != height)
                return null;

            using var bitmap = SKBitmap.Decode(codec);
            if (bitmap == null)
                return null;

            var pens = new Dictionary<SKColor, byte>();
            var palette = new Rgb[paletteSize];
            Array.Fill(palette, Rgb.Black);
            var source = bitmap.Pixels;
            var pixels = new byte[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var color = source[i];
                if (color.Alpha != 255)
                    return null;

                if (!pens.TryGetValue(color, out var pen))
                {
                    if (pens.Count == palette.Length)
                        return null;

                    pen = (byte)pens.Count;
                    pens.Add(color, pen);
                    palette[pen] = new Rgb(color.Red, color.Green, color.Blue);
                }
                pixels[i] = pen;
            }
            return new ArcadeTitleCard(pixels, palette);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
