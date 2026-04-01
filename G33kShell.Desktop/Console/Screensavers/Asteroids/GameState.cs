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
/// the nearest asteroid, a threat-weighted priority asteroid, ship velocity, shield level, bullet
/// load, weapon readiness, and a derived firing-lane cue. This gives the policy a compact
/// "what surrounds me" view plus a hint about when the current tactical picture is favourable
/// for taking the shot.
/// </remarks>
public class GameState : IAiGameState
{
    private const int SensorCount = 8;
    private const int PriorityTargetOffset = 12;

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
        Asteroid priorityAsteroid = null;
        var nearestDistance = float.MaxValue;
        var bestThreatScore = float.MinValue;
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

            var closingSpeed = GetClosingSpeed(relativePos, asteroid.MovementVelocity);
            var threatScore = proximity * 1.5f + Math.Max(0.0f, closingSpeed) * 1.1f + sizeWeight * 0.35f;
            if (threatScore > bestThreatScore)
            {
                bestThreatScore = threatScore;
                priorityAsteroid = asteroid;
            }
        }

        FillTargetBlock(inputVector, 9, nearestAsteroid, shipForward, shipRight);
        FillTargetBlock(inputVector, PriorityTargetOffset, priorityAsteroid, shipForward, shipRight);

        inputVector[17] = Vector2.Dot(m_ship.Velocity * 5.0f, shipForward).Clamp(-1.0f, 1.0f);
        inputVector[18] = Vector2.Dot(m_ship.Velocity * 5.0f, shipRight).Clamp(-1.0f, 1.0f);
        inputVector[19] = (m_ship.Shield * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[20] = ((double)m_bullets.Count / Ship.MaxBullets * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[21] = m_isFireReady() ? 1.0 : -1.0;
        inputVector[22] = GetFiringLaneScore(priorityAsteroid ?? nearestAsteroid, shipForward, shipRight);
    }

    private void FillTargetBlock(double[] inputVector, int offset, Asteroid asteroid, Vector2 shipForward, Vector2 shipRight)
    {
        if (asteroid == null)
        {
            for (var i = 0; i < 5; i++)
                inputVector[offset + i] = 0.0;
            return;
        }

        var relativePos = WrapDelta(asteroid.Position - m_ship.Position);
        var distanceMagnitude = relativePos.Length();
        var direction = distanceMagnitude > 0.0f ? relativePos / distanceMagnitude : Vector2.Zero;
        inputVector[offset] = Vector2.Dot(direction, shipForward).Clamp(-1.0f, 1.0f);
        inputVector[offset + 1] = Vector2.Dot(direction, shipRight).Clamp(-1.0f, 1.0f);
        inputVector[offset + 2] = (1.0f - distanceMagnitude / m_arenaDiagonal).Clamp(0.0f, 1.0f);
        inputVector[offset + 3] = GetClosingSpeed(relativePos, asteroid.MovementVelocity);
        inputVector[offset + 4] = (asteroid.SizeMetric / 4.0f * 2.0f - 1.0f).Clamp(-1.0f, 1.0f);
    }

    private float GetClosingSpeed(Vector2 relativePos, Vector2 asteroidVelocity)
    {
        var distanceMagnitude = relativePos.Length();
        if (distanceMagnitude <= 0.0001f)
            return 0.0f;

        var toShip = -relativePos / distanceMagnitude;
        var relativeVelocity = asteroidVelocity - m_ship.Velocity;
        return (Vector2.Dot(relativeVelocity, toShip) / 0.35f).Clamp(-1.0f, 1.0f);
    }

    private double GetFiringLaneScore(Asteroid asteroid, Vector2 shipForward, Vector2 shipRight)
    {
        if (asteroid == null)
            return -1.0;

        var relativePos = WrapDelta(asteroid.Position - m_ship.Position);
        var distanceMagnitude = relativePos.Length();
        if (distanceMagnitude <= 0.0001f)
            return 1.0;

        var direction = relativePos / distanceMagnitude;
        var forward = Vector2.Dot(direction, shipForward).Clamp(-1.0f, 1.0f);
        var lateral = Math.Abs(Vector2.Dot(direction, shipRight).Clamp(-1.0f, 1.0f));
        var proximity = (1.0f - distanceMagnitude / m_arenaDiagonal).Clamp(0.0f, 1.0f);
        var closing = Math.Max(0.0f, GetClosingSpeed(relativePos, asteroid.MovementVelocity));
        var aimWindow = Math.Max(0.0f, forward) * Math.Max(0.0f, 1.0f - lateral * 2.5f);
        var firingLane = aimWindow * (0.65f + proximity * 0.35f) * (0.7f + closing * 0.3f);
        return (firingLane * 2.0f - 1.0f).Clamp(-1.0f, 1.0f);
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
