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
using Avalonia.Platform;
using DTC.Core;
using JetBrains.Annotations;
using SkiaSharp;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// Displays a monitor motherboard on the palette-indexed pixel screen.
/// </summary>
[DebuggerDisplay("MonitorCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class MonitorCanvas : PixelScreensaverBase
{
    private static readonly Uri ImageUri = new("avares://G33kShell/Assets/Screensavers/Monitor.png");
    private static readonly Rgb[] s_basePalette = CreateBasePalette();

    private byte[] m_pixels;
    private int m_imageWidth;
    private int m_imageHeight;

    public MonitorCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 1)
    {
        Name = "monitor";
    }

    public override void OnLoaded(WindowManager windowManager)
    {
        using var imageStream = AssetLoader.Open(ImageUri);
        using var image = SKBitmap.Decode(imageStream);
        m_imageWidth = image.Width;
        m_imageHeight = image.Height;
        m_pixels = new byte[m_imageWidth * m_imageHeight];
        for (var y = 0; y < m_imageHeight; y++)
        {
            for (var x = 0; x < m_imageWidth; x++)
            {
                var colour = image.GetPixel(x, y);
                var luminosity = new Rgb(colour.Red, colour.Green, colour.Blue).Luminosity();
                m_pixels[y * m_imageWidth + x] = (byte)Math.Round(luminosity * byte.MaxValue);
            }
        }

        base.OnLoaded(windowManager);
    }

    public override void StartScreensaver(ScreenData shellScreen)
    {
        base.StartScreensaver(shellScreen);
        StartPixelScreen(m_imageWidth, m_imageHeight);
        if (PixelScreen == null)
            return;

        using (PixelScreen.Lock(out var pixels))
            Array.Copy(m_pixels, pixels.Pixels, m_pixels.Length);
    }

    public override void UpdateFrame(ScreenData screen)
    {
        ClearTextOverlay(screen);
    }

    protected override Rgb[] GetPixelPalette() =>
        PixelScreenData.CreateGreyscalePalette(s_basePalette, Background ?? Rgb.Black, Foreground ?? Rgb.White);

    private static Rgb[] CreateBasePalette()
    {
        var palette = new Rgb[byte.MaxValue + 1];
        for (var i = 0; i < palette.Length; i++)
            palette[i] = new Rgb((byte)i, (byte)i, (byte)i);
        return palette;
    }
}
