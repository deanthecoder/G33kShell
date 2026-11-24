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

    public static Rgb LiftMidtones(int rgb)
    {
        // Partially compensate for the gameplay output curve on the water alone.
        byte Lift(int shift) => (byte)Math.Round(255 * Math.Pow((rgb >> shift & 255) / 255.0, 0.78));
        return new Rgb(Lift(16), Lift(8), Lift(0));
    }

    public static int Y(int age)
    {
        // The first three swells lift the surf into place. Afterwards it bobs over 16 pixels.
        var cycle = age / 128;
        var step = age % 128 / 8;
        if (cycle < 3)
            return 5072 - cycle * 16 + (step <= 8 ? step : 8 - (step - 8) * 3);
        return 5024 + 2 * Math.Min(step, 16 - step);
    }
}
