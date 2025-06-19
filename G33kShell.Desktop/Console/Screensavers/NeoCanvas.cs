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
using System.Collections.Generic;
using System.Diagnostics;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;
using SkiaSharp;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A canvas to displaying an animated Matrix corridor effect.
/// </summary>
[DebuggerDisplay("NeoCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class NeoCanvas : ScreensaverBase
{
    private readonly string m_pngData = "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";
    private double[] m_backLums;
    private double[] m_displayLums;
    private List<(int X, double Y, double dY)> m_trailHeads;

    // Blending and trail parameters
    private const double BlendFactor = 0.85;
    private const double TrailLuminosity = 0.2;
    private const double TrailProbability = 0.02; // Probability of spawning a new trail head each frame
    private const int MaxTrailHeads = 100;        // Maximum number of active trail heads

    public NeoCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 20)
    {
        Name = "neo";
    }

    public override void BuildScreen(ScreenData screen)
    {
        // Resize background to match ASCII screen dimension.
        using var original = SKBitmap.Decode(Convert.FromBase64String(m_pngData));
        using var background = original.Resize(new SKSizeI(screen.Width, screen.Height), SKFilterQuality.Medium);
            
        // Get base luminosity values.
        m_backLums = new double[screen.Width * screen.Height];
        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Width; x++)
            {
                var rgb = background.GetPixel(x, y);
                m_backLums[y * screen.Width + x] = rgb.Red / 255.0;
            }
        }

        // Initialize display luminosity to zero.
        m_displayLums = new double[screen.Width * screen.Height];

        // Initialize trail heads list.
        m_trailHeads = new List<(int X, double Y, double dY)>();

        // Seed screen with invisible characters.
        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Width; x++)
                screen.PrintAt(x, y, new Attr(m_backLums[y * screen.Width + x].ToAscii(), Background));
        }
    }

    public override void UpdateFrame(ScreenData screen)
    {
        // Blend display luminosity with background luminosity
        for (var i = 0; i < m_displayLums.Length; i++)
            m_displayLums[i] = BlendFactor * m_displayLums[i] + (1.0 - BlendFactor) * m_backLums[i] * 0.5;

        // Update trail heads positions
        var newTrailHeads = new List<(int X, double Y, double dY)>();
        foreach (var head in m_trailHeads)
        {
            var newY = head.Y + head.dY;
            if (newY >= screen.Height)
                continue; // The trail head has moved off the screen.
            newTrailHeads.Add((head.X, newY, head.dY));
                
            // Set the luminosity at the new position to the trail luminosity
            var offset = (int)newY * screen.Width + head.X;
            m_displayLums[offset] = TrailLuminosity + m_backLums[offset];
        }
        m_trailHeads = newTrailHeads;

        // Possibly spawn new trail heads.
        for (var x = 0; x < screen.Width && m_trailHeads.Count < MaxTrailHeads; x++)
        {
            var random = Random.Shared;
            if (random.NextDouble() < TrailProbability)
            {
                // Spawn a new trail head at the top row.
                var speed = random.NextDouble();
                m_trailHeads.Add((x, 0.0, 0.3 + 0.7 * speed));
                m_displayLums[x] = speed * TrailLuminosity;
            }
        }

        // Update screen foreground colors based on display luminosity
        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Width; x++)
            {
                var i = y * screen.Width + x;
                screen.Chars[y][x].Background = m_displayLums[i].Clamp(0.0, 1.0).Lerp(Background, Foreground);
                screen.Chars[y][x].Foreground = Math.Max(m_backLums[i] * 0.75, m_displayLums[i]).Lerp(Background, Foreground);
            }
        }
    }
}
