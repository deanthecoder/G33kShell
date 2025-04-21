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

using System;
using CSharp.Core;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Pong;

public class Brain : AiBrainBase
{
    public const int BrainInputCount = 8;

    public Brain() : base(BrainInputCount, [16], 4)
    {
    }

    private Brain(Brain brain) : base(brain)
    {
    }
    public (Direction LeftBat, Direction RightBat) ChooseMoves(IAiGameState state)
    {
        var outputs = GetOutputs(state);

        var leftBatDirection = Direction.Left; // No move.
        var diff = outputs[0] - outputs[1];
        if (Math.Abs(diff) > 0.2) // Apply a threshold for a move.
            leftBatDirection = diff > 0 ? Direction.Up : Direction.Down;

        var rightBatDirection = Direction.Left; // No move.
        diff = outputs[2] - outputs[3];
        if (Math.Abs(diff) > 0.2)
            rightBatDirection = diff > 0 ? Direction.Up : Direction.Down;
            
        return (leftBatDirection, rightBatDirection);
    }

    public override AiBrainBase Clone() => new Brain(this);
}