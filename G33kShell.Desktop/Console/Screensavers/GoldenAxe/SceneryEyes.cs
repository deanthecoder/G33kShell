// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Animates the original turtle and eagle eyes at their tile-map positions.
/// </summary>
/// <remarks>
/// The exported atlas contains three gaze directions for each eye, but no animation
/// scripts. Turtle timing, palette and position match the MAME set-6 trace;
/// the eagle timing and palette still await verification.
/// </remarks>
internal static class SceneryEyes
{
    public const int TurtlePalette = 0x8e;
    public const int EaglePalette = 0x8f;

    public static (int Frame, int Palette, int X, int Y)? Sample(int area, bool camp, int tick)
    {
        if (camp)
            return null;

        if (area == 2)
        {
            var turtlePose = (tick % 130) switch { < 100 => 0, < 110 => 1, < 120 => 2, _ => 1 };
            // Background anchor (683,488), with the original frame offset (-13,-24).
            return (1079 + turtlePose, TurtlePalette, 670, 464);
        }

        // Hold each gaze before looking across and returning through the middle.
        var pose = (tick % 360) switch
        {
            < 90 => 0,
            < 150 => 1,
            < 240 => 2,
            _ => 1
        };
        return area switch
        {
            4 => (1174 + pose, EaglePalette, 569, 304),
            _ => null
        };
    }
}
