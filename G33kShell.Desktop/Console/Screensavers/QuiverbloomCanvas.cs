// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
// purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using DTC.Core;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// Animated parametric creature that blooms and quivers in monochrome.
/// </summary>
/// <remarks>
/// Inspired by Vitaliy Kaurov's Wolfram Community post and the equations shared
/// there by Daniel Sanchez: https://community.wolfram.com/groups/-/m/t/3516580
/// </remarks>
[UsedImplicitly]
public class QuiverbloomCanvas : PixelScreensaverBase
{
    private const int PixelWidth = 320;
    private const int PixelHeight = 240;
    private const int PointCount = 10_000;
    private const double SourceMinX = 70.0;
    private const double SourceMinY = 30.0;
    private const double SourceWidth = 260.0;
    private const double SourceHeight = 320.0;
    private const double AnimationSpeed = 0.2;
    private const double HorizontalDisplacement = 97.5;
    private const double HeadLift = 124.0;
    private const byte LineShade = 7;

    private static readonly Rgb[] s_basePalette =
    [
        new Rgb(0, 0, 0),
        new Rgb(30, 30, 30),
        new Rgb(58, 58, 58),
        new Rgb(88, 88, 88),
        new Rgb(122, 122, 122),
        new Rgb(162, 162, 162),
        new Rgb(208, 208, 208),
        new Rgb(255, 255, 255)
    ];

    public QuiverbloomCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
    {
        Name = "quiverbloom";
    }

    public override void StartScreensaver(ScreenData shellScreen)
    {
        base.StartScreensaver(shellScreen);
        StartPixelScreen(PixelWidth, PixelHeight);
    }

    public override void UpdateFrame(ScreenData screen)
    {
        ClearTextOverlay(screen);
        if (!IsPixelScreenActive || PixelScreen == null)
            return;

        using (PixelScreen.Lock(out var pixels))
        {
            pixels.Clear();
            DrawBloom(pixels, Time * AnimationSpeed);
        }
    }

    protected override Rgb[] GetPixelPalette()
    {
        var foreground = Foreground ?? Rgb.White;
        var background = Background ?? Rgb.Black;
        return PixelScreenData.CreateGreyscalePalette(s_basePalette, background, foreground);
    }

    private static void DrawBloom(PixelScreenData screen, double time)
    {
        var scale = Math.Min(screen.Width / SourceWidth, screen.Height / SourceHeight);
        var offsetX = (screen.Width - SourceWidth * scale) * 0.5;
        var offsetY = (screen.Height - SourceHeight * scale) * 0.5;
        var previous = CalculatePoint(PointCount - 1, time, scale, offsetX, offsetY);

        for (var i = PointCount - 2; i >= 0; i--)
        {
            var current = CalculatePoint(i, time, scale, offsetX, offsetY);
            screen.DrawAntialiasedLine(previous.X, PixelHeight - previous.Y, current.X, PixelHeight - current.Y, LineShade);
            previous = current;
        }
    }

    private static Point CalculatePoint(int index, double time, double scale, double offsetX, double offsetY)
    {
        var x = (double)index;
        var y = x / 235.0;
        var k = (4.0 + Math.Sin(x / 11.0 + 8.0 * time)) * Math.Cos(x / 14.0);
        var e = y / 8.0 - 19.0;
        var d = Math.Sqrt(k * k + e * e) + Math.Sin(y / 9.0 + 2.0 * time);
        var q = 2.0 * Math.Sin(2.0 * k) + Math.Sin(y / 17.0) * k * (9.0 + 2.0 * Math.Sin(y - 3.0 * d));
        var c = d * d / 49.0 - time;
        var headWeight = index / (double)(PointCount - 1);
        var sourceX = q + HorizontalDisplacement * Math.Cos(c) + 200.0;
        var sourceY = 840.0 - q * Math.Sin(c) - d * 39.0 - HeadLift * headWeight;
        return new Point(
            offsetX + (sourceX - SourceMinX) * scale,
            offsetY + (sourceY - SourceMinY) * scale * 1.5);
    }

    private readonly record struct Point(double X, double Y);
}
