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
using DTC.Core;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

public class Snake
{
    private readonly int m_arenaWidth;
    private readonly int m_arenaHeight;
    private readonly CircularBuffer<IntPoint> m_segments;
    private readonly Dictionary<IntPoint, int> m_segmentCache = [];

    public Direction Direction { get; private set; } = Direction.Left;
    public IntPoint HeadPosition { get; private set; }
    public IEnumerable<IntPoint> Segments => m_segments;
    public int Length { get; private set; } = 5;
    public int StepsSinceFood { get; private set; }
    public bool IsDead { get; private set; }
    public int TotalStepsToStarvation => m_arenaWidth + Length * 10;
    public int MaxLength { get; }

    public Snake(int arenaWidth, int arenaHeight)
    {
        m_arenaWidth = arenaWidth;
        m_arenaHeight = arenaHeight;
        MaxLength = (int)(m_arenaWidth * arenaHeight * 0.9);
        m_segments = new CircularBuffer<IntPoint>(MaxLength + 1);

        Reset();
    }

    public void Reset()
    {
        m_segments.Clear();
        m_segmentCache.Clear();
        Direction = Direction.Left;
        HeadPosition = new IntPoint(m_arenaWidth / 2, m_arenaHeight / 2);
        Length = 5;
        StepsSinceFood = 0;
        IsDead = false;
    }

    public bool Move(Direction direction, IntPoint foodPosition)
    {
        direction = NormalizeDirection(direction);
        Direction = direction;
        var oldHeadPosition = HeadPosition;
        HeadPosition = direction switch
        {
            Direction.Left => HeadPosition.WithDelta(-1, 0),
            Direction.Right => HeadPosition.WithDelta(1, 0),
            Direction.Up => HeadPosition.WithDelta(0, -1),
            Direction.Down => HeadPosition.WithDelta(0, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };

        if (IsCollision(HeadPosition))
        {
            IsDead = true;
            return false;
        }

        StepsSinceFood++;
        if (StepsSinceFood >= TotalStepsToStarvation)
        {
            IsDead = true;
            return false;
        }

        var foodEaten = false;
        if (HeadPosition == foodPosition)
        {
            foodEaten = true;
            StepsSinceFood = 0;
            Length = Math.Min(Length + 3, MaxLength);
        }
        
        // Extend snake.
        m_segments.Write(oldHeadPosition);
        AddSegment(oldHeadPosition);
        while (m_segments.Count > Length)
        {
            RemoveSegment(m_segments.Read());
        }

        return foodEaten;
    }

    public bool IsCollision(IntPoint worldPoint)
    {
        if (worldPoint.X < 0 || worldPoint.X >= m_arenaWidth || worldPoint.Y < 0 || worldPoint.Y >= m_arenaHeight)
            return true; // Out of playing arena.

        // Hit the tail?
        return CheckForTailHit(worldPoint);
    }

    private bool CheckForTailHit(IntPoint worldPoint)
    {
        return m_segmentCache.GetValueOrDefault(worldPoint, 0) > 0;
    }
    
    private void AddSegment(IntPoint pt)
    {
        if (m_segmentCache.TryGetValue(pt, out var count))
            m_segmentCache[pt] = count + 1;
        else
            m_segmentCache[pt] = 1;
    }

    private void RemoveSegment(IntPoint pt)
    {
        if (!m_segmentCache.TryGetValue(pt, out var count))
            return;
        if (count == 1)
            m_segmentCache.Remove(pt);
        else
            m_segmentCache[pt] = count - 1;
    }

    private Direction NormalizeDirection(Direction requestedDirection)
    {
        if (IsOpposite(Direction, requestedDirection))
            return Direction;

        return requestedDirection;
    }

    private static bool IsOpposite(Direction currentDirection, Direction requestedDirection) =>
        currentDirection switch
        {
            Direction.Left => requestedDirection == Direction.Right,
            Direction.Right => requestedDirection == Direction.Left,
            Direction.Up => requestedDirection == Direction.Down,
            Direction.Down => requestedDirection == Direction.Up,
            _ => false
        };
}
