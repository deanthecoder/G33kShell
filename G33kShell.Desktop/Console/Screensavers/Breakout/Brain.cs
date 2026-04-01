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

namespace G33kShell.Desktop.Console.Screensavers.Breakout;

/// <summary>
/// Neural controller for the Breakout screensaver.
/// </summary>
public class Brain : AiBrainBase
{
    private const int FrameHistory = 4;

    protected override int BrainVersion => 6;

    public Brain() : base(17, [28, 18], 3, FrameHistory)
    {
    }

    private Brain(Brain brain) : base(brain)
    {
    }

    public int ChooseMove(IAiGameState state)
    {
        var outputs = GetOutputs(state);

        var stayScore = outputs[1];
        var moveLeftScore = outputs[0];
        var moveRightScore = outputs[2];

        if (moveLeftScore < stayScore + 0.08 && moveRightScore < stayScore + 0.08)
            return 0;

        return moveLeftScore > moveRightScore ? -1 : 1;
    }

    public override AiBrainBase Clone() => new Brain(this);
}
