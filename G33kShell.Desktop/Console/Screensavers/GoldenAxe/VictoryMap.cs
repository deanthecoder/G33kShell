// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// for any purpose. The software is provided as is, without warranty of any kind.

using System;
using System.Numerics;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>Draws the US set-6 victory parchment, ink and quill onto the arcade surface.</summary>
internal static class VictoryMap
{
    private const int TileSize = ArcadeParchment.TileSize;
    private const int FramesPerTile = ArcadeParchment.FramesPerTile;
    private const int QuillEnters = 94;
    private const int CurveStarts = 122;
    private const int CurveEnds = 209;
    private const int HoldStarts = 240;
    private const int TotalTicks = 474;
    internal const double Duration = TotalTicks / ArcadeParchment.TicksPerSecond;

    // The ROM table at 0x7788 uses map coordinates. Screen columns start two tiles to the right.
    private static readonly (int X, int Y)[] s_inkTiles =
    [
        (4, 6), (5, 6), (6, 6), (7, 6), (4, 7), (5, 7), (4, 8),
        (4, 9), (3, 9), (3, 8), (5, 8), (5, 9), (6, 8), (6, 7),
        (6, 9), (7, 7), (7, 8), (7, 9), (8, 7), (8, 8), (8, 9)
    ];

    // Sprite origins measured from the arcade entrance and exit, where the feather moves off-screen.
    private static readonly (int X, int Y)[] s_entrance =
    [
        (214, -60), (207, -57), (201, -54), (194, -51), (187, -48), (181, -46), (174, -43),
        (167, -41), (161, -39), (154, -37), (147, -35), (141, -34), (134, -32), (127, -31),
        (121, -30), (114, -28), (107, -27), (101, -26), (94, -25), (87, -23), (81, -22),
        (74, -21), (67, -19), (61, -18), (54, -17), (47, -16), (41, -16), (34, -15),
        (28, -15)
    ];
    private static readonly (int X, int Y)[] s_exit =
    [
        (76, 10), (83, 10), (91, 5), (99, 1), (107, -4), (114, -8), (122, -12),
        (130, -16), (138, -19), (145, -23), (153, -26), (161, -28), (169, -31), (176, -34),
        (184, -36), (192, -38), (200, -40), (207, -41), (215, -43), (223, -44), (231, -46),
        (238, -48), (246, -50), (254, -52), (262, -53), (269, -55), (277, -56), (285, -58),
        (293, -59), (300, -60), (308, -61)
    ];

    internal static ArcadeParchment Load(ArcadeData data) =>
        ArcadeParchment.Load(data, "victory-map.png", TotalTicks,
            Array.ConvertAll(s_inkTiles, tile => (tile.X + 2, tile.Y)),
            [new ArcadeParchment.TextLine(61, 48, "CONGRATULATIONS !!")], QuillOrigin, HoldStarts);

    private static (int X, int Y)? QuillOrigin(int tick) => tick switch
    {
        < QuillEnters or >= HoldStarts => null,
        <= CurveStarts => s_entrance[tick - QuillEnters],
        >= CurveEnds => s_exit[tick - CurveEnds],
        _ => CurveOrigin(tick)
    };

    private static (int X, int Y) CurveOrigin(int tick)
    {
        var segment = (tick - CurveStarts) / FramesPerTile;
        var start = CurveStarts + segment * FramesPerTile;
        var t = (tick - start) / (double)(Math.Min(start + FramesPerTile, CurveEnds) - start);
        var p0 = Waypoint(segment);
        var p3 = Waypoint(segment + 1);
        var p1 = p0 + (p3 - Waypoint(Math.Max(0, segment - 1))) / 6;
        var p2 = p3 - (Waypoint(Math.Min(s_inkTiles.Length + 1, segment + 2)) - p0) / 6;
        var s = 1 - t;
        var point = (float)(s * s * s) * p0 + (float)(3 * s * s * t) * p1
                    + (float)(3 * s * t * t) * p2 + (float)(t * t * t) * p3;
        return ((int)Math.Round(point.X) - 3, (int)Math.Round(point.Y) - 61);
    }

    private static Vector2 Waypoint(int index)
    {
        if (index == 0)
            return new Vector2(29, 46);
        if (index > s_inkTiles.Length)
            return new Vector2(76, 71);
        var (x, y) = s_inkTiles[index - 1];
        return new Vector2((x + 2) * TileSize + TileSize / 2, y * TileSize + TileSize / 2);
    }

}
