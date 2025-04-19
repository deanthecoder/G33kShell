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
using System.Linq;
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
    private readonly List<Particle> m_particles = [];
    private char[,] m_crystal;
    private int m_crystalCount;

    public CrystalCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
    {
        Name = "crystal";
    }
    
    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);

        StartAgain(screen);
    }
    
    public override void UpdateFrame(ScreenData screen)
    {
        while (m_particles.Count < 8)
            m_particles.Add(new Particle(screen));
        
        // Grow a few particles per frame.
        for (var i = 0; i < 10; i++)
        {
            // Move the particle.
            foreach (var particle in m_particles)
                particle.Move(m_crystal);

            // Remove particles that have hit the edge.
            var stuckParticles = m_particles.Where(o => o.IsStuck).ToArray();
            m_crystalCount += stuckParticles.Length;
            foreach (var particle in stuckParticles)
            {
                m_crystal[particle.X, particle.Y] = '.';
                m_particles.Remove(particle);
            }
        }

        if (m_crystalCount >= MaxCrystals)
        {
            StartAgain(screen);
        }
        else
        {
            var height = screen.Height;
            var width = screen.Width;

            // Draw the crystal.
            screen.Clear(Foreground, Background);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (m_crystal[x, y] != 0)
                    {
                        screen.PrintAt(x, y, m_crystal[x, y]);
                        
                        var f = (".•oO".IndexOf(m_crystal[x, y]) / 3.0).Lerp(0.25, 1.0);
                        screen.SetForeground(x, y, f.Lerp(Background, Foreground));
                    }
                }
            }
            
            // Draw the particles.
            foreach (var particle in m_particles)
                screen.PrintAt(particle.X, particle.Y, '•');

            // Update characters.
            for (var y = 1; y < height - 1; y++)
            {
                for (var x = 1; x < width - 1; x++)
                {
                    if (m_crystal[x, y] == 0 || m_crystal[x, y] == 'O')
                        continue;
                    
                    // Count surrounding non-empty characters.
                    var sum = 0.0;
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            if (m_crystal[x + dx, y + dy] != 0)
                                sum++;
                        }
                    }
            
                    sum /= 9.0;
                    m_crystal[x, y] = "..•oO"[(int)sum.Lerp(0.0, 4.9)];
                }
            }
        }
    }

    private void StartAgain(ScreenData screen)
    {
        // Start with a single seed.
        m_crystal = new char[screen.Width, screen.Height];
        m_crystal[screen.Width / 2, screen.Height / 2] = 'O';
        m_crystalCount = 1;
        
        screen.Clear(Foreground, Background);
    }

    private class Particle
    {
        private readonly ScreenData m_screen;
        
        public int X { get; private set; }
        public int Y { get; private set; }
        public bool IsStuck { get; private set; }

        public Particle(ScreenData screen)
        {
            m_screen = screen;
            
            var (x, y) = Spawn();
            X = x;
            Y = y;
        }

        private (int, int) Spawn()
        {
            // Spawn particles near the outer edge for efficiency
            var rand = Random.Shared;
            var edge = rand.Next(4);
            return edge switch
            {
                0 => (rand.Next(m_screen.Width), 0),                   // Top
                1 => (rand.Next(m_screen.Width), m_screen.Height - 1), // Bottom
                2 => (0, rand.Next(m_screen.Height)),                  // Left
                _ => (m_screen.Width - 1, rand.Next(m_screen.Height))  // Right
            };
        }

        private (int dx, int dy) GetPreferredDirection()
        {
            // Random direction.
            var rand = Random.Shared;
            var dx = rand.NextDouble() * 2.0 - 1.0;
            var dy = rand.NextDouble() * 2.0 - 1.0;

            // ...with a very slight bias towards the center.
            dx += (m_screen.Width / 2.0 - X) / (m_screen.Width / 2.0) * 0.15;
            dy += (m_screen.Height / 2.0 - Y) / (m_screen.Height / 2.0) * 0.1;
            
            var idx = (int)Math.Round(dx);
            var idy = (int)Math.Round(dy);

            // Trap to the edges.
            if (X + idx < 0 || X + idx >= m_screen.Width)
                idx = -idx;
            if (Y + idy < 0 || Y + idy >= m_screen.Height)
                idy = -idy;

            return (idx, idy);
        }

        public void Move(char[,] crystal)
        {
            // See if we're about to hit something.
            var dir = GetPreferredDirection();
            IsStuck = crystal[X + dir.dx, Y + dir.dy] != 0;
            if (IsStuck)
                return;
            
            X += dir.dx;
            Y += dir.dy;
        }
    }
}