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
using CSharp.Core;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

/// <summary>
/// Captures the state of the game into a 'double' array that can be fed into the neural network.
/// </summary>
public class GameState : IAiGameState
{
    private readonly Snake m_snake;
    private readonly IntPoint m_foodPosition;

    public GameState(Snake snake, IntPoint foodPosition)
    {
        m_snake = snake;
        m_foodPosition = foodPosition;
    }

    public double[] ToInputVector()
    {
        var inputVector = new List<double>();

        // Encode snake length.
        inputVector.Add((m_snake.Length / (double)m_snake.MaxLength).Clamp(0.0, 1.0));
        
        // Encode snake direction.
        inputVector.Add(m_snake.Direction == Direction.Up ? -1.0 : m_snake.Direction == Direction.Down ? 1.0 : 0.0);
        inputVector.Add(m_snake.Direction == Direction.Left ? -1.0 : m_snake.Direction == Direction.Right ? 1.0 : 0.0);
        
        // Encode distances to food.
        inputVector.Add(((m_snake.HeadPosition.X - m_foodPosition.X) / 16.0).Clamp(-1.0, 1.0));
        inputVector.Add(((m_snake.HeadPosition.Y - m_foodPosition.Y) / 16.0).Clamp(-1.0, 1.0));
        
        // Encode time since food was eaten.
        inputVector.Add((m_snake.StepsSinceFood / (double)m_snake.TotalStepsToStarvation).Clamp(0.0, 1.0));
        
        // Encode local view.
        inputVector.AddRange(GetLocalViewGrid());

        return inputVector.ToArray();
    }

    private double[] GetLocalViewGrid()
    {
        const int radius = 2;
        var grid = new double[9];

        // Local coordinates: (0,0) is top-left of local grid
        var i = 0;
        for (var dx = -radius; dx <= radius; dx++)
        {
            var world = m_snake.HeadPosition.WithDelta(dx, 0);
            
            // Get content at this world cell
            if (m_snake.IsCollision(world))
                grid[i] = -1.0;
            if (world == m_foodPosition)
                grid[i] = 0.5;

            i++;
        }

        for (var dy = -radius; dy <= radius; dy++)
        {
            if (dy == 0)
                continue;
            
            var world = m_snake.HeadPosition.WithDelta(0, dy);
            
            // Get content at this world cell
            if (m_snake.IsCollision(world))
                grid[i] = -1.0;
            if (world == m_foodPosition)
                grid[i] = 0.5;

            i++;
        }

        return grid;
    }
}