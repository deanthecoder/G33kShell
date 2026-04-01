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
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Asteroids;

/// <summary>
/// Asteroids simulation used both for live play and for offline training evaluation.
/// </summary>
/// <remarks>
/// The training objective deliberately rewards the real task more than visual style:
/// score gained, surviving longer, staying shielded, and converting bullets into hits.
/// Small motion terms are still present to discourage pure camping, but they are kept
/// secondary so the policy is pushed toward actually clearing asteroids rather than merely
/// looking active.
/// </remarks>
[DebuggerDisplay("Rating = {Rating}, Score = {Score}")]
public class Game : AiGameBase
{
    public readonly struct TrainingProfile
    {
        public static TrainingProfile Default => new(12, 0.10f, 0.0, 1.0, "Full");

        public TrainingProfile(int desiredAsteroidMetric, float asteroidSpeedMultiplier, double aimedSpawnChance, double collisionDamageMultiplier, string stageLabel)
        {
            DesiredAsteroidMetric = desiredAsteroidMetric;
            AsteroidSpeedMultiplier = asteroidSpeedMultiplier;
            AimedSpawnChance = aimedSpawnChance;
            CollisionDamageMultiplier = collisionDamageMultiplier;
            StageLabel = stageLabel;
        }

        public int DesiredAsteroidMetric { get; }
        public float AsteroidSpeedMultiplier { get; }
        public double AimedSpawnChance { get; }
        public double CollisionDamageMultiplier { get; }
        public string StageLabel { get; }
    }

    private const int FireCooldownTicks = 6;
    private const int ShipCollisionCooldownTicks = 12;
    private readonly bool m_enableVisualEffects;
    private readonly TrainingProfile m_trainingProfile;
    private int m_bulletsFired;
    private double m_cumulativeSpeed;
    private int m_gameTicks;
    private int m_lastFireTick = -FireCooldownTicks;
    private int m_lastShipHitTick = -ShipCollisionCooldownTicks;
    private int m_leftTurns;
    private bool m_previousShootIntent;
    private int m_rightTurns;
    private GameState m_gameState;
    private int m_sameTurnStreak;
    private int m_sameTurnStreakPeak;
    private int m_stationaryStreak;
    private int m_stationaryStreakPeak;
    private int m_stationaryTicks;
    private int m_ticksSinceScore;

    /// <summary>
    /// Score used for public display.
    /// </summary>
    public int Score { get; private set; }

    /// <summary>
    /// Gets the evolutionary fitness used to rank candidate Asteroids brains.
    /// </summary>
    public override double Rating
    {
        get
        {
            if (Score == 0 && m_gameTicks < 300)
                return 0.0;
            if (m_gameTicks > 600 && m_sameTurnStreakPeak > 240 && TurnEquality < 0.10)
                return 0.0;
            if (m_gameTicks > 600 && (double)(m_leftTurns + m_rightTurns) / m_gameTicks > 0.92 && TurnEquality < 0.10)
                return 0.0;
            if (m_gameTicks > 900 && GetStationaryRatio() > 0.92 && GetAverageSpeed() < 0.012)
                return 0.0;

            var survivalScore = Math.Min(m_gameTicks, 45 * 60) * 0.05;
            var combatScore = Score * 350.0;
            var shieldScore = Ship.Shield * 100.0;
            var hitRatioScore = HitRatio * 120.0;
            var averageSpeed = GetAverageSpeed();
            var stationaryRatio = GetStationaryRatio();
            var totalTurns = m_leftTurns + m_rightTurns;
            var turnRatio = m_gameTicks == 0 ? 0.0 : (double)totalTurns / m_gameTicks;
            var mobilityScore = Math.Min(averageSpeed / 0.04, 1.0) * 250.0;
            var shotWastePenalty = Math.Max(0, m_bulletsFired - Score * 3) * 1.0;
            var campingPenalty = stationaryRatio * 200.0;
            var sustainedCampingPenalty = Math.Max(0.0, m_stationaryStreakPeak - 90) * 0.35;
            var excessiveTurningPenalty = Math.Max(0.0, turnRatio - 0.50) * 950.0;
            var imbalancePenalty = totalTurns < 60 ? 0.0 : (1.0 - TurnEquality) * 900.0;
            var spinLockPenalty = Math.Max(0.0, m_sameTurnStreakPeak - 80) * 1.8;
            var earlyDeathPenalty = Ship.Shield <= 0.02 ? 80.0 : 0.0;

            return Math.Max(0.0, survivalScore + combatScore + shieldScore + hitRatioScore + mobilityScore - shotWastePenalty - campingPenalty - sustainedCampingPenalty - excessiveTurningPenalty - imbalancePenalty - spinLockPenalty - earlyDeathPenalty);
        }
    }

