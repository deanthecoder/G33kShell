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
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A canvas displaying Conway's Game of Life as a screensaver.
/// </summary>
[DebuggerDisplay("GameOfLifeCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class GameOfLifeCanvas : ScreensaverBase
{
    private readonly bool[,] m_cells;
    private readonly Queue<int> m_checksums = new Queue<int>();
    private const int ChecksumHistorySize = 10;

    public GameOfLifeCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 15)
    {
        Name = "conway";
        m_cells = new bool[screenWidth, screenHeight * 2];
        InitializeRandomCells();
    }

    private void InitializeRandomCells()
    {
        for (var y = 0; y < m_cells.GetLength(1); y++)
        {
            for (var x = 0; x < m_cells.GetLength(0); x++)
                m_cells[x, y] = Random.Shared.NextDouble() < 0.25; // 25% chance of a live cell
        }
    }

    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);
        
        var highResScreen = new HighResScreen(screen);

        for (var y = 0; y < m_cells.GetLength(1); y++)
        {
            for (var x = 0; x < m_cells.GetLength(0); x++)
                if (m_cells[x, y])
                    highResScreen.Plot(x, y, Foreground);
        }
    }

    public override void UpdateFrame(ScreenData screen)
    {
        var nextGen = new bool[m_cells.GetLength(0), m_cells.GetLength(1)];
        
        for (var y = 0; y < m_cells.GetLength(1); y++)
        {
            for (var x = 0; x < m_cells.GetLength(0); x++)
            {
                var liveNeighbors = CountLiveNeighbors(x, y);
                if (m_cells[x, y])
                    nextGen[x, y] = liveNeighbors == 2 || liveNeighbors == 3; // Survive
                else
                    nextGen[x, y] = liveNeighbors == 3; // Birth
            }
        }

        if (ShouldReset(nextGen))
            InitializeRandomCells();
        else
            Array.Copy(nextGen, m_cells, m_cells.GetLength(0) * m_cells.GetLength(1));

        BuildScreen(screen);
    }

    private int CountLiveNeighbors(int x, int y)
    {
        var width = m_cells.GetLength(0);
        var height = m_cells.GetLength(1);

        var count = 0;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;
                int nx = x + dx, ny = y + dy;
                
                // Wrap coordinates to screen edge.
                if (nx < 0)
                    nx += width;
                else if (nx >= width)
                    nx -= width;
                if (ny < 0)
                    ny += height;
                else if (ny >= height)
                    ny -= height;
                
                if (m_cells[nx, ny])
                    count++;
            }
        }
        
        return count;
    }

    private bool ShouldReset(bool[,] nextGen)
    {
        var checksum = ComputeChecksum(nextGen);
        if (m_checksums.Contains(checksum))
            return true; // We've seen this frame before - Reset.

        m_checksums.Enqueue(checksum);
        if (m_checksums.Count > ChecksumHistorySize)
            m_checksums.Dequeue();
        return false;
    }

    private int ComputeChecksum(bool[,] grid)
    {
        var sum = 0;
        for (var y = 0; y < m_cells.GetLength(1); y++)
        {
            for (var x = 0; x < m_cells.GetLength(0); x++)
                sum = (sum * 31) + (grid[x, y] ? 1 : 0);
        }
        
        return sum;
    }
}