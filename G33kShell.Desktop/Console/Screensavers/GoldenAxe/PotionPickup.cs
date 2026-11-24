// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Keeps a collected potion's world burst and screen-space journey to the magic meter alive.
/// </summary>
internal readonly record struct PotionPickup(double X, double Z, double Y, double ScreenX, double ScreenY, int Tick, int Slot)
{
    public const int Lifetime = 64;
}
