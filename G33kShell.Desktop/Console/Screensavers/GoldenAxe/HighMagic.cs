// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Collections.Generic;
using System.Linq;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Reconstructs the missing Ax and Gilius casts from the US set-6 sprite families and effect windows.
/// </summary>
internal static class HighMagic
{
    public static int[][][][] Ax(int[][][][] recordings)
    {
        var five = new int[275][][];
        var six = new int[276][][];
        for (var tick = 0; tick < six.Length; tick++)
        {
            if (tick < 48)
            {
                five[tick] = Jars(recordings[3][tick], 5);
                six[tick] = Jars(recordings[3][tick], 6);
                continue;
            }
            if (tick < five.Length)
            {
                // The same burst sites now use exclusively the large rock/smoke family.
                five[tick] = recordings[3][tick].Select(s =>
                    new[] { s[0] < 383 ? s[0] + 5 : s[0], 0x3b, s[2], 160 + s[3], 0, 186 + s[4] * 3 / 4, 1 }).ToArray();
            }
            six[tick] = Eruption(tick - 48);
        }
        return [.. recordings, five, six];
    }

    private static int[][] Eruption(int age)
    {
        var sprites = new List<int[]>();
        if (age < 168)
        {
            // The cap, stem and ground flare are each drawn from mirrored half sprites.
            var large = age is >= 30 and < 145;
            var phase = age / 4 % 3;
            var height = Math.Min(38, Math.Min(age, 168 - age));
            Pair((large ? 595 : 210) + phase, 0x3e, large ? 39 : 19, 183 - height);
            Pair((large ? 598 : 213) + phase, 0x3f, large ? 13 : 6, 186);
            Pair((large ? 601 : 216) + phase, 0x40, large ? 51 : 25, 191);
        }
        // Stagger the falling rocks. The final large boulder outlasts the eruption.
        for (var i = 0; i < 17; i++)
        {
            var fall = age - 22 - i * 11;
            if (fall < 0 || fall > 50)
                continue;
            var x = 22 + (i * 103 + 37) % 276;
            sprites.Add([i % 4 == 0 ? 219 : 220, 0x41, i % 2, x, 0, -24 + fall * fall / 9, 1]);
        }
        return sprites.ToArray();

        void Pair(int frame, int palette, int halfWidth, int bottom)
        {
            sprites.Add([frame, palette, 0, 160 - halfWidth, 0, bottom, 1]);
            sprites.Add([frame, palette, 1, 160 + halfWidth, 0, bottom, 1]);
        }
    }

    public static int[][][][] Gilius(int[][][][] recordings)
    {
        var four = new int[197][][];
        for (var tick = 0; tick < four.Length; tick++)
        {
            if (tick < 48)
            {
                four[tick] = Jars(recordings[2][tick], 4);
                continue;
            }
            var sprites = new List<int[]>();
            foreach (var sprite in recordings[0][tick].Where(LightningCage.IsSprite))
            {
                // The centre bolt follows the caster. The four flanking columns strike fixed screen lanes.
                sprites.Add(sprite);
                foreach (var x in new[] { 64, 128, 192, 256 })
                {
                    if (sprite[0] >= 573)
                        sprites.Add([sprite[0], sprite[1], 0, x, 0, 156 + sprite[5] + 80, 1]);
                    else
                        sprites.Add([539 + tick / 3 % 3, sprite[1], 0, x, 0, 166, 1]);
                }
            }
            four[tick] = sprites.ToArray();
        }
        return [.. recordings, four];
    }

    private static int[][] Jars(int[][] source, int count)
    {
        if (source.Length == 0)
            return [];
        var first = source[0];
        var center = (first[3] + source[^1][3]) / 2.0;
        var spacing = (first[3] - source[^1][3]) / (double)(source.Length - 1) * (source.Length + 1) / (count + 1);
        return Enumerable.Range(0, count).Select(i => new[] { first[0], first[1], first[2],
            (int)Math.Round(center + ((count - 1) / 2.0 - i) * spacing), first[4], first[5] }).ToArray();
    }
}
