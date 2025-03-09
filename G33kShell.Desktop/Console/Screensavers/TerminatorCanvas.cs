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
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;
using SkiaSharp;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A canvas to displaying a Terminator.
/// </summary>
[DebuggerDisplay("TerminatorCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class TerminatorCanvas : ScreensaverBase
{
    private const string PngData = "...";
    private double[,] m_imageLums;

    public TerminatorCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 10)
    {
        Name = "terminator";
    }

    public override void BuildScreen(ScreenData screen)
    {
        // Resize background to match ASCII screen dimension.
        using var original = SKBitmap.Decode(Convert.FromBase64String(PngData));
        using var background = original.Resize(new SKSizeI(screen.Width, screen.Height), SKFilterQuality.Medium);
            
        // Get base luminosity values.
        m_imageLums = new double[screen.Width, screen.Height];
        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Width; x++)
            {
                var luminosity = background.GetPixel(x, y).Red / 255.0;
                m_imageLums[x, y] = luminosity;
            }
        }

        // Draw the image.
        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Width; x++)
                screen.PrintAt(x, y, new Attr(m_imageLums[x, y].ToAscii(), Foreground.WithBrightness(m_imageLums[x, y].Lerp(0.2, 0.8))));
        }
    }

    public override void UpdateFrame(ScreenData screen)
    {
        var isOn = (Time * 200.0).SmoothNoise() > 0.6;

        var minLim = isOn ? 0.65 : 0.2;
        var maxLim = isOn ? 1.0 : 0.8;

        for (var x = 31; x <= 37; x++)
        {
            screen.SetForeground(x, 10, Foreground.WithBrightness(m_imageLums[x, 10].Lerp(minLim, maxLim)));
            screen.SetForeground(x, 11, Foreground.WithBrightness(m_imageLums[x, 11].Lerp(minLim, maxLim)));
            screen.SetForeground(x, 12, Foreground.WithBrightness(m_imageLums[x, 12].Lerp(minLim, maxLim)));
        }

        screen.SetForeground(30, 11, Foreground.WithBrightness(m_imageLums[30, 11].Lerp(minLim, maxLim)));
        screen.SetForeground(38, 11, Foreground.WithBrightness(m_imageLums[37, 11].Lerp(minLim, maxLim)));
    }
}