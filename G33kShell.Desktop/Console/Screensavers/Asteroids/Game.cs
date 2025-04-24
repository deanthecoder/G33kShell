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
using System.Diagnostics;
using System.Numerics;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Asteroids;

[DebuggerDisplay("Rating = {Rating}, Score = {Score}")]
public class Game : AiGameBase
{
    private int m_bulletsFired;
    private int m_gameTicks;
    private int m_leftTurns;
    private int m_rightTurns;
    private GameState m_gameState;
    private int m_thrustTicks;
    private int m_ticksSinceScore;

    /// <summary>
    /// Score used for public display.
    /// </summary>
    public int Score { get; private set; }

    public override double Rating
    {
        get
        {
            if (m_thrustTicks == 0)
                return 0.0; // Penalize non-thrusters.

            var rating = 1.0 + Score * Score * HitRatio * Math.Min(m_gameTicks, 20 * 60) * TurnEquality * 0.00001;
            return rating * rating;
        }
    }
    
    public override IEnumerable<(string Name, string Value)> ExtraGameStats()
    {
        yield return ("Score", Score.ToString());
        yield return ("HitRatio", HitRatio.ToString("P1"));
        yield return ("Ticks", m_gameTicks.ToString());
        yield return ("TurnEquality", TurnEquality.ToString("P1"));
        yield return ("Thrusts", m_thrustTicks.ToString());
    }

    private double TurnEquality
    {
        get
        {
            var totalTurns = m_leftTurns + m_rightTurns;
            if (totalTurns == 0)
                return 0.0; // No turns - no bonus.
            return 1.0 - (double)Math.Abs(m_leftTurns - m_rightTurns) / totalTurns;
        }
    }

    public override bool IsGameOver => m_ticksSinceScore >= 200_000 || Ship.Shield <= 0.02;
    public Ship Ship { get; private set; }
    public List<Asteroid> Asteroids { get; } = [];
    public List<Bullet> Bullets { get; } = [];

    private double HitRatio => m_bulletsFired == 0 ? 0.0 : (double)Score / m_bulletsFired;

    public Game(int arenaWidth, int arenaHeight, Brain brain) : base(arenaWidth, arenaHeight, brain)
    {
    }

    public override Game ResetGame()
    {
        Ship = new Ship(ArenaWidth, ArenaHeight);
        Score = 0;
        m_gameState = new GameState(Ship, Asteroids, ArenaWidth, ArenaHeight);

        Asteroids.Clear();
        Bullets.Clear();
        m_bulletsFired = 0;
        m_gameTicks = 0;
        m_leftTurns = 0;
        m_rightTurns = 0;
        m_thrustTicks = 0;
        m_ticksSinceScore = 0;

        EnsureMinimumAsteroidCount();
        
        return this;
    }

    private void EnsureMinimumAsteroidCount()
    {
        while (Asteroids.FastSum(o => o.SizeMetric) < 12)
        {
            var pos = new Vector2(GameRand.NextFloat() * ArenaWidth, GameRand.NextFloat() * ArenaHeight);
            if (Vector2.Distance(pos, Ship.Position) < 25)
                continue; // Too close to the ship.

            Asteroids.Add(new Asteroid(pos, 0.1f, GameRand, ArenaWidth, ArenaHeight));
        }
    }

    public override void Tick()
    {
        if (IsGameOver)
            return;

        m_gameTicks++;
        m_ticksSinceScore++;

        // Spawn asteroids.
        EnsureMinimumAsteroidCount();

        // Move ship.
        Ship.Move();
        if (Ship.IsThrusting)
            m_thrustTicks++;

        if (Ship.Turning == Ship.Turn.Left)
            m_leftTurns++;
        else if (Ship.Turning == Ship.Turn.Right)
            m_rightTurns++;

        // Spawn new bullets.
        if (Ship.IsShooting && Bullets.Count < Ship.MaxBullets)
        {
            Bullets.Add(new Bullet(Ship.Position, Ship.Theta, ArenaWidth, ArenaHeight));
            m_bulletsFired++;
        }

        // Move the asteroids.
        foreach (var asteroid in Asteroids)
            asteroid.Move();

        // Move the bullets.
        foreach (var bullet in Bullets)
            bullet.Move();

        // Remove expired bullets.
        Bullets.RemoveAll(o => o.IsExpired);

        // Check for bullet/asteroid collisions.
        if (Bullets.Count > 0)
        {
            var bulletsToRemove = new List<Bullet>();
            for (var index = 0; index < Bullets.Count; index++)
            {
                var bullet = Bullets[index];
                Asteroid hitAsteroid = null;
                // ReSharper disable once ForCanBeConvertedToForeach
                // ReSharper disable once LoopCanBeConvertedToQuery
                for (var i = 0; i < Asteroids.Count; i++)
                {
                    if (Asteroids[i].IsInvulnerable || !Asteroids[i].Contains(bullet.Position))
                        continue;
                    hitAsteroid = Asteroids[i];
                    break;
                }

                if (hitAsteroid == null)
                    continue; // Bullet not hitting anything.

                // Bullet hit an asteroid.
                bulletsToRemove.Add(bullet);
                hitAsteroid.Explode(Asteroids);
                Score++;
                m_ticksSinceScore = 0;
            }

            for (var i = 0; i < bulletsToRemove.Count; i++)
                Bullets.Remove(bulletsToRemove[i]);
        }

        // Check for ship/asteroid collisions.
        const float shipRadius = 4.5f;
        for (var i = 0; i < Asteroids.Count; i++)
        {
            var distance = Asteroids[i].DistanceTo(Ship.Position);
            if (distance < Asteroids[i].Radius + shipRadius)
            {
                Ship.Shield = Math.Max(0.0, Ship.Shield - 0.08);
                if (Ship.Shield <= 0.001)
                {
                    // Ship is dead.
                    return;
                }
                break;
            }
        }

        // Apply the AI.
        var moves = ((Brain)Brain).ChooseMoves(m_gameState);
        Ship.IsShooting = moves.IsShooting;
        Ship.Turning = moves.Turn;
        Ship.IsThrusting = moves.IsThrusting;
    }
}
