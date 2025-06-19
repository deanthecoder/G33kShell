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

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A canvas to displaying an animated Mandelbrot.
/// </summary>
[DebuggerDisplay("MandelbrotCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class MandelbrotCanvas : ScreensaverBase
{
    private const int MaxIter = 800;
    private const double Limit = 4.0;

    public MandelbrotCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
    {
        Name = "mandelbrot";
    }

    public override void UpdateFrame(ScreenData screen)
    {
        const double animPeriod = 20.0;
        var t = FrameNumber / (TargetFps * animPeriod);
        t = 0.5 - 0.5 * Math.Cos(t * Math.PI);
        var zoom = 0.2 + Math.Pow(1.01, t * 1200.0) - 1.0;

        var offsetX = -0.743644 - 0.0001 + 0.00011 * t;
        const double offsetY = 0.131825;
        const double densityMultiplier = 1.0 / MaxIter;

        var h = Height;
        var w = Width;
        var halfWidth = w / 2;
        var halfHeight = h / 2;
        var scale = 2.0 / (w * zoom);
        var scaleX = scale * 0.5;

        for (var y = 0; y < h; ++y)
        {
            for (var x = 0; x < w; ++x)
            {
                var cr = (x - halfWidth) * scaleX + offsetX;
                var ci = (y - halfHeight) * scale + offsetY;
                var zr = 0.0;
                var zi = 0.0;
                
                var iteration = 0;
                while (iteration++ < MaxIter)
                {
                    var tr = zr * zr;
                    var ti = zi * zi;
                    if (tr + ti > Limit)
                        break;

                    zi = 2.0 * zr * zi + ci;
                    zr = tr - ti + cr;
                }

                screen.PrintAt(x, y, (iteration * densityMultiplier).ToAscii());
                screen.SetColors(x, y, (iteration * densityMultiplier * 0.2).Lerp(Background, Foreground), (iteration * densityMultiplier).Lerp(Background, Foreground));
            }
        }
    }
}