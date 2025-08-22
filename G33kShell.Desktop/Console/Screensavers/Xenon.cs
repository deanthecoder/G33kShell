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
/// A canvas to displaying the Xenon 2 Shopkeeper.
/// </summary>
[DebuggerDisplay("XenonCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class XenonCanvas : ScreensaverBase
{
    private const string PngData = "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";
    private double[,] m_imageLums;

    public XenonCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 1)
    {
        Name = "xenon";
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
                m_imageLums[x, y] = Math.Pow(luminosity, 1.5);
            }
        }

        // Draw the image.
        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Width; x++)
            {
                var luminosity = m_imageLums[x, y];
                screen.PrintAt(x, y, new Attr(luminosity.ToAscii(), luminosity.Lerp(0.3, 1.0).Lerp(Background, Foreground), luminosity.Lerp(0.0, 0.3).Lerp(Background, Foreground)));
            }
        }
    }

    public override void UpdateFrame(ScreenData screen)
    {
    }
}