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

namespace G33kShell.Desktop.Console.Screensavers.RoadFighter;

public class Brain : AiBrainBase
{
    private const int FrameHistory = 1;

    protected override int BrainVersion => 8;

    public Brain() : base(30, [24, 12], 3, FrameHistory)
    {
    }

    private Brain(AiBrainBase brain) : base(brain)
    {
    }

    public int ChooseMove(IAiGameState state)
    {
        var outputs = GetOutputs(state);
        var left = outputs[0];
        var stay = outputs[1];
        var right = outputs[2];

        if (left > stay && left >= right)
            return -1;

        if (right > stay && right > left)
            return 1;

        return 0;
    }

    public override AiBrainBase Clone() => new Brain(this);
}
