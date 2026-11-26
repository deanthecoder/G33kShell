// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using DTC.Core;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

internal static class TurtleVillageSurf
{
    public const int Area = 1;
    public const int StartProgress = 4944;
    public const int Palette = 0x21;
    public const int PaletteStart = 244;
    public const int StripWidth = 56;
    public const int Depth = 4352;
    private static readonly int[] s_frames = [487, 486, 488];

    public static int Frame(int age) => s_frames[Math.Max(0, age - 1) / 6 % s_frames.Length];

    public static int X(int age) => 4950 - Math.Max(0, age - 1) % 28 * 2;

    public static byte Opacity(int visibleRows)
    {
        var fade = Math.Clamp((visibleRows - 8) / 16.0, 0, 1);
        return (byte)Math.Round(255 * fade * fade * (3 - 2 * fade));
    }

    internal static void DrawHighlights(PixelScreenData screen, int age, int top, int cameraX, int opacity, byte paleWater)
    {
        // Let the sprite supply the crest shapes. Only its brighter, water-exclusive palette slots catch light.
        for (var y = Math.Max(0, top); y < ArcadeData.Height; y++)
        {
            var row = y - top;
            var phase = age * 0.027 + row * 0.30;
            var center = cameraX + (int)Math.Round(23 * Math.Sin(phase) + row * 3);
            const int spacing = 113, radius = 23;
            center = (center % spacing + spacing) % spacing - spacing;
            for (; center - radius < ArcadeData.Width; center += spacing)
            for (var x = Math.Max(0, center - radius); x < Math.Min(ArcadeData.Width, center + radius); x++)
            {
                var offset = y * ArcadeData.Width + x;
                var crest = screen.Pixels[offset] - PaletteStart;
                if (crest is < 0 or > 9)
                    continue;
                var falloff = 1 - Math.Abs(x - center) / (double)radius;
                // Midtones carry the visible reflection; foam is already close to paleWater.
                var reflectivity = 0.8 - crest * 0.06;
                var weight = (byte)(opacity * reflectivity * falloff);
                if (weight > 0)
                    screen.DustPixel(offset, paleWater, weight);
            }
        }
    }

    public static Rgb LiftMidtones(int rgb)
    {
        // Partially compensate for the gameplay output curve on the water alone.
        byte Lift(int shift) => (byte)Math.Round(255 * Math.Pow((rgb >> shift & 255) / 255.0, 0.78));
        return new Rgb(Lift(16), Lift(8), Lift(0));
    }

    public static double Y(int age)
    {
        // The first three swells lift the surf into place. Afterwards it bobs over 16 pixels.
        var cycle = age / 128;
        var phase = age % 128;
        var swell = (1 - Math.Cos(Math.Tau * phase / 128)) * 0.5;
        if (cycle < 3)
        {
            var start = 5072 - cycle * 16;
            return phase < 64 ? start + 8 * swell : start - 16 + 24 * swell;
        }
        return 5024 + 16 * swell;
    }
}
