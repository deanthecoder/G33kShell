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

public class GameState : IAiGameState
{
    public const int SensorGridSizeX = 10;
    public const int SensorGridSizeY = 13;
    public const int SensorBlockTileSize = 2;
    public const int SensorOriginBlockDx = -1;
    private const int ScalarInputCount = 7;
    public const int InputCount = SensorGridSizeX * SensorGridSizeY + ScalarInputCount;
    private readonly Game m_game;

    public GameState(Game game)
    {
        m_game = game;
    }

    public void FillInputVector(double[] inputVector)
    {
        var i = 0;
        for (var y = 0; y < SensorGridSizeY; y++)
        {
            for (var x = 0; x < SensorGridSizeX; x++)
                inputVector[i++] = m_game.GetBlockSensorValue(SensorOriginBlockDx + x, y);
        }

        inputVector[i++] = 1.0;
        inputVector[i++] = m_game.MarioBlockXOffset;
        inputVector[i++] = m_game.MarioBlockYOffset;
        inputVector[i++] = m_game.MarioVelocityX / Game.MaxRunPixelsPerFrame;
        inputVector[i++] = m_game.MarioVelocityY / Game.MaxFallPixelsPerFrame;
        inputVector[i++] = m_game.IsGrounded ? 1.0 : -1.0;
        inputVector[i] = m_game.IsJumpHeld ? 1.0 : -1.0;
    }
}
