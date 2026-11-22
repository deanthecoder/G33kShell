// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// for any purpose. The software is provided as is, without warranty of any kind.

using System;
using System.Collections.Generic;
using System.IO;
using DTC.Core;
using SkiaSharp;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>Loads an optional local title image into the screensaver's indexed surface.</summary>
internal sealed record ArcadeTitleCard(byte[] Pixels, Rgb[] Palette)
{
    internal const int DurationTicks = 150;

    internal static ArcadeTitleCard LoadLocal(int paletteSize) =>
        TryLoad(Path.Combine(ArcadeData.LocalAssetDirectory, "title.png"), paletteSize);

    internal static ArcadeTitleCard TryLoad(string path, int paletteSize)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var codec = SKCodec.Create(stream);
            if (codec == null || codec.Info.Width != ArcadeData.Width || codec.Info.Height != ArcadeData.Height)
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
