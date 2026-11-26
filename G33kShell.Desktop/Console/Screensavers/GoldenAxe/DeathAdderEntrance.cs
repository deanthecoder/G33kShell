// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// for any purpose. The software is provided as is, without warranty of any kind.

using System;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>Reconstructs the throne-room transformation with the original snake and helmet artwork.</summary>
internal static class DeathAdderEntrance
{
    internal const int BodyPilePalette = 0x91;
    internal const int SnakePalette = 0x92;
    private const int CrawlTicks = 200;
    private const int RiseTick = CrawlTicks - 12;
    private const int FormTick = RiseTick + 72;
    private const int RevealTick = FormTick + 46;
    internal const int WalkTick = RevealTick + 20;
    internal const int PileFadeTicks = 45;

    internal static double BossOpacity(int tick) => Math.Clamp((tick - RevealTick) / 20.0, 0, 1);

    internal static void Draw(PixelScreenData screen, Game game, Fighter boss, ArcadeRenderer renderer)
    {
        var tick = boss.StateTicks;
        if (tick >= WalkTick)
            return;
        var feet = game.ProjectView(boss.X, boss.Z, boss.Y);
        var frame = 1145;
        if (tick >= FormTick)
            frame = 1164 + Math.Min(5, (tick - FormTick) / 7);
        else if (tick >= RiseTick)
        {
            // Each bank has the same nine heights with a different twist to the coils.
            var age = tick - RiseTick;
            frame += 9 * (age / 4 % 3) - Math.Min(8, age / 8);
        }
        var opacity = 1 - BossOpacity(tick);
        if (opacity < 1)
            screen.BeginTranslucentSprite((byte)Math.Round(255 * opacity));
        renderer.Sprite(screen, frame, boss.Palette, feet.X, feet.Y, false);
        if (opacity < 1)
            screen.EndTranslucentSprite();

        for (var i = 0; i < 8; i++)
        {
            var age = tick - i * 8;
            if (age is < 0 or >= CrawlTicks)
                continue;
            var remaining = 1 - age / (double)CrawlTicks;
            var side = i % 2 == 0 ? -1 : 1;
            // The four floor poses lie at slopes of 0.70–0.85 in projected screen pixels.
            var dx = (118 + i % 3 * 12) * remaining;
            var dy = dx * (0.70 + i % 4 * 0.05);
            renderer.Sprite(screen, 1133 + age / 10 % 4, SnakePalette, feet.X + side * dx, feet.Y + dy, side > 0);
        }
    }
}
