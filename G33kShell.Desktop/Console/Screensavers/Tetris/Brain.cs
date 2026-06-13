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

namespace G33kShell.Desktop.Console.Screensavers.Tetris;

/// <summary>
/// Neural placement evaluator for the Tetris screensaver.
/// </summary>
public class Brain : AiBrainBase
{
    protected override int BrainVersion => 6;

    public Brain() : base(GameState.InputCount, [32, 20], 1)
    {
    }

    private Brain(Brain brain) : base(brain)
    {
    }

    public double ScorePlacement(GameState state) => GetOutputs(state)[0];

    public override AiBrainBase Clone() => new Brain(this);
}
