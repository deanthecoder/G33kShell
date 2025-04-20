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

 namespace G33kShell.Desktop.Console.Screensavers.Asteroids;

public class Brain : AiBrainBase
{
    public const int BrainInputCount = 10;

    public Brain() : base(BrainInputCount, [32, 16], 4)
    {
    }

    private Brain(Brain brain) : base(brain)
    {
    }

    public (Ship.Turn Turn, bool IsShooting, bool IsThrusting) ChooseMoves(IAiGameState state)
    {
        var outputs = GetOutputs(state);

        var turn = Ship.Turn.None;
        if (outputs[0] > outputs[1] && outputs[0] > 0.9)
            turn = Ship.Turn.Left;
        else if (outputs[1] > outputs[0] && outputs[1] > 0.9)
            turn = Ship.Turn.Right;

        var shoot = outputs[2] > 0.9;
        var thrust = outputs[3] > 0.9;
        
        return (turn, shoot, thrust);
    }

    public override AiBrainBase Clone() => new Brain(this);
}