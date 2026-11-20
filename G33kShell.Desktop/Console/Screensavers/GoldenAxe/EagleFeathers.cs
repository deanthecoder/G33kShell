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
/// Samples drifting feathers from the original sprite atlas without adding combat actors.
/// </summary>
/// <remarks>
/// Motion and palette selection approximate the arcade effect. Simulation ticks keep
/// animation deterministic and independent of rendering frequency.
/// </remarks>
internal static class EagleFeathers
{
    public const int Palette = 0x80;
    public const int Count = 8;
    public const int BackgroundCount = 5;
    public const int StartProgress = 4674;

    public static bool IsVisible(Game game) => game.AreaIndex == 3 && !game.IsCamp && game.Progress >= StartProgress;

    public static (int Frame, double X, double Y, bool Flip) Sample(int index, int tick, double cameraX)
    {
        var foreground = index >= BackgroundCount;
        var speed = (foreground ? 2.4 : 1.3) + index % 3 * 0.3;
        const int span = ArcadeData.Width + 96;
        var travel = index * 67.0 - tick * speed + cameraX * (foreground ? 0.55 : 0.25);
        var x = (travel % span + span) % span - 48;
        var y = 66 + index * 37 % 120 + Math.Sin(tick * 0.045 + index * 1.7) * 8;
        var phase = (tick / (foreground ? 5 : 7) + index * 3) % 9;
        var frame = (index % 2 == 0 ? 1024 : 1033) + phase;
        return (frame, x, y, index % 3 == 0);
    }
}