    public override double DegeneracyScore
    {
        get
        {
            var turnRatio = m_gameTicks == 0 ? 0.0 : (double)(m_leftTurns + m_rightTurns) / m_gameTicks;
            var equalityPenalty = m_leftTurns + m_rightTurns < 60 ? 0.0 : 1.0 - TurnEquality;
            var spinPenalty = Math.Min(1.0, Math.Max(0.0, m_sameTurnStreakPeak - 60) / 180.0);
            var turnPenalty = Math.Min(1.0, Math.Max(0.0, turnRatio - 0.55) / 0.35);
            var stationaryRatio = GetStationaryRatio();
            var stationaryPenalty = Math.Min(1.0, Math.Max(0.0, stationaryRatio - 0.55) / 0.25);
            var campPenalty = Math.Min(1.0, Math.Max(0.0, m_stationaryStreakPeak - 240) / 720.0);
            var lowSpeedPenalty = Math.Min(1.0, Math.Max(0.0, 0.02 - GetAverageSpeed()) / 0.02);
            return Math.Max(
                Math.Max(spinPenalty, Math.Max(turnPenalty, equalityPenalty)),
                Math.Max(stationaryPenalty, Math.Max(campPenalty, lowSpeedPenalty)));
        }
    }

    public override string DegeneracyReason =>
        DegeneracyScore < 0.60
            ? string.Empty
            : GetStationaryRatio() > 0.85 && GetAverageSpeed() < 0.012
                ? "camping"
            : m_sameTurnStreakPeak >= 120
                ? "spin-lock"
                : TurnEquality < 0.2
                    ? "one-sided turning"
                    : "over-turning";
    
    public override IEnumerable<(string Name, string Value)> ExtraGameStats()
    {
        yield return ("Stage", m_trainingProfile.StageLabel);
        yield return ("Score", Score.ToString());
        yield return ("HitRatio", HitRatio.ToString("P1"));
        yield return ("Ticks", m_gameTicks.ToString());
        yield return ("TurnEquality", TurnEquality.ToString("P1"));
        yield return ("TurnRatio", (m_gameTicks == 0 ? 0.0 : (double)(m_leftTurns + m_rightTurns) / m_gameTicks).ToString("P1"));
        yield return ("Stationary", (m_gameTicks == 0 ? 0.0 : (double)m_stationaryTicks / m_gameTicks).ToString("P1"));
        yield return ("SpinPeak", m_sameTurnStreakPeak.ToString());
    }

    private double TurnEquality =>
        m_leftTurns + m_rightTurns == 0
            ? 0.0
            : 1.0 - (double)Math.Abs(m_leftTurns - m_rightTurns) / (m_leftTurns + m_rightTurns);

    public override bool IsGameOver => m_ticksSinceScore >= 20 * 60 || m_gameTicks >= 60 * 60 || Ship.Shield <= 0.02;
    public Ship Ship { get; private set; }
    public List<Asteroid> Asteroids { get; } = [];
    public List<Bullet> Bullets { get; } = [];
    public List<ExhaustParticle> ExhaustParticles { get; } = [];

    private double HitRatio => m_bulletsFired == 0 ? 0.0 : (double)Score / m_bulletsFired;

    private double GetAverageSpeed() =>
        m_gameTicks == 0 ? 0.0 : m_cumulativeSpeed / m_gameTicks;

    private double GetStationaryRatio() =>
        m_gameTicks == 0 ? 0.0 : (double)m_stationaryTicks / m_gameTicks;

    public Game(int arenaWidth, int arenaHeight, Brain brain, TrainingProfile? trainingProfile = null, bool enableVisualEffects = false) : base(arenaWidth, arenaHeight, brain)
    {
        m_trainingProfile = trainingProfile ?? TrainingProfile.Default;
        m_enableVisualEffects = enableVisualEffects;
    }

