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
using System;
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
    private readonly double[] m_inputVector = new double[Brain.BrainInputCount];
    private readonly Ship m_ship;
    private readonly List<Asteroid> m_asteroids;
    private readonly List<Bullet> m_bullets;
    private readonly float m_arenaDiagonal;

    public GameState(Ship ship, List<Asteroid> asteroids, List<Bullet> bullets, int arenaWidth, int arenaHeight)
    {
        m_ship = ship;
        m_asteroids = asteroids;
        m_bullets = bullets;

        m_arenaDiagonal = new Vector2(arenaWidth, arenaHeight).Length();
    }

    public double[] ToInputVector()
    {
        // Bias.
        m_inputVector[0] = 1.0;

        // Find nearest asteroid.
        var asteroid = m_asteroids.Count == 0 ? null : m_asteroids.FastFindMin(o => Vector2.DistanceSquared(o.Position, m_ship.Position));
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
        
        var normalizedVelocity = m_ship.Velocity == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(m_ship.Velocity);
        m_inputVector[3] = normalizedVelocity.X.Clamp(-1.0f, 1.0f);
        m_inputVector[4] = normalizedVelocity.Y.Clamp(-1.0f, 1.0f);
        m_inputVector[5] = (m_ship.Theta / Math.Tau).Clamp(-1.0f, 1.0f);
        m_inputVector[6] = m_ship.IsShooting ? 1.0 : 0.0;
        m_inputVector[7] = m_ship.IsThrusting ? 1.0 : 0.0;
        m_inputVector[8] = m_ship.Turning switch
        {
            Ship.Turn.Left => -1.0,
            Ship.Turn.Right => 1.0,
            _ => 0.0
        };
        m_inputVector[9] = (double)m_bullets.Count / Ship.MaxBullets;

        return m_inputVector;
    }
}