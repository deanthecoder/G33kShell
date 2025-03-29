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
using CSharp.Core;
using CSharp.Core.Extensions;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

public class GameState
{
    private readonly Flags m_flags;
    private readonly int m_snakeLength;
    private readonly IntPoint m_snakePosition;
    private readonly IntPoint m_foodPosition;
    private readonly int m_stepsSinceFood;

    public const int LocalViewDimension = 5;
    public const int SpaceAwarenessLength = 10;

    [Flags]
    public enum Flags : ushort
    {
        MovingLeft = 1 << 1,
        MovingRight = 1 << 2,
        MovingUp = 1 << 3,
        MovingDown = 1 << 4,
        FoodUp = 1 << 5,
        FoodDown = 1 << 6,
        FoodLeft = 1 << 7,
        FoodRight = 1 << 8
    }

    public GameState(Flags flags, int snakeLength, IntPoint snakePosition, IntPoint foodPosition, int stepsSinceFood)
    {
        m_flags = flags;
        m_snakeLength = snakeLength;
        m_snakePosition = snakePosition;
        m_foodPosition = foodPosition;
        m_stepsSinceFood = stepsSinceFood;
    }

    public double[] SurroundingCells { get; } = new double[LocalViewDimension * LocalViewDimension];
    public double[] LocalSpaceAwareness { get; } = new double[SpaceAwarenessLength];

    public double[] ToInputVector()
    {
        var inputVector = new double[
            Enum.GetValues<Flags>().Length +
            1 + // Snake length.
            1 + // Distance to food.
            1 + // Steps since food.
            SurroundingCells.Length +
            LocalSpaceAwareness.Length];

        // Encode flags.
        var dataIndex = 0;
        foreach (var flag in Enum.GetValues<Flags>())
            inputVector[dataIndex++] = m_flags.HasFlag(flag) ? 1.0 : 0.0;
        
        // Encode snake length.
        inputVector[dataIndex++] = (m_snakeLength / 300.0).Clamp(0.0, 1.0);
        
        // Encode distance to food.
        inputVector[dataIndex++] = (m_snakePosition.ManhattanDistance(m_foodPosition) / 100.0).Clamp(0.0, 1.0);
        
        // Encode time since food was eaten.
        inputVector[dataIndex++] = (m_stepsSinceFood / 200.0).Clamp(0.0, 1.0);
        
        // Encode local view.
        foreach (var surroundingCell in SurroundingCells)
            inputVector[dataIndex++] = surroundingCell;
        
        // Encode space awareness.
        foreach (var spaceAwareness in LocalSpaceAwareness)
            inputVector[dataIndex++] = spaceAwareness;

        return inputVector;
    }
}