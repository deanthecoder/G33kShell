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

namespace G33kShell.Desktop.Console.Screensavers.Mario;

public class GameState : IAiGameState
{
    public const int InputCount = 27;
    private readonly Game m_game;

    public GameState(Game game)
    {
        m_game = game;
    }

    public void FillInputVector(double[] inputVector)
    {
        var i = 0;
        inputVector[i++] = (m_game.MarioVelocityX / Game.MaxRunPixelsPerFrame).Clamp(0.0, 1.0);
        inputVector[i++] = (m_game.MarioVelocityY / 8.0).Clamp(-1.0, 1.0);
        inputVector[i++] = m_game.IsGrounded ? 1.0 : -1.0;
        inputVector[i++] = ((m_game.MarioY + Game.MarioCollisionHeight) / Game.ViewHeight).Clamp(0.0, 1.0);
        inputVector[i++] = (m_game.TicksSinceLastJump / 90.0).Clamp(0.0, 1.0);
        inputVector[i++] = ((m_game.BestX - m_game.MarioX) / 64.0).Clamp(0.0, 1.0);
        inputVector[i++] = m_game.DistanceToNextObstacle / 128.0;
        inputVector[i++] = m_game.DistanceToNextGap / 128.0;
        inputVector[i++] = m_game.DistanceToNextOverheadBlock / 128.0;
        inputVector[i++] = m_game.DistanceToNextPipe / 192.0;
        inputVector[i++] = (m_game.NextGroundDeltaY / 80.0).Clamp(-1.0, 1.0);
        inputVector[i++] = m_game.IsBlockedAhead ? 1.0 : -1.0;
        inputVector[i++] = m_game.IsGapAhead ? 1.0 : -1.0;
        inputVector[i++] = m_game.HasCeilingAbove ? 1.0 : -1.0;
        inputVector[i++] = m_game.RunMood;

        foreach (var sample in Game.SensorSamples)
            inputVector[i++] = m_game.IsSolidAtOffset(sample.X, sample.Y) ? 1.0 : -1.0;
    }
}
