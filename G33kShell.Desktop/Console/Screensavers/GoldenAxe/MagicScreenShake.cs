// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Shakes the scene throughout a spell without moving gameplay coordinates.
/// </summary>
internal static class MagicScreenShake
{
    public static (int X, int Y) Sample(int tick, int duration, int level)
    {
        if (tick < 0 || tick >= duration)
            return default;

        var envelope = Math.Min(1, Math.Min((tick + 1) / 8.0, (duration - tick) / 12.0));
        var strength = Math.Max(1, Math.Min(4, 1 + level * 0.5) * envelope);
        ReadOnlySpan<int> horizontal = [1, -1, 1, 0, -1, 1, -1, 0];
        ReadOnlySpan<int> vertical = [-1, 1, 0, -1, 1, 0, -1, 1];
        var phase = tick / 2 % horizontal.Length;
        return ((int)Math.Round(horizontal[phase] * strength), (int)Math.Round(vertical[phase] * strength * 0.6));
    }
}
