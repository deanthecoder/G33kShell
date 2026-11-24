// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Small rising blue motes, a collection burst, and glints curving towards the HUD.
/// </summary>
internal static class PotionSparkles
{
    public static void DrawIdle(PixelScreenData screen, double x, double y, int age, int seed, byte blue, byte white, byte opacity)
    {
        // Stagger bottles without consuming gameplay randomness or storing an unbounded emitter trail.
        for (var i = 0; i < 4; i++)
        {
            var phase = (age + seed * 7 + i * 15) % 60;
            if (phase > age)
                continue;
            var t = phase / 60.0;
            var drift = Math.Sin(seed + i * 2.4 + t * 3) * 3;
            var alpha = Math.Sin(t * Math.PI) * 0.48;
            Glint(screen, x + (i - 1.5) * 3 + drift, y - 3 - t * 19, alpha * opacity,
                blue, white, phase is >= 23 and <= 29);
        }
    }

    public static void DrawBursts(PixelScreenData screen, Game game, byte blue, byte white, byte opacity)
    {
        foreach (var pickup in game.PotionPickups)
        {
            var age = game.TickCount - pickup.Tick;
            if (age >= 36)
                continue;
            var point = game.ProjectView(pickup.X, pickup.Z, pickup.Y);
            var t = age / 36.0;
            for (var i = 0; i < 10; i++)
            {
                var angle = i * Math.PI * 2 / 10;
                var radius = 2 + 19 * (1 - Math.Pow(1 - t, 2));
                Glint(screen, point.X + Math.Cos(angle) * radius, point.Y - 7 + Math.Sin(angle) * radius * 0.65 - t * 9,
                    opacity * (1 - t) * 0.9, blue, white, i % 3 == 0);
            }
        }
    }

    public static void DrawTrails(PixelScreenData screen, Game game, byte blue, byte white, byte opacity)
    {
        foreach (var pickup in game.PotionPickups)
        {
            // Full-meter pickups still sparkle locally, but do not pretend to add another HUD potion.
            if (pickup.Slot < 0)
                continue;
            var targetX = 109 + Math.Min(pickup.Slot, game.Character.PotionCapacity - 1) * 10;
            for (var i = 2; i >= 0; i--)
            {
                var age = game.TickCount - pickup.Tick - 5 - i * 5;
                if (age < 0 || age >= 48)
                    continue;
                var t = age / 48.0;
                var ease = t * t * (3 - 2 * t);
                var x = pickup.ScreenX + (targetX - pickup.ScreenX) * ease + Math.Sin(t * Math.PI) * 18;
                var y = pickup.ScreenY - 7 + (ArcadeRenderer.MagicMeterTop + 13 - (pickup.ScreenY - 7)) * ease - Math.Sin(t * Math.PI) * 20;
                var alpha = Math.Min(1, age / 5.0) * Math.Min(1, (48 - age) / 10.0);
                Glint(screen, x, y, opacity * alpha * (1 - i * 0.2), blue, white, true);
            }
        }
    }

    private static void Glint(PixelScreenData screen, double x, double y, double opacity, byte blue, byte white, bool cross)
    {
        if (opacity < 1)
            return;
        var px = (int)Math.Round(x);
        var py = (int)Math.Round(y);
        screen.BeginTranslucentSprite((byte)opacity);
        if (cross)
        {
            screen.SetPixel(px - 1, py, blue);
            screen.SetPixel(px + 1, py, blue);
            screen.SetPixel(px, py - 1, blue);
            screen.SetPixel(px, py + 1, blue);
        }
        screen.SetPixel(px, py, cross ? white : blue);
        screen.EndTranslucentSprite();
    }
}
