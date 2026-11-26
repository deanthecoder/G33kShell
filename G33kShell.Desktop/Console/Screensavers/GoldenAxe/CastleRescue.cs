// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// for any purpose. The software is provided as is, without warranty of any kind.

using System;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>Plays the set-6 castle rescue before the parchment map.</summary>
internal static class CastleRescue
{
    private const int ReleaseTick = 302;
    private const int ReunitedAge = 200;
    internal const int FadeTicks = 45;
    internal const int Duration = ReleaseTick + ReunitedAge + 120 + FadeTicks;

    internal static byte HudOpacity(int tick) =>
        (byte)Math.Round(255 * Math.Clamp(1 - tick / 120.0, 0, 1));

    internal static void Draw(PixelScreenData screen, Game game, ArcadeData data, ArcadeRenderer renderer)
    {
        var tick = game.RescuingCaptives ? game.EndStateTicks : 0;
        var throneCameraX = 4096 - 7 - data.Stops[^1];
        var panX = (int)Math.Round(game.ViewCamera.X - throneCameraX);
        if (panX >= ArcadeData.Width)
            return;
        var panY = (int)Math.Round(game.ViewCamera.Y - data.CameraRange(throneCameraX).High);
        var chain = Math.Clamp((tick - 150) / 2, 0, 76);
        for (var link = chain - 10; link >= -10; link -= 10)
        {
            Pose(1078, 0x8d, 108, link);
            Pose(1078, 0x8d, 206, link);
        }
        Pose(1076, 0x8d, 105, chain);
        Pose(1077, 0x8d, 203, chain);
        var release = tick - ReleaseTick;
        if (release < 0)
        {
            Pose(525, 0x5a, 193, 10 + chain);
            Pose(531, 0x5c, 94, 10 + chain);
            return;
        }

        var right = release switch
        {
            < 12 => 525, < 18 => 526, < 24 => 527, < 180 => 528 + (release - 24) / 8 % 3,
            < 190 => 536, < ReunitedAge => 537, _ => 538
        };
        var rightX = Math.Max(105, 193 - Math.Max(0, release - 22) / 2);
        Pose(right, right == 538 ? 0x5b : 0x5a, rightX, Math.Min(166, 151 + release) - data.Frames[right].Height);

        // The king is on the nearer floor lane, including the settled reunion pose.
        var left = release switch { < 12 => 532, < 24 => 533, < ReunitedAge => 534, _ => 535 };
        var leftX = left switch { 532 => 91, 533 => 86, 534 => 79, _ => 81 };
        Pose(left, 0x5c, leftX, Math.Min(180, 154 + release) - data.Frames[left].Height);

        void Pose(int frame, int palette, int x, int y)
        {
            var bounds = data.Frames[frame];
            renderer.Sprite(screen, frame, palette, x + panX - bounds.Ox, y + panY - bounds.Oy, false);
        }
    }
}
