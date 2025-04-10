using CSharp.Core;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

public class Brain : AiBrainBase
{
    public Brain() : base(GetInputSize(), [32, 16, 16], 4)
    {
    }

    private static int GetInputSize() =>
        new GameState(new Snake(16, 16), IntPoint.Zero).ToInputVector().Length;

    public Direction ChooseMove(IAiGameState state) =>
        (Direction)ChooseHighestOutput(state);
}