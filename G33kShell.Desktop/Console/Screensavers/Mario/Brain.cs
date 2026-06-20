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

using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Mario;

public class Brain : AiBrainBase
{
    protected override int BrainVersion => 4;

    public Brain() : base(GameState.InputCount, [32, 20], 4, frameStackCount: 2)
    {
    }

    private Brain(AiBrainBase brain) : base(brain)
    {
    }

    public Move ChooseMove(IAiGameState state)
    {
        var outputs = GetOutputs(state);
        var left = outputs[0] > outputs[1] && outputs[0] > 0.0;
        var right = outputs[1] >= outputs[0] && outputs[1] > -0.10;
        return new Move(left, right, outputs[2] > 0.0, outputs[3] > -0.15);
    }

    public override AiBrainBase Clone() => new Brain(this);

    public readonly record struct Move(bool Left, bool Right, bool Jump, bool Run);
}
