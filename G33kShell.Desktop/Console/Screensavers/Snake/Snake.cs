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
using System.Linq;
using CSharp.Core;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

public class Snake
{
    private readonly int m_arenaWidth;
    private readonly int m_arenaHeight;
    private readonly LinkedList<IntPoint> m_segments = [];
    private readonly Dictionary<IntPoint, int> m_segmentCache = [];

    public enum DeathType { None, Wall, Self, Starved }

    public event EventHandler FoodEaten;
    public event EventHandler Starved;
    public event EventHandler<DeathType> TerminalCollision;

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
        
        HeadPosition = new IntPoint(arenaWidth / 2, arenaHeight / 2);
    }

    public void Move(Direction direction, IntPoint foodPosition)
    {
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

        if (IsCollision(HeadPosition, out var collisionType))
        {
            IsDead = true;
            TerminalCollision?.Invoke(this, collisionType);
            return;
        }

        StepsSinceFood++;
        if (StepsSinceFood >= TotalStepsToStarvation)
        {
            IsDead = true;
            Starved?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (HeadPosition == foodPosition)
        {
            FoodEaten?.Invoke(this, EventArgs.Empty);
            StepsSinceFood = 0;
            Length = Math.Min(Length + 3, MaxLength);
        }
        
        // Extend snake.
        m_segments.AddFirst(oldHeadPosition);
        AddSegment(oldHeadPosition);
        if (m_segments.Count > Length)
        {
            RemoveSegment(m_segments.Last!.Value);
            m_segments.RemoveLast();
        }
    }

    public bool IsCollision(IntPoint worldPoint, out DeathType deathType)
    {
        if (worldPoint.X < 0 || worldPoint.X >= m_arenaWidth || worldPoint.Y < 0 || worldPoint.Y >= m_arenaHeight)
        {
            deathType = DeathType.Wall;
            return true; // Out of playing arena.
        }
        
        // Hit the tail?
        if (CheckForTailHit(worldPoint))
        {
            deathType = DeathType.Self;
            return true; // Hit the tail.
        }
        
        deathType = DeathType.None;
        return false;
    }

    private bool CheckForTailHit(IntPoint worldPoint)
    {
        var count = m_segmentCache.GetValueOrDefault(worldPoint, 0);
        if (m_segments.FirstOrDefault().Equals(worldPoint))
            count--;
        return count > 0;
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
}