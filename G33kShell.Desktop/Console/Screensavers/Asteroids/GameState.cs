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
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Asteroids;

/// <summary>
/// Encodes the Asteroids arena into an egocentric sensor vector for the neural controller.
/// </summary>
/// <remarks>
/// The first block of inputs forms eight radial sensors around the ship, where each sensor
/// stores the strongest nearby asteroid presence in that direction. Extra inputs then describe
/// the nearest asteroid in ship-relative coordinates, ship velocity, shield level, bullet load,
/// and whether a shot can be fired immediately. This gives the policy a compact "what surrounds me"
/// view instead of relying on a single target summary.
/// </remarks>
public class GameState : IAiGameState
{
    private const int SensorCount = 8;

    private readonly Ship m_ship;
    private readonly List<Asteroid> m_asteroids;
    private readonly List<Bullet> m_bullets;
    private readonly Func<bool> m_isFireReady;
    private readonly int m_arenaWidth;
    private readonly int m_arenaHeight;
    private readonly float m_arenaDiagonal;

    public GameState(Ship ship, List<Asteroid> asteroids, List<Bullet> bullets, Func<bool> isFireReady, int arenaWidth, int arenaHeight)
    {
        m_ship = ship;
        m_asteroids = asteroids;
        m_bullets = bullets;
        m_isFireReady = isFireReady;
        m_arenaWidth = arenaWidth;
        m_arenaHeight = arenaHeight;

        m_arenaDiagonal = new Vector2(arenaWidth, arenaHeight).Length();
    }

    /// <summary>
    /// Fills the neural-network input vector for the current Asteroids frame.
    /// </summary>
    public void FillInputVector(double[] inputVector)
    {
        // Bias.
        inputVector[0] = 1.0;

        var shipForward = m_ship.Theta.ToDirection();
        var shipRight = new Vector2(shipForward.Y, -shipForward.X);
        Asteroid nearestAsteroid = null;
        var nearestDistance = float.MaxValue;
        Array.Clear(inputVector, 1, SensorCount);
        for (var i = 0; i < m_asteroids.Count; i++)
        {
            var asteroid = m_asteroids[i];
            var relativePos = WrapDelta(asteroid.Position - m_ship.Position);
            var distance = relativePos.LengthSquared();
            if (distance < nearestDistance)
            {
                nearestAsteroid = asteroid;
                nearestDistance = distance;
            }

            var distanceMagnitude = MathF.Sqrt(distance);
            if (distanceMagnitude <= 0.0f)
                continue;

            var asteroidDirection = relativePos / distanceMagnitude;
            var localForward = Vector2.Dot(asteroidDirection, shipForward).Clamp(-1.0f, 1.0f);
            var localRight = Vector2.Dot(asteroidDirection, shipRight).Clamp(-1.0f, 1.0f);
            var angle = MathF.Atan2(localRight, localForward);
            var sensorIndex = GetSensorIndex(angle);

            var proximity = (1.0f - distanceMagnitude / m_arenaDiagonal).Clamp(0.0f, 1.0f);
            var sizeWeight = asteroid.SizeMetric / 4.0f;
            inputVector[1 + sensorIndex] = Math.Max(inputVector[1 + sensorIndex], (proximity * sizeWeight).Clamp(0.0f, 1.0f));
        }

        if (nearestAsteroid != null)
        {
            var nearestRelativePos = WrapDelta(nearestAsteroid.Position - m_ship.Position);
            var nearestDistanceMagnitude = nearestRelativePos.Length();
            var nearestDirection = nearestDistanceMagnitude > 0.0f ? nearestRelativePos / nearestDistanceMagnitude : Vector2.Zero;
            inputVector[9] = Vector2.Dot(nearestDirection, shipForward).Clamp(-1.0f, 1.0f);
            inputVector[10] = Vector2.Dot(nearestDirection, shipRight).Clamp(-1.0f, 1.0f);
            inputVector[11] = (1.0f - nearestDistanceMagnitude / m_arenaDiagonal).Clamp(0.0f, 1.0f);
        }
        else
        {
            inputVector[9] = 0.0;
            inputVector[10] = 0.0;
            inputVector[11] = 0.0;
        }

        inputVector[12] = Vector2.Dot(m_ship.Velocity * 5.0f, shipForward).Clamp(-1.0f, 1.0f);
        inputVector[13] = Vector2.Dot(m_ship.Velocity * 5.0f, shipRight).Clamp(-1.0f, 1.0f);
        inputVector[14] = (m_ship.Shield * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[15] = ((double)m_bullets.Count / Ship.MaxBullets * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[16] = m_isFireReady() ? 1.0 : -1.0;
    }

    private Vector2 WrapDelta(Vector2 delta)
    {
        var halfWidth = m_arenaWidth / 2.0f;
        var halfHeight = m_arenaHeight / 2.0f;

        if (delta.X > halfWidth)
            delta.X -= m_arenaWidth;
        else if (delta.X < -halfWidth)
            delta.X += m_arenaWidth;

        if (delta.Y > halfHeight)
            delta.Y -= m_arenaHeight;
        else if (delta.Y < -halfHeight)
            delta.Y += m_arenaHeight;

        return delta;
    }

    private static int GetSensorIndex(float angle)
    {
        var normalized = (angle + MathF.PI) / MathF.Tau;
        var index = (int)(normalized * SensorCount);
        if (index < 0)
            return 0;
        if (index >= SensorCount)
            return SensorCount - 1;
        return index;
    }
}
