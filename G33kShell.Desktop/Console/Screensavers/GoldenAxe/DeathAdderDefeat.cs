// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// for any purpose. The software is provided as is, without warranty of any kind.

using System;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>Draws Death Adder's airborne axe and the spray at the point of impact.</summary>
internal static class DeathAdderDefeat
{
    internal const int FallenPose = 0x36058;
    internal const int EmbeddedPose = 0x36070;
    internal const int ImpactTick = 74;
    internal const int Duration = ImpactTick + 210;

    internal static void Draw(PixelScreenData screen, ArcadeData data, ArcadeRenderer renderer,
        Fighter boss, (double X, double Y) feet, bool flip, byte[] bloodColors)
    {
        var tick = boss.StateTicks;
        var direction = flip ? -1 : 1;
        if (tick < ImpactTick)
        {
            var spin = data.Animations[0x36080];
            var pose = spin[tick / spin[0].Duration % spin.Length];
            var frame = data.Frames[pose.Frame];
            var phase = tick / (double)ImpactTick;
            var centerX = feet.X + direction * (-27 + 28 * phase);
            var centerY = feet.Y - 8 - 21 * phase - 4 * 138 * phase * (1 - phase);
            var axeFlip = flip ^ ((pose.Flags & 1) != 0);
            renderer.Sprite(screen, pose.Frame, boss.Palette,
                centerX - (axeFlip ? -1 : 1) * (frame.Ox + frame.Width * 0.5),
                centerY - frame.Oy - frame.Height * 0.5, axeFlip, flipY: (pose.Flags & 2) == 0);
            return;
        }

        DrawBlood(screen, feet, flip, tick, bloodColors, landedOnly: false);
    }

    internal static void DrawBlood(PixelScreenData screen, (double X, double Y) feet,
        bool flip, int tick, byte[] bloodColors, bool landedOnly)
    {
        var direction = flip ? -1 : 1;
        // The initial jet breaks into two weaker pulses. Heavy clots land close to the wound.
        for (var i = 0; i < 144; i++)
        {
            var pulse = i / 48;
            var age = tick - ImpactTick - pulse * 37 - i % 48 / 2;
            if (age < 0)
                continue;
            var clot = i % 4 == 0;
            var force = 1 - pulse * 0.19;
            var vx = (i * 17 % 43 - 21) * (clot ? 0.046 : 0.095) * force;
            var speed = (2.3 + i * 11 % 31 * 0.12) * force;
            var gravity = clot ? 0.15 : 0.10;
            var floor = 8 + i * 7 % 15;
            var landTick = (int)Math.Ceiling((speed + Math.Sqrt(speed * speed + 4 * gravity * floor)) / (2 * gravity));
            var landed = age >= landTick;
            if (landed != landedOnly)
                continue;
            var travel = Math.Min(age, landTick);
            var x = (int)Math.Round(feet.X + direction * (16 + travel * vx));
            var y = (int)Math.Round(feet.Y - 1 + (landed ? floor : travel * (gravity * travel - speed)));
            var shade = landed ? 0 : clot ? 1 : 2 + i % 2;
            var color = bloodColors[shade];
            if (landed)
                screen.BeginTranslucentSprite(200);
            screen.SetPixel(x, y, color);
            if (clot || landed)
                screen.SetPixel(x + 1, y, color);
            if (!landed)
                screen.SetPixel(x, y + 1, color);
            if (clot && !landed)
                screen.SetPixel(x + 1, y + 1, bloodColors[0]);
            if (landed)
                screen.EndTranslucentSprite();
        }
    }
}
