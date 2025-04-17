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

namespace G33kShell.Desktop.Console.Screensavers.Asteroids;

/// <summary>
/// <inheritdoc cref="IAiGameState"/>
/// </summary>
public class GameState : IAiGameState
{
    private readonly double[] m_inputVector = new double[3];
    private readonly Ship m_ship;
    private readonly List<Asteroid> m_asteroids;
    private readonly float m_arenaDiagonal;

    public GameState(Ship ship, List<Asteroid> asteroids, int arenaWidth, int arenaHeight)
    {
        m_ship = ship;
        m_asteroids = asteroids;

        m_arenaDiagonal = new Vector2(arenaWidth, arenaHeight).Length();
    }

    public double[] ToInputVector()
    {
        // Bias.
        m_inputVector[0] = 1.0;

        // Find nearest asteroid.
        Asteroid asteroid = null;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < m_asteroids.Count; i++)
        {
            var d = Vector2.DistanceSquared(m_asteroids[i].Position, m_ship.Position);
            if (d > bestDistance)
                continue;
            asteroid = m_asteroids[i];
            bestDistance = d;
        }
        
        if (asteroid != null)
        {
            var relativePos = asteroid.Position - m_ship.Position;
            var angleToAsteroid = Vector2.Dot(Vector2.Normalize(relativePos), m_ship.Theta.ToDirection()).Clamp(-1.0f, 1.0f);
            m_inputVector[1] = angleToAsteroid;
            m_inputVector[2] = 1.0 - relativePos.Length() / m_arenaDiagonal;
        }
        else
        {
            m_inputVector[1] = 0.0;
            m_inputVector[2] = 0.0;
        }

        return m_inputVector;
    }
}