// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// A dragon smoke emission anchored in the world after the flame moves away.
/// </summary>
internal readonly record struct SmokePuff(double X, double Z, double Y, int Tick, double Scale = 1,
    double Soot = 1, double Opacity = 1, int Duration = 120);
