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
using DTC.Core;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A canvas to displaying an animated Matrix effect.
/// </summary>
[DebuggerDisplay("WormsCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class WormsCanvas : ScreensaverBase
{
    private enum Layer
    {
        Lowest = 10,
        Middle = 20,
        Top = 33
    }

    private const int MaxIlluminatedCount = 40;

    private readonly Dictionary<int, (int dx, int dy)> m_directions = new()
    {
        { 0, (0, -1) }, // Up
        { 1, (1, 0) },  // Right
        { 2, (0, 1) },  // Down
        { 3, (-1, 0) }  // Left
    };
    private readonly IEnumerable<(Layer Layer, int WormCount)> m_wormLayerDefinitions =
    [
        (Layer.Lowest, 120), (Layer.Middle, 120), (Layer.Top, 25)
    ];
    private readonly List<Worm> m_worms = [];

    public WormsCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
    {
        Name = "worms";
    }

    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);

        // Define layers and worm counts to process in one loop.
        foreach (var (layer, wormCount) in m_wormLayerDefinitions)
            m_worms.AddRange(CreateWorms(wormCount, layer));
    }

    private List<Worm> CreateWorms(int wormCount, Layer layer)
    {
        var worms = new List<Worm>();
        var cells = new CellManager(Width, Height);
        var failedStartPoints = new List<(int X, int Y)?>();
        while (worms.Count < wormCount)
        {
            // Spawn a new worm.
            (int X, int Y)? startPos;
            while (true)
            {
                startPos = GetStartPoint(cells, failedStartPoints);
                if (startPos == null)
                {
                    // No valid start points available.
                    return worms;
                }

                if (!failedStartPoints.Contains(startPos))
                    break;
            }
            
            var worm = new Worm(startPos.Value.X, startPos.Value.Y, Random.Shared, layer);
            worms.Add(worm);

            // Grow it.
            while (GrowWorm(worm, cells))
                cells.Set(worm.X, worm.Y);
            
            // If the worm is too small, try again with a new one.
            if (worm.Length < Worm.MinLength)
            {
                worms.Remove(worms.Last());
                failedStartPoints.Add(startPos);
            }
        }

        return worms;
    }

    private bool GrowWorm(Worm worm, CellManager cells)
    {
        if (worm.IsDead)
            return false;

        if (GrowWormSingleAttempt(worm, cells))
            return true;
        
        // Couldn't grow that way.
        if (worm.Length == 1)
        {
            // Too short to make any smaller - Kill it.
            return false;
        }
        
        // Try moving in a different direction.
        var wormDir = worm.Dir;
        worm.PopSegment();

        for (var attempts = 0; attempts < 4; attempts++)
        {
            var didGrow = GrowWormSingleAttempt(worm, cells);
            if (didGrow && worm.Dir != wormDir)
                return true;
            worm.PopSegment();
        }

        return false;
    }

    private bool GrowWormSingleAttempt(Worm worm, CellManager cells)
    {
        var (dx, dy) = m_directions[worm.Dir];
        var nextX = (worm.X + dx) % Width;
        var nextY = (worm.Y + dy) % Height;
        while (nextX < 0) nextX += Width;
        while (nextY < 0) nextY += Height;
        
        var canAdvance = !HasSurroundingCell(cells, worm.Dir, nextX, nextY);
        if (!canAdvance)
            return false;
        worm.Append(nextX, nextY);
        
        // Bend?
        if (worm.ShouldBend())
            worm.Bend();
            
        return true;
    }

    public override void UpdateFrame(ScreenData screen)
    {
        // Light 'em up.
        var illuminatedCount = m_worms.Count(o => o.IsIlluminated);
        if (illuminatedCount < MaxIlluminatedCount)
        {
            var nonIlluminatedWorms = m_worms.Where(o => !o.IsIlluminated).ToArray();
            if (nonIlluminatedWorms.Length > 0)
            {
                var toIlluminate = nonIlluminatedWorms[Random.Shared.Next(nonIlluminatedWorms.Length)];
                toIlluminate.Illuminate();
            }
        }
        
        // Draw the worms.
        m_worms.ForEach(o => o.Draw(screen, Foreground, Background));
    }

    private (int X, int Y)? GetStartPoint(CellManager cells, List<(int X, int Y)?> failedStartPoints)
    {
        for (var i = 0; i < 150; i++)
        {
            var random = Random.Shared;
            var x = random.Next(0, Width);
            var y = random.Next(0, Height);
            if (!failedStartPoints.Contains((x, y)) && IsValidStartPoint(cells, x, y))
                return (x, y);
        }
        
        // Brute force search.
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (!failedStartPoints.Contains((x, y)) && IsValidStartPoint(cells, x, y))
                    return (x, y);
            }
        }
        
        // No start points found.
        return null;
    }

    private static bool IsValidStartPoint(CellManager cells, int x, int y) =>
        !HasSurroundingCell(cells, 0, x, y) && !HasSurroundingCell(cells, 2, x, y);

    private static bool HasSurroundingCell(CellManager cells, int dir, int x, int y)
    {
        var checks = new Dictionary<int, (int dx, int dy)[]>
        {
            [0] = [(0, -1), (-1, -1), (1, -1)], // Only check directly above
            [1] = [(1, 0), (1, -1), (1, 1)],  // Only check directly right
            [2] = [(0, 1), (-1, 1), (1, 1)],  // Only check directly below
            [3] = [(-1, 0), (-1, -1), (-1, 1)]  // Only check directly left
        };

        return checks[dir].Any(o => cells.Get(x + o.dx, y + o.dy));
    }

    private class CellManager
    {
        private readonly bool[,] m_cells;
        private readonly int m_width;
        private readonly int m_height;

        public CellManager(int width, int height)
        {
            m_cells = new bool[width, height];
            m_width = width;
            m_height = height;
        }

        public bool Get(int x, int y) =>
            m_cells[Wrap(x, m_width), Wrap(y, m_height)];

        public void Set(int x, int y) =>
            m_cells[Wrap(x, m_width), Wrap(y, m_height)] = true;

        private static int Wrap(int coordinate, int size) =>
            (coordinate % size + size) % size;
    }

    [DebuggerDisplay("Worm length: {Length}")]
    private class Worm
    {
        private readonly Random m_random;
        private readonly Layer m_layer;
        private readonly List<(int Dir, int X, int Y)> m_positions = [];
        private readonly int m_maxLength;
        private char[] m_segments;
        private Stopwatch m_stopWatch;
        private int m_illuminationDurationMs;

        public const int MinLength = 6;
        private const int MaxLength = 40;
        
        public int Dir => m_positions.Last().Dir;
        public bool IsDead => m_positions.Count >= m_maxLength;

        public int X => m_positions[Length - 1].X;
        public int Y => m_positions[Length - 1].Y;
        public int Length => m_positions.Count;
        public bool IsIlluminated => IlluminationProgress > 0.0 && IlluminationProgress <= 1.0;
        
        private double IlluminationProgress => (m_stopWatch?.ElapsedMilliseconds ?? 0.0) / m_illuminationDurationMs;

        public Worm(int x, int y, Random random, Layer layer)
        {
            m_random = random;
            m_layer = layer;
            m_positions.Add((m_random.Next(0, 4), x, y));
            m_maxLength = m_random.Next(MinLength, MaxLength);
        }

        public void Append(int nextX, int nextY) =>
            m_positions.Add((Dir, nextX, nextY));

        public bool ShouldBend() =>
            m_random.Next(100) > 50;

        public void Bend()
        {
            var nextDir = Dir + (m_random.NextBool() ? 1 : -1);
            while (nextDir < 0) nextDir += 4;
            nextDir %= 4;
            m_positions[^1] = (nextDir, m_positions[^1].X, m_positions[^1].Y);
        }

        public void Draw(ScreenData screen, Rgb foreground, Rgb background)
        {
            if (m_segments == null)
            {
                var dirs = m_positions.Select(o => o.Dir).ToArray();
                m_segments = new char[Length];
                m_segments[0] = "│─│─"[dirs[0]];
                for (var i = 1; i < dirs.Length; i++)
                {
                    var from = dirs[i - 1];
                    var to = dirs[i];
                    m_segments[i] = from switch
                    {
                        0 => to switch
                        {
                            0 => '│',
                            1 => '┌',
                            3 => '┐',
                            _ => m_segments[i]
                        },
                        1 => to switch
                        {
                            1 => '─',
                            0 => '┘',
                            2 => '┐',
                            _ => m_segments[i]
                        },
                        2 => to switch
                        {
                            2 => '│',
                            1 => '└',
                            3 => '┘',
                            _ => m_segments[i]
                        },
                        3 => to switch
                        {
                            3 => '─',
                            2 => '┌',
                            0 => '└',
                            _ => m_segments[i]
                        },
                        _ => m_segments[i]
                    };
                }
            }

            var j = 0;
            for (var i = 0; i < m_positions.Count; i++)
            {
                var segmentFactor = i / (m_positions.Count - 1.0);
                var (_, x, y) = m_positions[i];
                screen.PrintAt(x, y, m_segments[j++]);
                screen.SetForeground(x, y, GetLayerBrightness(foreground, background, m_layer, segmentFactor));
            }
        }

        private Rgb GetLayerBrightness(Rgb foreground, Rgb background, Layer layer, double segmentFactor)
        {
            var minBrightness = (int)layer / 100.0;
            if (!IsIlluminated)
                return minBrightness.Lerp(background, foreground);

            var maxBrightness = minBrightness * 3.0;

            var glowProgress = IlluminationProgress * 3.0 - 1.0;                                                               // Ranges from 0.0 to 2.0
            var distanceFromGlow = Math.Pow(Math.Abs(segmentFactor - glowProgress), segmentFactor < glowProgress ? 1.0 : 5.0); // Distance from glow point
            var boost = (1.0 - distanceFromGlow).Clamp(0.0, 1.0);                                                              // Brightest when distance is 0, fades out otherwise
            var brightness = boost.Lerp(minBrightness, maxBrightness);
            return brightness.Lerp(background, foreground);
        }

        public void PopSegment() =>
            m_positions.RemoveAt(m_positions.Count - 1);

        public void Illuminate()
        {
            m_stopWatch ??= Stopwatch.StartNew();
            m_stopWatch.Restart();
            m_illuminationDurationMs = m_random.Next(1000, 5000);
        }
    }
}