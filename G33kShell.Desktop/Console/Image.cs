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
using System.IO;
using System.Linq;
using Avalonia.Media;
using CSharp.Core;
using CSharp.Core.Extensions;
using SkiaSharp;

namespace G33kShell.Desktop.Console;

/// <summary>
/// Represents an image passed through an ASCII filter, allowing optional 'fade in' transition effect.
/// </summary>
[DebuggerDisplay("Image:{X},{Y} {Width}x{Height}")]
public class Image : Visual
{
    private double[] m_lums;
    private bool m_startFadeIn;
    private TimeSpan m_fadeInDuration;
    private double m_opacity = 1.0;

    public Image Init(int width, int height, FileInfo imageFile)
    {
        using var bitmap = SKBitmap.Decode(imageFile.FullName);
        return Init(width, height, bitmap);
    }

    public Image Init(int width, int height, SKBitmap bitmap)
    {
        base.Init(width, height);

        var lums = CreateLuminosityValues(width, height, bitmap);
        m_lums = AdjustLuminosity(lums);

        return this;
    }
    
    private static double[] CreateLuminosityValues(int width, int height, SKBitmap bitmap)
    {
        var lums = new double[width * height];
        var blockWidth = (double)bitmap.Width / width;
        var blockHeight = (double)bitmap.Height / height;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var rgb = bitmap.GetPixel((int)(x * blockWidth), (int)(y * blockHeight));
                var lum = 0.21 * rgb.Red + 0.72 * rgb.Green + 0.07 * rgb.Blue;
                lums[y * width + x] = lum / 255.0;
            }
        }
        
        return lums;
    }
    
    private static double[] AdjustLuminosity(double[] lums)
    {
        // Calculate the exponent needed to shift the average luminosity.
        var currentAverage = lums.Average();
        var exponent = Math.Log(0.55) / Math.Log(currentAverage);

        // Apply the non-linear transformation using the calculated exponent.
        return lums.Select(o => Math.Pow(o, exponent).Clamp(0.0, 1.0)).ToArray();
    }

    public override void Render()
    {
        if (m_startFadeIn)
        {
            m_startFadeIn = false;
            _ = new Animation(m_fadeInDuration, f =>
            {
                m_opacity = f;
                InvalidateVisual();
                return true;
            }).StartAsync();
        }
        
        const string gradient = " ,;/░▒▓█";
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var lum = m_lums[y * Width + x] * m_opacity;
                var col = Parent.Foreground;

                if (lum < 0.5)
                {
                    // Half brightness
                    var rgb = Parent.Foreground.GetColor();
                    col = new SolidColorBrush(rgb.SetBrightness(0.5));
                }

                // Determine the character from the gradient using normalizedLum
                var ch = gradient[(int)(lum * (gradient.Length - 1))];
                Screen.PrintAt(x, y, new Attr(ch)
                {
                    Foreground = col
                });
            }
        }

        base.Render();
    }

    public Image EnableFadeIn(TimeSpan fadeInDuration)
    {
        m_fadeInDuration = fadeInDuration;
        m_startFadeIn = true;
        m_opacity = 0.0;
        return this;
    }
}