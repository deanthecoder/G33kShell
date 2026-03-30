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

using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Breakout;

/// <summary>
/// Encodes a compact Breakout state vector for the neural controller.
/// </summary>
public class GameState : IAiGameState
{
    private Game m_game;

    public GameState(Game game)
    {
        m_game = game;
    }

    public void Reset(Game game)
    {
        m_game = game;
    }

    public void FillInputVector(double[] inputVector)
    {
        inputVector[0] = 1.0;
        inputVector[1] = NormalizeX(m_game.PaddleX);
        inputVector[2] = NormalizeX(m_game.BallX);
        inputVector[3] = NormalizeY(m_game.BallY);
        inputVector[4] = (m_game.BallDx / Game.MaxBallSpeed).Clamp(-1.0, 1.0);
        inputVector[5] = (m_game.BallDy / Game.MaxBallSpeed).Clamp(-1.0, 1.0);
        inputVector[6] = ((m_game.BallX - m_game.PaddleX) / m_game.ArenaPixelWidth * 2.0).Clamp(-1.0, 1.0);
        inputVector[7] = ((m_game.PaddleY - m_game.BallY) / m_game.ArenaPixelHeight * 2.0).Clamp(-1.0, 1.0);
        inputVector[8] = ((m_game.PredictLandingX() - m_game.PaddleX) / m_game.ArenaPixelWidth * 2.0).Clamp(-1.0, 1.0);

        if (m_game.TryFindNearestBrick(out var brickX, out var brickY))
        {
            inputVector[9] = ((brickX - m_game.BallX) / m_game.ArenaPixelWidth * 2.0).Clamp(-1.0, 1.0);
            inputVector[10] = ((brickY - m_game.BallY) / m_game.ArenaPixelHeight * 2.0).Clamp(-1.0, 1.0);
        }
        else
        {
            inputVector[9] = 0.0;
            inputVector[10] = -1.0;
        }

        inputVector[11] = (m_game.BrickCompletionRatio * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[12] = ((double)m_game.Lives / Game.MaxLives * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[13] = m_game.BallDy > 0.0 ? 1.0 : -1.0;
        inputVector[14] = m_game.GetBrickDensityAroundBall(-1).Clamp(0.0, 1.0);
        inputVector[15] = m_game.GetBrickDensityAroundBall(0).Clamp(0.0, 1.0);
        inputVector[16] = m_game.GetBrickDensityAroundBall(1).Clamp(0.0, 1.0);
    }

    private double NormalizeX(double x) => (x / m_game.ArenaPixelWidth * 2.0 - 1.0).Clamp(-1.0, 1.0);

    private double NormalizeY(double y) => (y / m_game.ArenaPixelHeight * 2.0 - 1.0).Clamp(-1.0, 1.0);
}
