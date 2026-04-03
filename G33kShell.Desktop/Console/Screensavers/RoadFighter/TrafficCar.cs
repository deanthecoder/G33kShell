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

namespace G33kShell.Desktop.Console.Screensavers.RoadFighter;

public sealed class TrafficCar
{
    public int Lane { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Speed { get; set; }
    public bool PassedPlayer { get; set; }
}
