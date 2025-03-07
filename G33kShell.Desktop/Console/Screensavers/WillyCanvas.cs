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
using CSharp.Core;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;
using SkiaSharp;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A canvas to displaying a Terminator.
/// </summary>
[DebuggerDisplay("WillyCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class WillyCanvas : ScreensaverBase
{
    private const string PngData =
        "iVBORw0KGgoAAAANSUhEUgAAABAAAABAAgMAAABuEx8FAAAACVBMVEUAAAAAAAD///+D3c/SAAAAAXRSTlMAQObYZgAAAGRJREFUGNPFjsENwDAIA608mSJPxJR5Vpmi4hUxZYGGSpmgn5ONYysAA6DlkDtUL0th2yglq2ASmF8QA5xpBG/FHM3CKrZFWsZWbY4Dej5J5N5vH4wbmS+IeZmNq6aXB/Abjf4AkO4tmb6B5FgAAAAASUVORK5CYII=";
    private const int MaxWillies = 5;
    private readonly Random m_random = new Random();
    private readonly Stopwatch m_spawnTimer = Stopwatch.StartNew();
    private readonly List<Willy> m_willys = [];
    private bool[,,] m_frames;
    private int m_frameWidth;
    private int m_frameHeight;
    private double m_spawnTimeSecs;

    public WillyCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
    {
        Name = "willy";
    }

    public override void BuildScreen(ScreenData screen)
    {
        // Load willy image frames.
        using var willyImg = SKBitmap.Decode(Convert.FromBase64String(PngData));
        m_frameHeight = willyImg.Height / 4;
        m_frameWidth = willyImg.Width;
        m_frames = new bool[4, m_frameWidth, m_frameHeight];
        
        for (var frame = 0; frame < 4; frame++)
        {
            for (var y = 0; y < m_frameHeight; y++)
            {
                for (var x = 0; x < m_frameWidth; x++)
                {
                    if (willyImg.GetPixel(x, y + frame * m_frameHeight) == SKColors.White)
                        m_frames[frame, x, y] = true;
                }
            }
        }
    }

    public override void UpdateFrame(ScreenData screen)
    {
        screen.ClearChars();
        screen.ClearColor(Foreground, Background);
        var highResScreen = new HighResScreen(screen);

        // Remove any dead Willys.
        m_willys.RemoveAll(w => w.IsDead);
        
        // Display current Willys.
        foreach (var willy in m_willys)
            willy.Draw(highResScreen, Foreground, screen.Width);
        
        // Spawn more Willys.
        if (m_spawnTimer.Elapsed.TotalSeconds >= m_spawnTimeSecs || m_willys.Count == 0)
        {
            if (m_willys.Count < MaxWillies)
            {
                m_willys.Add(new Willy(m_random, screen, m_frameWidth, m_frameHeight, m_frames));
                m_spawnTimeSecs = m_random.NextDouble() * 4 + 1;
                m_spawnTimer.Restart();
            }
        }
    }

    private class Willy
    {
        private readonly int m_frameWidth;
        private readonly int m_frameHeight;
        private readonly Stopwatch m_stopwatch;
        private readonly bool[,,] m_frames;
        private readonly int m_y;
        private readonly int m_direction;
        private readonly double m_speed;

        public bool IsDead { get; private set; }

        public Willy(Random random, ScreenData screen, int frameWidth, int frameHeight, bool[,,] frames)
        {
            m_direction = random.NextBool() ? 1 : -1;
            m_speed = random.NextDouble() * 6 + 5;
            m_y = random.Next(0, screen.Height * 2 - frameHeight);
            m_frameWidth = frameWidth;
            m_frameHeight = frameHeight;
            m_stopwatch = Stopwatch.StartNew();
            m_frames = frames;
        }

        public void Draw(HighResScreen highResScreen, Rgb foreground, int screenWidth)
        {
            var time = m_stopwatch.Elapsed.TotalSeconds * m_speed;
            var frame = (int)time % 4;
            var willyX = (int)(time / 4) * 8 * m_direction;
            if (m_direction == -1)
                willyX += screenWidth;

            for (var y = 0; y < m_frameHeight; y++)
            {
                for (var x = 0; x < m_frameWidth; x++)
                {
                    if (m_frames[frame, x, y])
                    {
                        var px = m_direction == -1 ? willyX + m_frameWidth - x : willyX + x;
                        highResScreen.Plot(px, m_y + y, foreground);
                    }
                }
            }
            
            // Check if Willy is off the screen and mark as dead.
            if ((m_direction == -1 && willyX + m_frameWidth < 0) || (m_direction == 1 && willyX > screenWidth))
                IsDead = true;
        }
    }
}