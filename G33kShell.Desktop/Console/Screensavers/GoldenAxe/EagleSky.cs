// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// for any purpose. Please retain this copyright header.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using DTC.Core;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Independently scrolling eagle clouds and the progress-driven sunset, from MAME set-6 captures.
/// </summary>
internal static class EagleSky
{
    public const int PaletteStart = 248;
    public const int FirstCapturedProgress = 4990;
    public static readonly uint[] Blue = [0x73adce, 0x7bb5d6, 0x84bdde, 0x8cc5e6, 0x94ceef, 0x9cd6f7, 0xa5deff, 0xade6ff];

    public static (int X, int Y) Scroll(int tick, int[] capturedX)
    {
        tick = Math.Max(0, tick);
        // The captured acceleration settles at 6 pixels/tick; all four hardware pages
        // contain the same 512x256 cloud pattern. Vertical motion is 1.5 pixels/tick.
        var x = tick < capturedX.Length ? capturedX[tick] : capturedX[^1] + (tick - capturedX.Length + 1) * 6;
        var y = 42 - (int)(tick * 1.5);
        return (x & 511, y & 255);
    }

    public static Rgb Color(uint blue, uint peach, double progress)
    {
        var amount = Math.Clamp((progress - 5055) / 52, 0, 1);
        byte Channel(int shift) => (byte)Math.Round((blue >> shift & 255) * (1 - amount) + (peach >> shift & 255) * amount);
        return new Rgb(Channel(16), Channel(8), Channel(0));
    }
}
