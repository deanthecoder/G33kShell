// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
//  purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.
using System.Collections.Generic;
using System.Numerics;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Pong;

/// <summary>
/// Captures the state of the game into a 'double' array that can be fed into the neural network.
/// </summary>
public class GameState : IAiGameState
{
    private readonly Vector2[] m_bats;
    private readonly Vector2 m_ballPosition;
    private readonly Vector2 m_ballVelocity;
    private readonly int m_arenaWidth;
    private readonly int m_arenaHeight;

    public GameState(Vector2[] bats, Vector2 ballPosition, Vector2 ballVelocity, int arenaWidth, int arenaHeight)
    {
        m_bats = bats;
        m_ballPosition = ballPosition;
        m_ballVelocity = ballVelocity;
        m_arenaWidth = arenaWidth;
        m_arenaHeight = arenaHeight;
    }

    public double[] ToInputVector()
    {
        var inputVector = new List<double>();

        // Encode bat positions.
        inputVector.Add(m_bats[0].Y / m_arenaHeight * 2.0f - 1.0f);
        inputVector.Add(m_bats[1].Y / m_arenaHeight * 2.0f - 1.0f);
        
        // Encode ball state.
        inputVector.Add(m_ballPosition.X / m_arenaWidth * 2.0f - 1.0f);
        inputVector.Add(m_ballPosition.Y / m_arenaHeight * 2.0f - 1.0f);
        inputVector.Add(m_ballVelocity.X.Clamp(-1.0f, 1.0f));
        inputVector.Add(m_ballVelocity.Y.Clamp(-1.0f, 1.0f));

        return inputVector.ToArray();
    }
}