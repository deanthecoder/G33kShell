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
    private const int MaxCrystals = 1000;
    private bool[,] m_grid;
    private readonly Random m_rand = new();
    private readonly int m_screenWidth;
    private readonly int m_screenHeight;
    private int m_crystalCount;

    public CrystalCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
    {
        Name = "crystal";
        m_screenWidth = screenWidth;
        m_screenHeight = screenHeight;
    }
    
    private (int, int) SpawnParticle()
    {
        // Spawn particles near the outer edge for efficiency
        var edge = m_rand.Next(4);
        return edge switch
        {
            0 => (m_rand.Next(m_screenWidth), 0), // Top
            1 => (m_rand.Next(m_screenWidth), m_screenHeight - 1), // Bottom
            2 => (0, m_rand.Next(m_screenHeight)), // Left
            _ => (m_screenWidth - 1, m_rand.Next(m_screenHeight)) // Right
        };
    }
    
    private (int, int) GetPreferredDirection(int x, int y)
    {
        var startingDist = (new Vector2(x, y) - new Vector2(m_screenWidth / 2.0f, m_screenHeight / 2.0f)).LengthSquared();

        while (true)
        {
            var dx = m_rand.Next(3) - 1;
            var dy = m_rand.Next(3) - 1;
            
            if (dx == 0 && dy == 0)
                continue;
            
            var distToCenter = (new Vector2(x + dx, y + dy) - new Vector2(m_screenWidth / 2.0f, m_screenHeight / 2.0f)).LengthSquared();
            if (distToCenter < startingDist)
                return (dx, dy);
        }
    }
    
    private void GrowCrystal(ScreenData screen)
    {
        var (x, y) = SpawnParticle();

        while (true)
        {
            var (dx, dy) = GetPreferredDirection(x, y);
            x += dx;
            y += dy;

            if (x < 0 || y < 0 || x >= m_screenWidth || y >= m_screenHeight)
                return; // Particle left the screen, abandon it

            if (m_grid[x, y])
            {
                m_grid[x - dx, y - dy] = true; // Attach it to the last valid position
                screen.PrintAt(x - dx, y - dy, 'O');
                m_crystalCount++;
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
    }

    private void StartAgain(ScreenData screen)
    {
        m_crystalCount = 0;

        // Start with a single seed.
        m_grid = new bool[m_screenWidth, m_screenHeight];
        InitializeSeeds(screen);
    }

    private void InitializeSeeds(ScreenData screen)
    {
        int numSeeds = 3; // Adjust as needed
        for (int i = 0; i < numSeeds; i++)
        {
            int x = m_rand.Next(screen.Width);
            int y = m_rand.Next(screen.Height);
            m_grid[x, y] = true;
            screen.PrintAt(x, y, 'O');
        }
        
        m_grid[screen.Width / 2, screen.Height / 2] = true;
        screen.PrintAt(screen.Width / 2, screen.Height / 2, 'O');
    }
}