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
using System.Collections.Generic;
using System.Linq;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

public class Snake
{
    private readonly LinkedList<(int X, int Y)> m_segments = [];
    private int m_length;

    public int X { get; private set; }
    public int Y { get; private set; }
    public Direction Direction { get; private set; }
    public IEnumerable<(int X, int Y)> Segments => m_segments;
    public int Length => m_segments.Count;

    public Snake(int arenaWidth, int arenaHeight)
    {
        X = arenaWidth / 2;
        Y = arenaHeight / 2;
        Direction = Direction.Left;
        m_length = 5;
    }

    public void Move(Direction direction)
    {
        Direction = direction;
        switch (direction)
        {
            case Direction.Left:
                X--;
                break;
            case Direction.Right:
                X++;
                break;
            case Direction.Up:
                Y--;
                break;
            case Direction.Down:
                Y++;
                break;
        }

        m_segments.AddFirst((X, Y));
        if (m_segments.Count > m_length)
            m_segments.RemoveLast();
    }

    public void Grow() =>
        m_length += 3;

    public void DrawOn(ScreenData screen)
    {
        // Draw the snake body.
        foreach (var (x, y) in Segments)
            screen.PrintAt(x, y, '■');

        // Draw snake head.
        screen.PrintAt(X, Y, '☻');
    }

    public bool IsTailHit(int x, int y) =>
        m_segments.Skip(1).Contains((x, y));
}