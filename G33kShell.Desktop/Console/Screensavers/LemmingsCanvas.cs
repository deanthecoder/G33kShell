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
using DTC.Core;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;
using SkiaSharp;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A canvas to displaying walking lemmings.
/// </summary>
[DebuggerDisplay("LemmingsCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class LemmingsCanvas : ScreensaverBase
{
    private const int MaxSprites = 5;
    private readonly Stopwatch m_spawnTimer = Stopwatch.StartNew();
    private readonly List<Sprite> m_sprites = [];
    private double[,,] m_frames;
    private int m_frameWidth;
    private int m_frameHeight;
    private double m_spawnTimeSecs;

    public LemmingsCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
    {
        Name = "lemmings";
    }

    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);
        
        LoadFrames(out m_frameWidth, out m_frameHeight, out m_frames);
    }

    private static void LoadFrames(out int frameWidth, out int frameHeight, out double[,,] frames)
    {
        const string pngData =
            "iVBORw0KGgoAAAANSUhEUgAAAA4AAACgCAYAAAA8Xl1PAAAAAXNSR0IB2cksfwAAAARnQU1BAACxjwv8YQUAAAAgY0hSTQAAeiYAAICEAAD6AAAAgOgAAHUwAADqYAAAOpgAABdwnLpRPAAABF1JREFUaN7Fma124zAQhcc+9oJ9gGIHhJcXbFBBSIqXxLwgRn2IIheUt2SxQxYUNSA8vMDGfoQEaIEtZTQa/Tnt1ufkpE18M5Y8+nRnnACAgAlHZnzSkP/veGGqCUZR/o7eG4C8cgmRgIpPC1OYAIDAlycFx2U9nHBbuS8VR8OH6Fv280TOqhyHjKSO69Vw4tWMn9VTTSZhFLgOgV/irRbFXKgX/V6+Uph4sML2+QkAAH7dABRzESgMGJs1oozmimoKD9ugqAldHetS//XdfnjvPhJ7RPFWsyLnGGlquUT6ejxsrQJ6mewY6eXaVocpxJc8XoVNrOdhAyKvHP+zudqQddnY1+n5UhsTE/n7sNzss9oQQMXk6mmhRzsua5MGhvBuiMRFo7eHvR15RZiDkp0yR5tVNRGBa5JlzrqMYI6NoX4CjCkWJZTRZvcbhQ2AGFgx3JE/jIeSuk421iYaSorvE14+WOSlnAQViw0ycZlL1H0kw7iYBZ3aAKU4Y6GAylX8yy5c8MJA1rCwumibixM2xBxZ/I3TdeSVnz0ac6ST4gyRM6IUyGiuH8hwRNZZgcdZSdLRKF5nxeIwlHKG+3CgRIso87X8s1FJz22qn5M5NNqnwcoQKnqRifAxJ8MCPCk4KscgNavYUckTqQjPsDFGfKLLJCWib4UNF9ymKr8fhNIxcmKZqwRk38kcypomxugik4SZw5mklCv/8vczAU4LTzXHUe24rK20yyjlqM85ju9JHUIA7HMcnkfzL+tSCNG38bXVy2MXZFvSKYvYrDuYBe1FBxbN7jd+dNAPqLOyo4OQuphv4pN8XQoNFy4CZHKhlg+FOtkGKPNSD1vY7TdR6zHjCy+h1coDLrgxXq80MHUfCbT7TstTuk/+f+ZktiYQRshpYTaEjKKMHpg/mD0ZF4H2c05BBimwtso4QFFsiH71ea5DY05eDbWV6FtvbWUWZX07iN9qsS4jYFU+FGpsQS0LG7CC7crLYxefchhY2CgVc2FYM02YXM2Mfk44Vyf1c9AND+/nOARh/ZzAGsu7P9pWx9f0c4JKJKOf05zLCrddCewd800S0s8RP+swIcuckEqH7eeMljS+nxPqcyQ6pjFnFEcZJLkzT9qtJLDa56dpJRK9LdZ+jg0bKipabhl34m4PMLspAGBjbcukvmgAMPiBGHRc1s+xICQ7J3JHWMNX41pE9WUga767nwMT+zka4ZqIfk6MyPA53N9RZSB1Vl9Hucv7OZhsDnfFVnPRsLoo5WY3E2EVVVtxUdv9StUefN3hEMP1it0/nP0cl2VJcWbs9nZjRMfp7OewmXTYQnJbjWOU2UFqLNG3RhapRtA311ac37kLiIjhpJ4qNT4CNHxH98ffim0GeQlwXNaOp/MQ/6zcpFygxzFrK1RXTaqtoh54XVQiqVLp91NcyuFqziV2OquoHXnSs/KYjq5yHdQ1emurswHaBAmM9fj6knjavwH9HMP3kO8BAP4B6I1KaTKZzKUAAAAASUVORK5CYII=";

        // Load sprite image frames.
        using var img = SKBitmap.Decode(Convert.FromBase64String(pngData));
        const int frameCount = 8;
        frameHeight = img.Height / frameCount;
        frameWidth = img.Width;
        frames = new double[frameCount, frameWidth, frameHeight];
        
        for (var frame = 0; frame < frameCount; frame++)
        {
            for (var y = 0; y < frameHeight; y++)
            {
                for (var x = 0; x < frameWidth; x++)
                {
                    var pixel = img.GetPixel(x, y + frame * frameHeight);
                    var luminosity = new Rgb(pixel.Red, pixel.Green, pixel.Blue).Luminosity();
                    frames[frame, x, y] = Math.Pow(luminosity, 1.5);
                }
            }
        }
    }

    public override void UpdateFrame(ScreenData screen)
    {
        screen.Clear(Foreground, Background);
        var highResScreen = new HighResScreen(screen);

        // Remove any dead sprites.
        m_sprites.RemoveAll(w => w.IsDead);
        
        // Display current sprites.
        foreach (var sprite in m_sprites)
            sprite.Draw(highResScreen, Foreground, Background, screen.Width);
        
        // Spawn more sprites.
        if (m_spawnTimer.Elapsed.TotalSeconds >= m_spawnTimeSecs || m_sprites.Count == 0)
        {
            if (m_sprites.Count < MaxSprites)
            {
                var random = Random.Shared;
                m_sprites.Add(new Sprite(random, screen, m_frameWidth, m_frameHeight, m_frames));
                m_spawnTimeSecs = random.NextDouble() * 4 + 1;
                m_spawnTimer.Restart();
            }
        }
    }

    private class Sprite
    {
        private readonly int m_frameWidth;
        private readonly int m_frameHeight;
        private Stopwatch m_stopwatch;
        private readonly double[,,] m_frames;
        private readonly int m_y;
        private readonly int m_direction;
        private readonly double m_speed;
        private readonly int m_frameCount;

        public bool IsDead { get; private set; }

        public Sprite(Random random, ScreenData screen, int frameWidth, int frameHeight, double[,,] frames) :
            this(random.Next(0, screen.Height * 2 - frameHeight),
                random.NextBool() ? 1 : -1,
                random.NextDouble() * 6 + 5,
                frameWidth,
                frameHeight,
                frames)
        {
        }

        private Sprite(int y, int direction, double speed, int frameWidth, int frameHeight, double[,,] frames)
        {
            m_y = y;
            m_direction = direction;
            m_speed = speed;
            m_frameWidth = frameWidth;
            m_frameHeight = frameHeight;
            m_frames = frames;

            m_frameCount = frames.GetLength(0);
        }

        public void Draw(HighResScreen highResScreen, Rgb foreground, Rgb background, int screenWidth)
        {
            m_stopwatch ??= Stopwatch.StartNew();
            
            var time = m_stopwatch.Elapsed.TotalSeconds * m_speed;
            var frame = (int)time % m_frameCount;
            var spriteX = (int)Math.Round(time / m_frameCount * (m_frameWidth + 2) * m_direction);
            if (m_direction == -1)
                spriteX += screenWidth;

            for (var y = 0; y < m_frameHeight; y++)
            {
                for (var x = 0; x < m_frameWidth; x++)
                {
                    if (m_frames[frame, x, y] < 0.001)
                        continue;
                    
                    var px = m_direction == -1 ? spriteX + m_frameWidth - x : spriteX + x;
                    if (px < screenWidth)
                        highResScreen.Plot(px, m_y + y, m_frames[frame, x, y].Lerp(background, foreground));
                }
            }
            
            // Check if Sprite is off the screen and mark as dead.
            if ((m_direction == -1 && spriteX + m_frameWidth < 0) || (m_direction == 1 && spriteX > screenWidth))
                IsDead = true;
        }
    }
}