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
/// A canvas to displaying marching Miner Willys.
/// </summary>
[DebuggerDisplay("WillyCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class WillyCanvas : ScreensaverBase
{
    private const int MaxWillies = 5;
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
        base.BuildScreen(screen);
        
        LoadWillyFrames(out m_frameWidth, out m_frameHeight, out m_frames);
    }

    public static void LoadWillyFrames(out int frameWidth, out int frameHeight, out bool[,,] frames)
    {
        const string pngData =
            "iVBORw0KGgoAAAANSUhEUgAAABAAAABAAgMAAABuEx8FAAAACVBMVEUAAAAAAAD///+D3c/SAAAAAXRSTlMAQObYZgAAAGRJREFUGNPFjsENwDAIA608mSJPxJR5Vpmi4hUxZYGGSpmgn5ONYysAA6DlkDtUL0th2yglq2ASmF8QA5xpBG/FHM3CKrZFWsZWbY4Dej5J5N5vH4wbmS+IeZmNq6aXB/Abjf4AkO4tmb6B5FgAAAAASUVORK5CYII=";

        // Load willy image frames.
        using var willyImg = SKBitmap.Decode(Convert.FromBase64String(pngData));
        frameHeight = willyImg.Height / 4;
        frameWidth = willyImg.Width;
        frames = new bool[4, frameWidth, frameHeight];
        
        for (var frame = 0; frame < 4; frame++)
        {
            for (var y = 0; y < frameHeight; y++)
            {
                for (var x = 0; x < frameWidth; x++)
                {
                    if (willyImg.GetPixel(x, y + frame * frameHeight) == SKColors.White)
                        frames[frame, x, y] = true;
                }
            }
        }
    }

    public override void UpdateFrame(ScreenData screen)
    {
        screen.Clear(Foreground, Background);
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
                var random = Random.Shared;
                m_willys.Add(new Willy(random, screen, m_frameWidth, m_frameHeight, m_frames));
                m_spawnTimeSecs = random.NextDouble() * 4 + 1;
                m_spawnTimer.Restart();
            }
        }
    }

    public class Willy
    {
        private readonly int m_frameWidth;
        private readonly int m_frameHeight;
        private Stopwatch m_stopwatch;
        private readonly bool[,,] m_frames;
        private readonly int m_y;
        private readonly int m_direction;
        private readonly double m_speed;

        public bool IsDead { get; private set; }

        public Willy(Random random, ScreenData screen, int frameWidth, int frameHeight, bool[,,] frames) :
            this(random.Next(0, screen.Height * 2 - frameHeight),
                random.NextBool() ? 1 : -1,
                random.NextDouble() * 6 + 5,
                frameWidth,
                frameHeight,
                frames)
        {
        }
        
        public Willy(int y, int direction, double speed, int frameWidth, int frameHeight, bool[,,] frames)
        {
            m_y = y;
            m_direction = direction;
            m_speed = speed;
            m_frameWidth = frameWidth;
            m_frameHeight = frameHeight;
            m_frames = frames;
        }

        public void Draw(HighResScreen highResScreen, Rgb foreground, int screenWidth)
        {
            m_stopwatch ??= Stopwatch.StartNew();
            
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
                        if (px < screenWidth)
                            highResScreen.Plot(px, m_y + y, foreground);
                    }
                }
            }
            
            // Check if Willy is off the screen and mark as dead.
            if ((m_direction == -1 && willyX + m_frameWidth < 0) || (m_direction == 1 && willyX > screenWidth))
                IsDead = true;
        }

        public void Reset()
        {
            m_stopwatch?.Restart();
            IsDead = false;
        }
    }
}