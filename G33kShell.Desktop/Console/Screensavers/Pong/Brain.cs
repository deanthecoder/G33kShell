// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
// purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Numerics;
using CSharp.Core;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Pong;

public class Brain : AiBrainBase
{
    public Brain() : base(GetInputSize(), [10], 6)
    {
    }

    private static int GetInputSize() =>
        new GameState([Vector2.One, Vector2.One], Vector2.One, Vector2.One, 1, 1).ToInputVector().Length;

    public (Direction LeftBat, Direction RightBat) ChooseMoves(IAiGameState state)
    {
        var outputs = GetOutputs(state);

        var leftBatDirection = Direction.Left; // No move.
        if (outputs[0] > outputs[1] && outputs[0] > outputs[2])
            leftBatDirection = Direction.Up;
        else if (outputs[2] > outputs[0] && outputs[2] > outputs[1])
            leftBatDirection = Direction.Down;

        var rightBatDirection = Direction.Left; // No move.
        if (outputs[3] > outputs[4] && outputs[3] > outputs[5])
            rightBatDirection = Direction.Up;
        else if (outputs[5] > outputs[3] && outputs[5] > outputs[4])
            rightBatDirection = Direction.Down;
            
        return (leftBatDirection, rightBatDirection);
    }
}