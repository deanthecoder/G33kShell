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
[DebuggerDisplay("EarthCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class EarthCanvas : ScreensaverBase
{
    private const string PngData =
        "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";
    private int m_imgWidth;
    private int m_imgHeight;
    private bool[,] m_imgData;

    public EarthCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
    {
        Name = "earth";
    }

    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);
        
        using var earthImg = SKBitmap.Decode(Convert.FromBase64String(PngData));
        m_imgHeight = earthImg.Height;
        m_imgWidth = earthImg.Width;
        m_imgData = new bool[m_imgWidth, m_imgHeight];
        
        for (var y = 0; y < m_imgHeight; y++)
        {
            for (var x = 0; x < m_imgWidth; x++)
                m_imgData[x, y] = earthImg.GetPixel(x, y).Red < 127;
        }
    }

    public override void UpdateFrame(ScreenData screen)
    {
        var turn = (float)Time * 0.25f; // Adjust speed of rotation.
        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Width; x++)
            {
                // Normalized coordinates (centered at 0)
                var cx = ((float)x / screen.Width - 0.5f) * screen.Width / screen.Height;
                var cy = ((float)y / screen.Height - 0.5f) * 2.0f;
                var r = cx * cx + cy * cy;

                if (r > 1.0f)
                    continue; // Outside sphere.

                // Convert to spherical coordinates.
                var lat = MathF.Asin(cy);                               // Latitude
                var lon = MathF.Atan2(cx, MathF.Sqrt(1.0f - r)) + turn; // Longitude with rotation

                // Convert to UV coordinates.
                var uvx = (int)(GetUvX(lon) * m_imgWidth) % m_imgWidth;
                var uvy = (int)(GetUvY(lat) * m_imgHeight) % m_imgHeight;

                // Get character from texture.
                var f = Sample(uvx, uvy);
                
                // Calculate light contribution from the sun.
                var sun = ((cx - cy) * 2.0 + 1.0).Clamp(0.0, 1.0);
                
                var ch = ((f + sun) / 2.0).Lerp(0.1, 1.0).ToAscii();
                screen.PrintAt(x, y, new Attr(ch, sun.Lerp(0.5, 1.0).Lerp(Background, Foreground)));
            }
        }
    }

    private double Sample(int uvx, int uvy)
    {
        var sum = 0.0;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var x = (uvx + dx + m_imgWidth) % m_imgWidth;
                var y = (uvy + m_imgHeight) % m_imgHeight;
                sum += m_imgData[x, y] ? 1.0 : 0.0;
            }
        }
        return sum / 9.0;
    }

    private static float GetUvX(float lon) => (lon / MathF.PI + 1.0f) * 0.5f;
    private static float GetUvY(float lat) => (lat / (MathF.PI / 2.0f) + 1.0f) * 0.5f;
}