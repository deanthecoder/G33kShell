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

/// <summary>
/// Neural controller for the Asteroids screensaver.
/// </summary>
/// <remarks>
/// The network consumes an egocentric state vector built by <see cref="GameState"/>:
/// radial asteroid danger sensors, nearest-target direction, ship motion, shield, bullet load,
/// and whether the weapon cooldown is ready. The four outputs are interpreted as left turn,
/// right turn, shoot, and thrust intents. During training the weights evolve through the
/// shared neuroevolution loop in <see cref="AI.AiGameCanvasBase"/>.
/// </remarks>
public class Brain : AiBrainBase
{
    protected override int BrainVersion => 8;

    public Brain() : base(17, [32, 16], 4)
    {
    }

    private Brain(Brain brain) : base(brain)
    {
    }

    public (Ship.Turn Turn, bool IsShooting, bool IsThrusting) ChooseMoves(IAiGameState state)
    {
        var outputs = GetOutputs(state);

        var turn = Ship.Turn.None;
        var turnBias = outputs[0] - outputs[1];
        if (turnBias > 0.15)
            turn = Ship.Turn.Left;
        else if (turnBias < -0.15)
            turn = Ship.Turn.Right;

        var shoot = outputs[2] > 0.15;
        var thrust = outputs[3] > 0.05;
        
        return (turn, shoot, thrust);
    }

    public override AiBrainBase Clone() => new Brain(this);
}
