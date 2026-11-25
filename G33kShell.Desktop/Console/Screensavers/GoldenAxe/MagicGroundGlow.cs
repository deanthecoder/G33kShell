// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// A patch of floor heated by magic, fading after the flame moves away.
/// </summary>
internal readonly record struct MagicGroundGlow(double X, double Z, double Y, int Tick, double Strength, string MagicKey, int StartTick)
{
    public const int HeatTicks = 30;
    public const int FadeTicks = 120;
}
