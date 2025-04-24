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

using CSharp.Core;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

public class Brain : AiBrainBase
{
    public Brain() : base(15, [32, 16, 16], 4)
    {
    }

    private Brain(AiBrainBase brain) : base(brain)
    {
    }

    public Direction ChooseMove(IAiGameState state) =>
        (Direction)ChooseHighestOutput(state);

    public override AiBrainBase Clone() => new Brain(this);
}