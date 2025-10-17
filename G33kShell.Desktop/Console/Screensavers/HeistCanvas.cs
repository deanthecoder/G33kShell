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

[DebuggerDisplay("HeistCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class HeistCanvas : ScreensaverBase
{
    private const double DriftSpeed = 14.0; // Cells per second.

    private readonly List<TextBlock> m_blocks = [];
    private DriftingBlock m_activeDrift;

    private ScreenData m_staticScreen;
    private bool m_isReassembling;
    private int m_removedBlockCount;

    public HeistCanvas(int width, int height) : base(width, height)
    {
        Name = "heist";
    }

    public override void StartScreensaver(ScreenData shellScreen)
    {
        base.StartScreensaver(shellScreen);
        
        // Clone the content of the current shell screen.
        using (Screen.Lock(out var screen))
            shellScreen.CopyTo(screen, 0, 0);
        
        m_staticScreen = shellScreen.Clone();
        m_blocks.Clear();
        m_activeDrift = null;
        m_removedBlockCount = 0;
        m_isReassembling = false;

        var height = shellScreen.Height;
        var width = shellScreen.Width;
        for (var y = 0; y < height; y++)
        {
            var row = shellScreen.Chars[y];
            var claimed = new bool[width];

            // First pass: capture "word" blocks comprised of alphanumeric/underscore characters.
            var x = 0;
            while (x < width)
            {
                while (x < width && !IsWordChar(row[x].Ch))
                    x++;
                if (x >= width)
                    break;

                var start = x;
                while (x < width && IsWordChar(row[x].Ch))
                {
                    claimed[x] = true;
                    x++;
                }

                AddBlock(y, start, x, row, isWord: true);
            }

            // Second pass: capture remaining visible punctuation/symbol blocks.
            x = 0;
            while (x < width)
            {
                if (claimed[x] || !IsNonWordVisibleChar(row[x].Ch))
                {
                    x++;
                    continue;
                }

                var start = x;
                while (x < width && !claimed[x] && IsNonWordVisibleChar(row[x].Ch))
                    x++;

                AddBlock(y, start, x, row, isWord: false);
            }
        }
    }
    
    public override void UpdateFrame(ScreenData screen)
    {
        if (m_staticScreen == null || m_blocks.Count == 0)
            return;

        if (m_activeDrift == null)
            StartNextDrift();

        // Start from the static snapshot, then overlay active drifts.
        screen.Clear(Foreground, Background);
        m_staticScreen.CopyTo(screen, 0, 0);

        if (m_activeDrift == null)
            return;

        var completed = m_activeDrift.Advance(0.1);
        DrawBlock(screen, m_activeDrift);
        if (!completed)
            return;

        OnDriftCompleted(m_activeDrift);
        m_activeDrift = null;
    }

    private void StartNextDrift()
    {
        List<TextBlock> candidates;
        if (!m_isReassembling)
        {
             candidates = m_blocks.Where(b => !b.IsRemoved && !b.IsMoving).ToList();
            if (candidates.Count == 0)
            {
                m_isReassembling = true;
                return;
            }
        }
        else
        {
            candidates = m_blocks.Where(b => b.IsRemoved && !b.IsMoving).ToList();
            if (candidates.Count == 0)
            {
                // Everything is back in place – restart the heist loop.
                m_isReassembling = false;
                m_removedBlockCount = 0;
                return;
            }
        }
        
        var block = candidates[Random.Shared.Next(candidates.Count)];
        StartDrift(block, m_isReassembling);
    }

    private void StartDrift(TextBlock block, bool returning)
    {
        block.IsMoving = true;

        if (!returning)
        {
            var direction = ChooseDirection(block);
            var start = ((double)block.X, (double)block.Y);
            var end = GetExitPoint(block, direction);

            block.LastDirection = direction;
            block.ExitPosition = end;
            block.HasExitPosition = true;

            m_staticScreen.Clear(block.X, block.Y, block.Length, 1, Foreground, Background);

            m_activeDrift = new DriftingBlock(block, start, end, false, DriftSpeed);
        }
        else
        {
            var direction = block.LastDirection ?? ChooseDirection(block);
            var start = block.HasExitPosition ? block.ExitPosition : GetExitPoint(block, direction);
            m_activeDrift = new DriftingBlock(block, start, (block.X, block.Y), true, DriftSpeed);
        }
    }

    private void OnDriftCompleted(DriftingBlock drift)
    {
        var block = drift.Block;
        block.IsMoving = false;

        if (!drift.IsReturning)
        {
            block.IsRemoved = true;
            m_removedBlockCount++;
            if (m_removedBlockCount >= m_blocks.Count)
                m_isReassembling = true;
        }
        else
        {
            for (var i = 0; i < block.Length; i++)
                m_staticScreen.PrintAt(block.X + i, block.Y, block.Content[i]);
            block.IsRemoved = false;
            block.HasExitPosition = false;
            if (m_removedBlockCount > 0)
                m_removedBlockCount--;
            if (m_removedBlockCount == 0 && m_activeDrift == null)
                m_isReassembling = false;
        }
    }

    private Direction ChooseDirection(TextBlock block)
    {
        var width = Width;
        var height = Height;
        var distances = new[]
        {
            (Direction.Up, block.Y + 1),
            (Direction.Down, height - block.Y),
            (Direction.Left, block.X + block.Length),
            (Direction.Right, width - block.X)
        };

        // Weight selection toward nearer edges for variety.
        var minDistance = distances.Min(d => d.Item2);
        var closeDirections = distances.Where(d => d.Item2 <= minDistance + 5).Select(d => d.Item1).ToList();
        if (closeDirections.Count == 0)
            closeDirections = distances.Select(d => d.Item1).ToList();

        return closeDirections[Random.Shared.Next(closeDirections.Count)];
    }

    private (double X, double Y) GetExitPoint(TextBlock block, Direction direction)
    {
        var width = Width;
        var height = Height;
        var x = (double)block.X;
        var y = (double)block.Y;
        var jitter = Random.Shared.NextDouble() * 2.0 - 1.0;

        return direction switch
        {
            Direction.Up => (Math.Clamp(x + jitter * 4.0, -block.Length - 2, width + 2), -1.5),
            Direction.Down => (Math.Clamp(x + jitter * 4.0, -block.Length - 2, width + 2), height + 1.5),
            Direction.Left => (-block.Length - 1.5, Math.Clamp(y + jitter * 2.0, -2, height + 2)),
            Direction.Right => (width + 1.5, Math.Clamp(y + jitter * 2.0, -2, height + 2)),
            _ => (x, y)
        };
    }

    private static void DrawBlock(ScreenData screen, DriftingBlock drift)
    {
        var baseX = (int)drift.Position.X;
        var baseY = (int)drift.Position.Y;

        for (var i = 0; i < drift.Block.Length; i++)
            screen.PrintAt(baseX + i, baseY, drift.Block.Content[i]);
    }

    private void AddBlock(int y, int start, int end, Attr[] row, bool isWord)
    {
        var length = end - start;
        if (length <= 0)
            return;

        if (!isWord && length > 10)
        {
            for (var segmentStart = start; segmentStart < end; segmentStart += 10)
            {
                var segmentEnd = Math.Min(segmentStart + 10, end);
                CreateBlock(y, segmentStart, segmentEnd, row);
            }

            return;
        }

        CreateBlock(y, start, end, row);
    }

    private void CreateBlock(int y, int start, int end, Attr[] row)
    {
        var length = end - start;
        if (length <= 0)
            return;

        var content = new Attr[length];
        for (var i = 0; i < length; i++)
        {
            content[i] = new Attr();
            content[i].Set(row[start + i]);
        }

        m_blocks.Add(new TextBlock(start, y, content));
    }

    private static bool IsWordChar(char ch) =>
        char.IsLetterOrDigit(ch) || ch == '_';

    private static bool IsNonWordVisibleChar(char ch) =>
        ch > ' ' && !IsWordChar(ch);

    private sealed class TextBlock
    {
        public TextBlock(int x, int y, Attr[] content)
        {
            X = x;
            Y = y;
            Content = content;
        }

        public int X { get; }
        public int Y { get; }
        public Attr[] Content { get; }
        public int Length => Content.Length;
        public bool IsRemoved { get; set; }
        public bool IsMoving { get; set; }
        public Direction? LastDirection { get; set; }
        public (double X, double Y) ExitPosition { get; set; }
        public bool HasExitPosition { get; set; }
    }

    private sealed class DriftingBlock
    {
        private readonly double m_speed;
        private readonly double m_totalDistance;

        public DriftingBlock(TextBlock block, (double X, double Y) start, (double X, double Y) end, bool isReturning, double speed)
        {
            Block = block;
            Start = start;
            End = end;
            IsReturning = isReturning;
            Position = start;
            m_speed = speed;
            m_totalDistance = Math.Max(0.001, Distance(start, end));
        }

        public TextBlock Block { get; }
        public bool IsReturning { get; }
        private (double X, double Y) Start { get; }
        private (double X, double Y) End { get; }
        public (double X, double Y) Position { get; private set; }
        private double Progress { get; set; }

        public bool Advance(double deltaSeconds)
        {
            if (Progress >= 1.0)
                return true;

            var delta = m_speed * deltaSeconds / m_totalDistance;
            Progress = Math.Min(1.0, Progress + delta);
            Position = (Progress.Lerp(Start.X, End.X), Progress.Lerp(Start.Y, End.Y));
            return Progress >= 1.0;
        }

        private static double Distance((double X, double Y) a, (double X, double Y) b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
