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
using DTC.Core.Extensions;
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
    private const string PngData = "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";
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