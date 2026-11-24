// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

internal static class ArcadeCameraLimits
{
    // US set 6 lower camera limit table (0xcd4a). Depth increases downwards.
    private static readonly (int Start, int Depth, int Divisor, int Slope)[] s_lower =
    [
        (0x0000, 704, 1, 0),
        (0x2048, 768, 4, 1),
        (0x2188, 848, 11, 10),
        (0x21e0, 928, 8, -1),
        (0x2350, 816, 1, 0),
        (0x24bf, 792, 1, 0),
        (0x2ff8, 856, 1, 0),
        (0x3180, 856, 1, -1),
        (0x31c0, 792, 1, 0),
        (0x3268, 792, 1, -1),
        (0x32b0, 720, 1, 0),
        (0x4270, 744, 1, 0),
        (0x43b0, 624, 1, 0),
        (0x4478, 560, 1, 0),
        (0x5000, 576, 1, 0),
        (0x5108, 736, 1, 0),
        (0x52c8, 676, 1, 0)
    ];

    public static int LowerY(int area, int progress)
    {
        var position = (area << 12) + progress;
        var index = 0;
        while (index + 1 < s_lower.Length && position > s_lower[index + 1].Start)
            index++;
        var segment = s_lower[index];
        return -(segment.Depth + (position - segment.Start) * segment.Slope / segment.Divisor);
    }
}
