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
using System.Numerics;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A screensaver that simulates diffusion-limited aggregation (DLA) crystal growth.
/// </summary>
[DebuggerDisplay("CrystalCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class CrystalCanvas : ScreensaverBase
{
    private const int MaxCrystals = 1500;
    private readonly Random m_rand = new Random();
    private int m_crystalCount;

    public CrystalCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
    {
        Name = "crystal";
    }
    
    private (int, int) SpawnParticle(int width, int height)
    {
        // Spawn particles near the outer edge for efficiency
        var edge = m_rand.Next(4);
        return edge switch
        {
            0 => (m_rand.Next(width), 0),          // Top
            1 => (m_rand.Next(width), height - 1), // Bottom
            2 => (0, m_rand.Next(height)),         // Left
            _ => (width - 1, m_rand.Next(height))  // Right
        };
    }
    
    private (int, int) GetPreferredDirection(int x, int y, int width, int height)
    {
        var startingDist = (new Vector2(x, y) - new Vector2(width / 2.0f, height / 2.0f)).LengthSquared();

        while (true)
        {
            var dx = m_rand.Next(3) - 1;
            var dy = m_rand.Next(3) - 1;
            
            if (dx == 0 && dy == 0)
                continue;
            
            var distToCenter = (new Vector2(x + dx, y + dy) - new Vector2(width / 2.0f, height / 2.0f)).LengthSquared();
            if (distToCenter < startingDist)
                return (dx, dy);
        }
    }
    
    private void GrowCrystal(ScreenData screen)
    {
        var (x, y) = SpawnParticle(screen.Width, screen.Height);

        while (true)
        {
            var (dx, dy) = GetPreferredDirection(x, y, screen.Width, screen.Height);
            x += dx;
            y += dy;

            if (x < 0 || y < 0 || x >= screen.Width || y >= screen.Height)
                return; // Particle left the screen, abandon it

            if (screen.Chars[y][x].Ch != ' ')
            {
                // Attach it to the last valid position
                if (screen.Chars[y - dy][x - dx].Ch == ' ')
                {
                    screen.PrintAt(x - dx, y - dy, '.');
                    m_crystalCount++;
                }
                return;
            }
        }
    }
    
    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);
        
        StartAgain(screen);
    }
    
    public override void UpdateFrame(ScreenData screen)
    {
        // Grow a few particles per frame.
        for (var i = 0; i < 5; i++)
            GrowCrystal(screen);

        if (m_crystalCount >= MaxCrystals)
        {
            screen.ClearChars();
            StartAgain(screen);
        }
        else
        {
            // Update characters.
            for (var y = 1; y < screen.Height - 1; y++)
            {
                for (var x = 1; x < screen.Width - 1; x++)
                {
                    if (screen.Chars[y][x].Ch == ' ' || screen.Chars[y][x].Ch == 'O')
                        continue;
                    
                    // Count surrounding non-empty characters.
                    var sum = 0.0;
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            if (screen.Chars[y + dy][x + dx].Ch != ' ')
                                sum++;
                        }
                    }

                    sum /= 9.0;
                    screen.PrintAt(x, y, ".•oO"[(int)sum.Lerp(0, 3.9)]);
                }
            }
        }
    }

    private void StartAgain(ScreenData screen)
    {
        m_crystalCount = 0;

        // Start with a single seed.
        screen.ClearChars();
        InitializeSeeds(screen);
    }

    private void InitializeSeeds(ScreenData screen)
    {
        var numSeeds = 3; // Adjust as needed
        for (var i = 0; i < numSeeds; i++)
        {
            var x = m_rand.Next(screen.Width);
            var y = m_rand.Next(screen.Height);
            screen.PrintAt(x, y, '.');
        }
        
        screen.PrintAt(screen.Width / 2, screen.Height / 2, '.');
    }
}