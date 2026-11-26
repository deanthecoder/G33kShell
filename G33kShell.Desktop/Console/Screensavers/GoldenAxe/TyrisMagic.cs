// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// If you modify the code, please retain this copyright header.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.
using System;
using System.Collections.Generic;
using System.Linq;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Reconstructs Tyris's missing seven-to-nine-potion spells using original artwork.
/// </summary>
/// <remarks>
/// Frame families, palettes and time windows come from the MAME handoff.
/// Motion is reconstructed; the dragon's settled composition matches the supplied still.
/// A seventh sprite value marks screen coordinates, unlike the caster-relative recordings.
/// </remarks>
internal static class TyrisMagic
{
    internal const int MeteorCenterX = 184;
    internal const int MeteorFloorY = 191;

    public static int[][][][] Expand(int[][][][] recordings, SpriteFrame[] frames)
    {
        // Use the recorded orbit in X and depth, anchored above screen centre in every player lane.
        // The recording also climbs in world height; carrying that rise over would lift the skulls offscreen.
        foreach (var tick in recordings[5])
        for (var i = 0; i < tick.Length; i++)
        {
            var sprite = tick[i];
            if (sprite[1] is not (0x24 or 0x35))
                continue;
            var bounds = frames[sprite[0]];
            var x = ArcadeData.Width / 2 + (int)Math.Round((sprite[3] - 4) * 0.67);
            var centerY = ArcadeData.Height / 2 - 18 + 20 * Math.Tanh(sprite[4] / 90.0);
            var y = (int)Math.Round(centerY - bounds.Oy - bounds.Height * 0.5);
            tick[i] = [sprite[0], sprite[1], sprite[2], x, 0, y, 1];
        }
        if (recordings.Length >= 9)
            return recordings;
        return recordings.Concat(Enumerable.Range(7, 3).Select(Build)).ToArray();

        int[][][] Build(int potions)
        {
            // Existing recordings begin two ticks after the magic button in the MAME trace.
            var duration = potions switch { 7 => 170, 8 => 292, _ => 366 };
            var result = new int[duration][][];
            for (var tick = 0; tick < duration; tick++)
            {
                var sprites = new List<int[]>();
                if (tick < 48)
                {
                    var jars = recordings[5][tick].Where(s => s[1] == 0x4a).ToArray();
                    if (jars.Length != 0)
                    {
                        var first = jars[0];
                        var center = (first[3] + jars[^1][3]) / 2.0;
                        var spacing = (first[3] - jars[^1][3]) / 5.0 * 7 / (potions + 1);
                        for (var i = 0; i < potions; i++)
                            sprites.Add([first[0], 0x4a, 0, (int)Math.Round(center + ((potions - 1) / 2.0 - i) * spacing), first[4], first[5]]);
                    }
                }
                else if (potions == 7)
                    FireWall(tick + 2, sprites);
                else if (potions == 8)
                    Meteors(tick + 2, sprites);
                else
                    Dragon(tick + 2, sprites);
                result[tick] = sprites.ToArray();
            }
            return result;
        }

        void Piece(List<int[]> sprites, int frame, int palette, double left, double top, bool flip = false)
        {
            var bounds = frames[frame];
            sprites.Add([frame, palette, flip ? 1 : 0, (int)Math.Round(flip ? left + bounds.Ox + bounds.Width : left - bounds.Ox), 0, (int)Math.Round(top - bounds.Oy), 1]);
        }

        void FireWall(int tick, List<int[]> sprites)
        {
            int[] poses = [660, 659, 661, 662];
            var age = tick - 50;
            var width = frames[poses[0]].Width;
            // The rear flame leads. The nearest flame clears the right edge at tick 171.
            const int depthOffsetX = 8;
            var leadX = 1 - width + age / 121.0 * (ArcadeData.Width + width + 5 * depthOffsetX - 2);
            for (var i = 0; i < 6; i++)
            {
                var phase = (age / 3 + i) % poses.Length;
                var frame = poses[phase];
                Piece(sprites, frame, 0x26, leadX - i * depthOffsetX, 165 + i * 8 - frames[frame].Height);
            }
        }

        void Meteors(int tick, List<int[]> sprites)
        {
            // The rippling core and rocky rim are half-sprites: each needs its mirrored partner.
            // MAME's instance counts also show two simultaneous copies of each core/rim pose.
            var age = tick - 50;
            if (tick <= 258)
            {
                var palette = 0x27 + (tick - 49) % 14;
                var rim = 519 + age / 8 % 3;
                Piece(sprites, 518, palette, MeteorCenterX - frames[518].Width, MeteorFloorY - frames[518].Height);
                Piece(sprites, 518, palette, MeteorCenterX, MeteorFloorY - frames[518].Height, flip: true);
                Piece(sprites, rim, 0x47, MeteorCenterX - frames[rim].Width, MeteorFloorY + 12 - frames[rim].Height);
                Piece(sprites, rim, 0x47, MeteorCenterX, MeteorFloorY + 12 - frames[rim].Height, flip: true);
            }
            // Trajectories are reconstructed; preserve the observed meteor and flame windows.
            for (var i = 0; i < 3; i++)
            {
                var launchAge = age - i * 4;
                if (launchAge < 0 || launchAge >= 215 && i != 0)
                    continue;
                var phase = launchAge % 43;
                var direction = (launchAge / 43 + i) % 2 == 0 ? 1 : -1;
                var x = 146 + direction * phase * 1.4;
                var bottom = 150 - phase * 3.5;
                if (tick >= 53 && tick <= 287)
                {
                    var impact = 675 + (tick - 53) / 2 % 3;
                    Piece(sprites, impact, 0x24, x, bottom - frames[impact].Height);
                }
                if (tick >= 56 && tick <= 293)
                {
                    var flame = 98 + (tick - 56) / 3 % 3;
                    Piece(sprites, flame, 0x35, x + 8, bottom - frames[flame].Height);
                }
            }
        }

        void Dragon(int tick, List<int[]> sprites)
        {
            // Dean recalls a shallow right-to-left sweep, descending then rising out.
            // Keep every head and breath piece on the same path; full MAME motion is still unverified.
            var progress = Math.Clamp((tick - 50) / 317.0, 0, 1);
            var smooth = progress * progress * (3 - 2 * progress);
            var offsetX = 60 - 250 * smooth;
            var offsetY = -135 * Math.Pow(2 * progress - 1, 6);
            void DragonPiece(int frame, int palette, double left, double top) =>
                Piece(sprites, frame, palette, left + offsetX, top + offsetY);
            if (tick <= 367)
                DragonPiece(680, 0x51, 286, 28);
            DrawBreath();
            // The upper jaw and crown occlude the flame at its origin inside the mouth.
            if (tick is >= 56 and <= 361)
                DragonPiece(679, 0x51, 228, 28);
            if (tick is >= 65 and <= 352)
                DragonPiece(678, 0x51, 265, -24);

            void DrawBreath()
            {
                if (tick is < 108 or > 359)
                    return;
                var age = tick < 348 ? tick - 108 : 359 - tick;
                if (age < 4)
                    DragonPiece(681, 0x52, 274, 38);
                else if (age < 8)
                    DragonPiece(682, 0x52, 244, 38);
                else if (age < 12)
                {
                    DragonPiece(683, 0x52, 240, 38);
                    DragonPiece(684, 0x52, 228, 105);
                }
                else
                {
                    var phase = (tick - 120) / 4 % 3;
                    if (phase == 0)
                    {
                        DragonPiece(692, 0x52, 128, 89);
                        DragonPiece(693, 0x52, 206, 78);
                        DragonPiece(694, 0x52, 257, 27);
                    }
                    else if (phase == 1)
                    {
                        DragonPiece(685, 0x52, 128, 80);
                        DragonPiece(686, 0x52, 207, 75);
                        DragonPiece(687, 0x52, 249, 68);
                        DragonPiece(688, 0x52, 268, 38);
                    }
                    else
                    {
                        DragonPiece(689, 0x52, 128, 83);
                        DragonPiece(690, 0x52, 233, 68);
                        DragonPiece(691, 0x52, 268, 38);
                    }
                }
            }
        }
    }
}
