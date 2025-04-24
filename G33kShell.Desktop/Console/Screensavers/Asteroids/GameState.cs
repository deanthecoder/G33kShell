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
    private readonly Ship m_ship;
    private readonly List<Asteroid> m_asteroids;
    private readonly float m_arenaDiagonal;

    public GameState(Ship ship, List<Asteroid> asteroids, int arenaWidth, int arenaHeight)
    {
        m_ship = ship;
        m_asteroids = asteroids;

        m_arenaDiagonal = new Vector2(arenaWidth, arenaHeight).Length();
    }

    public void FillInputVector(double[] inputVector)
    {
        // Bias.
        inputVector[0] = 1.0;

        // Find nearest asteroid.
        var asteroid = m_asteroids.Count == 0 ? null : m_asteroids.FastFindMin(o => Vector2.DistanceSquared(o.Position, m_ship.Position));
        if (asteroid != null)
        {
            var relativePos = asteroid.Position - m_ship.Position;
            var angleToAsteroid = Vector2.Dot(Vector2.Normalize(relativePos), m_ship.Theta.ToDirection()).Clamp(-1.0f, 1.0f);
            inputVector[1] = angleToAsteroid;
            inputVector[2] = 1.0 - relativePos.Length() / m_arenaDiagonal;
        }
        else
        {
            inputVector[1] = 0.0;
            inputVector[2] = 0.0;
        }
        
        var relativeVelocity = asteroid?.Velocity ?? Vector2.Zero - m_ship.Velocity;
        relativeVelocity = relativeVelocity.LengthSquared() > 0.0f ? Vector2.Normalize(relativeVelocity) : Vector2.Zero;
        inputVector[3] = relativeVelocity.X.Clamp(-1.0f, 1.0f);
        inputVector[4] = relativeVelocity.Y.Clamp(-1.0f, 1.0f);
        inputVector[5] = (m_ship.Theta / Math.Tau).Clamp(-1.0f, 1.0f);
    }
}