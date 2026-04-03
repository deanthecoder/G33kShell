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

using System;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.RoadFighter;

/// <summary>
/// Compact egocentric road and traffic view for the Road Fighter-style controller.
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
        inputVector[1] = m_game.GetPlayerRoadOffset().Clamp(-1.0, 1.0);
        inputVector[2] = m_game.GetLeftMargin().Clamp(0.0, 1.0);
        inputVector[3] = m_game.GetRightMargin().Clamp(0.0, 1.0);
        inputVector[4] = m_game.GetRoadCurveDelta(0, 4).Clamp(-1.0, 1.0);
        inputVector[5] = m_game.GetRoadCurveDelta(4, 10).Clamp(-1.0, 1.0);
        inputVector[6] = m_game.GetUpcomingCenterOffset(4).Clamp(-1.0, 1.0);
        inputVector[7] = m_game.GetUpcomingCenterOffset(8).Clamp(-1.0, 1.0);
        inputVector[8] = m_game.GetUpcomingCenterOffset(12).Clamp(-1.0, 1.0);
        FillTrafficBand(inputVector, 9, 0, 3);

        if (m_game.TryGetNearestTraffic(out var dx, out var dy, out var speedDelta))
        {
            inputVector[12] = dx.Clamp(-1.0, 1.0);
            inputVector[13] = dy.Clamp(0.0, 1.0);
            inputVector[14] = speedDelta.Clamp(-1.0, 1.0);
        }
        else
        {
            inputVector[12] = 0.0;
            inputVector[13] = 1.0;
            inputVector[14] = 0.0;
        }

        FillTrafficBand(inputVector, 15, 4, 8);
        FillTrafficBand(inputVector, 18, 9, 14);
        FillTrafficBand(inputVector, 21, 15, 22);
        inputVector[24] = m_game.GetLaneClearance(-1, 20).Clamp(0.0, 1.0);
        inputVector[25] = m_game.GetLaneClearance(0, 20).Clamp(0.0, 1.0);
        inputVector[26] = m_game.GetLaneClearance(1, 20).Clamp(0.0, 1.0);
        inputVector[27] = m_game.PlayerLane == -1 ? 1.0 : 0.0;
        inputVector[28] = m_game.PlayerLane == 0 ? 1.0 : 0.0;
        inputVector[29] = m_game.PlayerLane == 1 ? 1.0 : 0.0;
    }

    private void FillTrafficBand(double[] inputVector, int startIndex, int minAhead, int maxAhead)
    {
        Array.Clear(inputVector, startIndex, 3);

        for (var lane = -1; lane <= 1; lane++)
            inputVector[startIndex + lane + 1] = m_game.GetLaneDanger(lane, minAhead, maxAhead).Clamp(0.0, 1.0);
    }
}
