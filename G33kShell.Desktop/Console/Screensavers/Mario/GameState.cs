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
using DTC.Core.Extensions;

namespace G33kShell.Desktop.Console.Screensavers.Mario;

public class GameState : IAiGameState
{
    private const int SensorGridSize = 8;
    private const int SensorChannelCount = 3;
    private const int ScalarInputCount = 35;
    public const int InputCount = SensorGridSize * SensorGridSize * SensorChannelCount + ScalarInputCount;
    private readonly Game m_game;

    public GameState(Game game)
    {
        m_game = game;
    }

    public void FillInputVector(double[] inputVector)
    {
        var i = 0;
        for (var y = 0; y < SensorGridSize; y++)
        {
            for (var x = 0; x < SensorGridSize; x++)
            {
                m_game.GetTileSensor(x, 2 - y, out var solid, out var question, out var enemy);
                inputVector[i++] = solid;
                inputVector[i++] = question;
                inputVector[i++] = enemy;
            }
        }

        inputVector[i++] = 1.0;
        inputVector[i++] = m_game.MarioTileXOffset;
        inputVector[i++] = (m_game.MarioVelocityX / Game.MaxRunPixelsPerFrame).Clamp(-1.0, 1.0);
        inputVector[i++] = (m_game.MarioVelocityY / Game.MaxFallPixelsPerFrame).Clamp(-1.0, 1.0);
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
        inputVector[i++] = m_game.DistanceToNextQuestionBlock / 192.0;
        inputVector[i++] = (m_game.NextQuestionBlockDeltaY / 128.0).Clamp(-1.0, 1.0);
        inputVector[i++] = m_game.HasQuestionBlockAhead ? 1.0 : -1.0;
        inputVector[i++] = m_game.HasQuestionBlockInJumpZone ? 1.0 : -1.0;
        inputVector[i++] = m_game.RunMood;
        inputVector[i++] = m_game.NearestEnemyDeltaX / 192.0;
        inputVector[i++] = (m_game.NearestEnemyDeltaY / 96.0).Clamp(-1.0, 1.0);
        inputVector[i++] = m_game.HasEnemyAhead ? 1.0 : -1.0;
        inputVector[i++] = m_game.HasEnemyThreat ? 1.0 : -1.0;
        inputVector[i++] = m_game.HasStompableEnemyAhead ? 1.0 : -1.0;
        inputVector[i++] = m_game.DistanceToEnemyAhead / 160.0;
        inputVector[i++] = m_game.DistanceToEnemyThreat / 80.0;
        inputVector[i++] = m_game.EnemyClosingSpeed;
        inputVector[i++] = m_game.HasEnemyBeside ? 1.0 : -1.0;
        inputVector[i++] = m_game.HasEnemyLandingTarget ? 1.0 : -1.0;
        inputVector[i++] = m_game.HasEnemyOverhead ? 1.0 : -1.0;
        inputVector[i++] = m_game.DistanceToFlagPole / 512.0;
        inputVector[i++] = m_game.IsNearFlagPole ? 1.0 : -1.0;
        inputVector[i] = m_game.FlagPoleLaunchReadiness;
    }
}