    public override Game ResetGame()
    {
        Brain.ResetTemporalState();
        Ship = new Ship(ArenaWidth, ArenaHeight);
        Score = 0;

        Asteroids.Clear();
        Bullets.Clear();
        ExhaustParticles.Clear();
        m_bulletsFired = 0;
        m_cumulativeSpeed = 0.0;
        m_gameTicks = 0;
        m_lastFireTick = -FireCooldownTicks;
        m_lastShipHitTick = -ShipCollisionCooldownTicks;
        m_leftTurns = 0;
        m_previousShootIntent = false;
        m_rightTurns = 0;
        m_sameTurnStreak = 0;
        m_sameTurnStreakPeak = 0;
        m_stationaryStreak = 0;
        m_stationaryStreakPeak = 0;
        m_stationaryTicks = 0;
        m_ticksSinceScore = 0;
        m_gameState = new GameState(Ship, Asteroids, Bullets, () => m_gameTicks - m_lastFireTick >= FireCooldownTicks, ArenaWidth, ArenaHeight);

        EnsureMinimumAsteroidCount(false);
        
        return this;
    }

    private void EnsureMinimumAsteroidCount(bool aimForShip)
    {
        while (Asteroids.FastSum(o => o.SizeMetric) < m_trainingProfile.DesiredAsteroidMetric)
        {
            var pos = new Vector2(GameRand.NextFloat() * ArenaWidth, GameRand.NextFloat() * ArenaHeight);
            if (Vector2.Distance(pos, Ship.Position) < 25)
                continue; // Too close to the ship.

            var asteroid = new Asteroid(pos, m_trainingProfile.AsteroidSpeedMultiplier, GameRand, ArenaWidth, ArenaHeight);
            if (aimForShip || GameRand.NextDouble() < m_trainingProfile.AimedSpawnChance)
                asteroid.AimTowards(Ship.Position);
            Asteroids.Add(asteroid);
        }
    }

    public override void Tick()
    {
        if (IsGameOver)
            return;

        m_gameTicks++;
        m_ticksSinceScore++;

        // Spawn asteroids.
        EnsureMinimumAsteroidCount(false);

        // Choose controls from the current world state before simulating the next frame.
        var moves = ((Brain)Brain).ChooseMoves(m_gameState);
        var canFire = m_gameTicks - m_lastFireTick >= FireCooldownTicks;
        var wantsToShoot = moves.IsShooting;
        Ship.IsShooting = wantsToShoot && !m_previousShootIntent && canFire;
        Ship.Turning = moves.Turn;
        Ship.IsThrusting = moves.IsThrusting;
        m_previousShootIntent = wantsToShoot;

        // Move ship.
        Ship.Move();
        var shipSpeed = Ship.Velocity.Length();
        m_cumulativeSpeed += shipSpeed;
        if (shipSpeed < 0.015f)
        {
            m_stationaryTicks++;
            m_stationaryStreak++;
            if (m_stationaryStreak > m_stationaryStreakPeak)
                m_stationaryStreakPeak = m_stationaryStreak;
        }
        else
        {
            m_stationaryStreak = 0;
        }
        
        if (Ship.IsThrusting && m_enableVisualEffects)
            SpawnExhaustParticles();

        if (Ship.Turning == Ship.Turn.Left)
        {
            m_leftTurns++;
            m_sameTurnStreak = m_sameTurnStreak >= 0 ? m_sameTurnStreak + 1 : 1;
        }
        else if (Ship.Turning == Ship.Turn.Right)
        {
            m_rightTurns++;
            m_sameTurnStreak = m_sameTurnStreak <= 0 ? m_sameTurnStreak - 1 : -1;
        }
        else
        {
            m_sameTurnStreak = 0;
        }

        var absTurnStreak = Math.Abs(m_sameTurnStreak);
        if (absTurnStreak > m_sameTurnStreakPeak)
            m_sameTurnStreakPeak = absTurnStreak;

        // Spawn new bullets.
        if (Ship.IsShooting && Bullets.Count < Ship.MaxBullets)
        {
            Bullets.Add(new Bullet(Ship.Position, Ship.Theta, ArenaWidth, ArenaHeight));
            m_bulletsFired++;
            m_lastFireTick = m_gameTicks;
        }

        // Move the asteroids.
        foreach (var asteroid in Asteroids)
            asteroid.Move();

        // Move the bullets.
        foreach (var bullet in Bullets)
            bullet.Move();

        // Remove expired bullets.
        Bullets.RemoveAll(o => o.IsExpired);

        if (m_enableVisualEffects && ExhaustParticles.Count > 0)
        {
            for (var i = 0; i < ExhaustParticles.Count; i++)
                ExhaustParticles[i].Move();
            ExhaustParticles.RemoveAll(o => o.IsExpired);
        }

        // Check for bullet/asteroid collisions.
        if (Bullets.Count > 0)
        {
            List<Bullet> bulletsToRemove = null;
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
                bulletsToRemove ??= new List<Bullet>();
                bulletsToRemove.Add(bullet);
                hitAsteroid.Explode(Asteroids);
                Score++;
                m_ticksSinceScore = 0;
            }

            if (bulletsToRemove != null)
            {
                for (var i = 0; i < bulletsToRemove.Count; i++)
                    Bullets.Remove(bulletsToRemove[i]);
            }
        }

