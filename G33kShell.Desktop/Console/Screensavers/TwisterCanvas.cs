// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
//  purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.
using System;
using System.Diagnostics;
using DTC.Core;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A canvas to displaying an animated twister effect.
/// </summary>
[DebuggerDisplay("TwisterCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class TwisterCanvas : ScreensaverBase
{
    private readonly int[,,] m_animation; // Precomputed frames.
    private int m_frame;

    public TwisterCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 15)
    {
        Name = "twister";

        // Precompute twister animation.
        m_animation = new int[120, screenHeight, 6];
        PrecomputeTwister();
    }

    private void PrecomputeTwister()
    {
        for (var i = 0; i < 120; i++)
        {
            var am = 120.0 + Math.Cos(i * Math.PI / 60.0) * 100.0;
            var an = -Math.PI + Math.Sin(i * Math.PI / 60.0) * Math.PI;

            for (var y = 0; y < Screen.Height; y++)
            {
                var fv = 8.0 * y / am + an;
                var x1 = (int)(24.0 * Math.Sin(fv));
                var x2 = (int)(24.0 * Math.Sin(fv + Math.PI / 2.0));
                var x3 = -x1;
                var x4 = -x2;

                m_animation[i, y, 0] = Math.Min(Math.Min(x1, x2), Math.Min(x3, x4));
                m_animation[i, y, 5] = Math.Max(Math.Max(x1, x2), Math.Max(x3, x4));

                if (x1 < x2)
                {
                    m_animation[i, y, 1] = x1;
                    m_animation[i, y, 2] = x2;
                }
                else if (x3 < x4)
                {
                    m_animation[i, y, 1] = x3;
                    m_animation[i, y, 2] = x4;
                }

                if (x2 < x3)
                {
                    m_animation[i, y, 3] = x2;
                    m_animation[i, y, 4] = x3;
                }
                else if (x4 < x1)
                {
                    m_animation[i, y, 3] = x4;
                    m_animation[i, y, 4] = x1;
                }
            }
        }
    }

    public override void UpdateFrame(ScreenData screen)
    {
        // Get the current frame data.
        var frame = m_frame++ % 120;

        var brightRgb = Foreground.WithBrightness(1.1);
        var dimRgb = Foreground.WithBrightness(0.4);
        for (var y = 0; y < screen.Height; y++)
        {
            var row = new int[6];
            for (var j = 0; j < 6; j++)
                row[j] = m_animation[frame, y, j];

            DrawLine(screen, -24, row[0], y, Background, ' ');
            DrawLine(screen, row[1], row[2], y, brightRgb, '=');
            DrawLine(screen, row[3], row[4], y, dimRgb, '-');
            DrawLine(screen, row[5], 24, y, Background, ' ');
        }
    }

    private static void DrawLine(ScreenData screen, int x1, int x2, int y, Rgb color, char ch)
    {
        if (x1 >= x2)
            return;
        var from = x1 + screen.Width / 2;
        var to = x2 + screen.Width / 2;
        for (var x = from; x < to; x++)
        {
            screen.PrintAt(x, y, ch);
            var f = (double)(x - from) / (to - from);
            f = (f * 2.0).Clamp(0.0, 1.0);
            screen.PrintAt(x, y, ch == ' ' ? ' ' : (color.R / 255.0 * f).ToAscii());
        }
    }
}