        // Check for ship/asteroid collisions.
        const float shipRadius = 4.5f;
        for (var i = 0; i < Asteroids.Count; i++)
        {
            var distance = Asteroids[i].DistanceTo(Ship.Position);
            if (distance < Asteroids[i].Radius + shipRadius)
            {
                HandleShipCollision(Asteroids[i], distance, shipRadius);
                break;
            }
        }
    }
    
    private Vector2 WrapDelta(Vector2 delta)
    {
        var halfWidth = ArenaWidth / 2.0f;
        var halfHeight = ArenaHeight / 2.0f;

        if (delta.X > halfWidth)
            delta.X -= ArenaWidth;
        else if (delta.X < -halfWidth)
            delta.X += ArenaWidth;

        if (delta.Y > halfHeight)
            delta.Y -= ArenaHeight;
        else if (delta.Y < -halfHeight)
            delta.Y += ArenaHeight;

        return delta;
    }

    private void HandleShipCollision(Asteroid asteroid, float distance, float shipRadius)
    {
        if (m_gameTicks - m_lastShipHitTick < ShipCollisionCooldownTicks)
            return;

        var damage = 0.08 * m_trainingProfile.CollisionDamageMultiplier;
        if (damage > 0.0)
            Ship.Shield = Math.Max(0.0, Ship.Shield - damage);
        m_lastShipHitTick = m_gameTicks;

        var pushDirection = WrapDelta(Ship.Position - asteroid.Position);
        pushDirection = pushDirection.LengthSquared() <= 0.0001f ? Ship.Theta.ToDirection() : Vector2.Normalize(pushDirection);

        var overlap = Math.Max(0.0f, asteroid.Radius + shipRadius - distance);
        Ship.ApplyImpact(pushDirection, impulse: 0.12f, separationDistance: overlap + 3.5f);
        asteroid.BounceAway(-pushDirection, separationDistance: overlap * 0.6f + 2.0f);
    }

    private void SpawnExhaustParticles()
    {
        var backward = -Ship.Theta.ToDirection();
        var right = new Vector2(backward.Y, -backward.X);
        var tailPosition = Ship.Position + backward * 4.5f;
        var particleCount = GameRand.NextDouble() < 0.4 ? 2 : 1;
        for (var i = 0; i < particleCount; i++)
        {
            var spread = (float)(GameRand.NextDouble() * 2.0 - 1.0);
            var speed = 0.22f + (float)GameRand.NextDouble() * 0.18f;
            var drift = ((float)GameRand.NextDouble() - 0.5f) * 0.05f;
            var velocity = backward * speed + right * (spread * 0.08f) + Ship.Velocity * 0.15f;
            var spawnPosition = tailPosition + right * drift + backward * ((float)GameRand.NextDouble() * 0.7f);
            ExhaustParticles.Add(new ExhaustParticle(spawnPosition, velocity, ArenaWidth, ArenaHeight, lifetimeTicks: 14));
        }
    }
}
