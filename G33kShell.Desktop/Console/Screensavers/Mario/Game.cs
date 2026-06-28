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
using System.Collections.Generic;
using System.IO;
using G33kShell.Desktop.Console.Screensavers.AI;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace G33kShell.Desktop.Console.Screensavers.Mario;

public class Game : AiGameBase
{
    public readonly record struct EnemySnapshot(double X, double Y, bool IsDead);

    public const int ViewWidth = 256;
    public const int ViewHeight = 240;
    public const double MaxWalkPixelsPerFrame = 1.5;
    public const double MaxRunPixelsPerFrame = 2.5;
    public const double AccelerationPixelsPerFrameSquared = 0.055;
    public const double RunAccelerationPixelsPerFrameSquared = 0.075;
    public const double DecelerationPixelsPerFrameSquared = 0.055;
    public const double SkidDecelerationPixelsPerFrameSquared = 0.125;
    public const double GravityPixelsPerFrameSquared = 0.25;
    public const double MaxFallPixelsPerFrame = 8.0;
    public const double WalkingJumpVelocityPixelsPerFrame = -5.5;
    public const double RunningJumpVelocityPixelsPerFrame = -6.0;
    public const int MarioCollisionWidth = 12;
    public const int MarioCollisionHeight = 16;
    public const int EnemyCollisionWidth = 16;
    public const int EnemyCollisionHeight = 16;
    public const double EnemyWalkPixelsPerFrame = 0.55;
    public const int FlagPoleX = 3175;
    private const int FlagPoleRightX = 3176;
    private const int FlagPoleTopY = 40;
    private const int FlagPoleBottomY = 208;
    private const int CastleDoorX = 3264;

    private const int MaxTicks = 5400;
    private const int MaxTicksWithoutProgress = 180;
    private const int DegenerateWallStallTicks = 45;
    private const int MaxTicksSinceJumpBeforePenalty = 120;
    private const int JumpCooldownTicks = 6;
    private const double JumpPressThreshold = 0.35;
    private const int EnemyCount = 5;
    private readonly bool m_useTrainingTimeouts;
    private readonly bool m_enableEnemies;
    private readonly double m_showRewardMultiplier;
    private readonly GameState m_gameState;
    private readonly List<Enemy> m_enemies = [];
    private int m_ticksWithoutProgress;
    private int m_wallStallTicks;
    private int m_groundedTicks;
    private int m_questionBlockHits;
    private int m_enemyStomps;
    private double m_lastStyleJumpX;
    private double m_flagGrabVelocityY;
    private double m_flagGrabHeightScore;
    private bool m_isJumpHeld;
    private readonly HashSet<(int X, int Y)> m_scoredBlockHitTiles = [];
    private readonly HashSet<(int X, int Y)> m_brokenBlockTiles = [];
    private readonly HashSet<(int X, int Y)> m_usedQuestionBlocks = [];
    private bool m_fell;
    private bool m_killedByEnemy;
    private EndSequenceState m_endSequenceState;

    private static LevelData s_level;
    private static bool[][] s_solidMap;
    private static readonly object s_levelLock = new object();

    public double MarioX { get; private set; }
    public double MarioY { get; private set; }
    public double MarioVelocityX { get; private set; }
    public double MarioVelocityY { get; private set; }
    public double MarioBlockXOffset
    {
        get
        {
            var blockSize = s_level.TileSize * GameState.SensorBlockTileSize;
            var offset = MarioX % blockSize;
            if (offset < 0.0)
                offset += blockSize;

            return offset / blockSize;
        }
    }
    public double MarioBlockYOffset
    {
        get
        {
            var blockSize = s_level.TileSize * GameState.SensorBlockTileSize;
            var offset = MarioY % blockSize;
            if (offset < 0.0)
                offset += blockSize;

            return offset / blockSize;
        }
    }

    public double BestX { get; private set; }
    public int Ticks { get; private set; }
    public int TicksSinceLastJump { get; private set; }
    public int LastBlockHitTick { get; private set; }
    public int LastBlockHitTileX { get; private set; }
    public int LastBlockHitTileY { get; private set; }
    public int LastBlockHitTileId { get; private set; }
    public int LastBlockHitSizeTiles { get; private set; }
    public bool HasReachedFlagPole { get; private set; }
    public bool IsMarioDead { get; private set; }
    public bool IsJumpHeld => m_isJumpHeld;
    
    public IEnumerable<EnemySnapshot> Enemies => GetEnemySnapshots();
    public bool IsGrounded => Collides(MarioX, MarioY + 1, MarioCollisionWidth, MarioCollisionHeight);

    public override bool IsGameOver =>
        m_endSequenceState == EndSequenceState.Complete ||
        m_fell ||
        (m_useTrainingTimeouts && HasReachedFlagPole) ||
        (m_useTrainingTimeouts && (Ticks >= MaxTicks || m_ticksWithoutProgress >= MaxTicksWithoutProgress)) ||
        (!m_useTrainingTimeouts && m_fell);

    public override double Rating
    {
        get
        {
            var distance = BestX;
            var progressTicks = Math.Max(1, Ticks - m_ticksWithoutProgress);
            var distancePerTick = Math.Max(0.0, BestX - 24.0) / progressTicks;
            var failedRunPenalty = m_killedByEnemy ? 25000.0 : IsMarioDead ? 22000.0 : m_fell ? 20000.0 : 0.0;
            var stuckPenalty = m_ticksWithoutProgress >= MaxTicksWithoutProgress ? 12000.0 : 0.0;
            var wallStallPenalty = m_wallStallTicks >= DegenerateWallStallTicks ? m_wallStallTicks * 90.0 : 0.0;
            return Math.Max(0.0,
                distance * 100.0 +
                distancePerTick * 4500.0 +
                GetScore() * m_showRewardMultiplier -
                failedRunPenalty -
                stuckPenalty -
                wallStallPenalty);
        }
    }

    public override double DegeneracyScore
    {
        get
        {
            if (m_killedByEnemy)
                return 1.0;
            if (IsMarioDead || m_fell)
                return 0.98;
            if (m_ticksWithoutProgress >= MaxTicksWithoutProgress)
                return 0.98;
            if (m_wallStallTicks >= DegenerateWallStallTicks)
                return Math.Min(0.95, 0.65 + (m_wallStallTicks - DegenerateWallStallTicks) / 180.0 * 0.35);

            return 0.0;
        }
    }

    public override string DegeneracyReason
    {
        get
        {
            if (m_killedByEnemy)
                return "enemy";
            if (IsMarioDead)
                return "dead";
            if (m_fell)
                return "fell";
            if (m_ticksWithoutProgress >= MaxTicksWithoutProgress)
                return "stuck";
            if (m_wallStallTicks >= DegenerateWallStallTicks)
                return "wall";

            return string.Empty;
        }
    }

    public override (string Name, double Value, string Format)? BestObservedMetric =>
        ("X", BestX, "0");

    private double GetFlagHeightScore() =>
        HasReachedFlagPole ? m_flagGrabHeightScore : 0.0;

    private double GetFlagStyleScore()
    {
        if (!HasReachedFlagPole)
            return 0.0;

        var height = GetFlagHeightScore();
        var highGrabBonus = Math.Pow(Math.Max(0.0, height - 120.0) / 96.0, 2.0) * 18000.0;
        var risingBonus = Math.Max(0.0, -m_flagGrabVelocityY) * 4500.0;
        return highGrabBonus + risingBonus;
    }

    private double GetScore() =>
        (HasReachedFlagPole ? 25000.0 : 0.0) +
        m_questionBlockHits * 500.0 +
        m_enemyStomps * 1000.0 +
        GetFlagHeightScore() * 320.0 +
        GetFlagStyleScore();

    public Game(int arenaWidth, int arenaHeight, Brain brain, bool useTrainingTimeouts = false, bool enableEnemies = false, double showRewardMultiplier = 1.0) : base(arenaWidth, arenaHeight, brain)
    {
        m_useTrainingTimeouts = useTrainingTimeouts;
        m_enableEnemies = enableEnemies;
        m_showRewardMultiplier = showRewardMultiplier;
        m_gameState = new GameState(this);
        EnsureLevelLoaded();
    }

    public override AiGameBase ResetGame()
    {
        Brain.ResetTemporalState();
        MarioX = 24;
        MarioY = FindGroundY((int)MarioX + MarioCollisionWidth / 2) - MarioCollisionHeight;
        MarioVelocityX = 0;
        MarioVelocityY = 0;
        BestX = MarioX;
        Ticks = 0;
        TicksSinceLastJump = MaxTicksSinceJumpBeforePenalty;
        m_ticksWithoutProgress = 0;
        m_wallStallTicks = 0;
        m_groundedTicks = 0;
        m_questionBlockHits = 0;
        m_enemyStomps = 0;
        m_lastStyleJumpX = MarioX;
        m_flagGrabVelocityY = 0.0;
        m_flagGrabHeightScore = 0.0;
        m_isJumpHeld = false;
        m_scoredBlockHitTiles.Clear();
        m_brokenBlockTiles.Clear();
        m_usedQuestionBlocks.Clear();
        m_fell = false;
        m_killedByEnemy = false;
        HasReachedFlagPole = false;
        IsMarioDead = false;
        m_endSequenceState = EndSequenceState.Playing;
        ResetEnemies();
        LastBlockHitTick = 0;
        LastBlockHitTileX = 0;
        LastBlockHitTileY = 0;
        LastBlockHitTileId = 0;
        LastBlockHitSizeTiles = 0;
        return this;
    }

    public override void Tick()
    {
        if (IsGameOver)
            return;

        Ticks++;
        TicksSinceLastJump++;
        if (IsMarioDead)
        {
            MarioVelocityY = Math.Min(MaxFallPixelsPerFrame, MarioVelocityY + GravityPixelsPerFrameSquared);
            MarioY += MarioVelocityY;
            UpdateEnemies();
            if (MarioY > ViewHeight + 32)
                m_fell = true;
            return;
        }

        if (!m_useTrainingTimeouts && m_ticksWithoutProgress >= MaxTicksWithoutProgress * 2)
        {
            KillMario();
            return;
        }

        if (m_endSequenceState != EndSequenceState.Playing)
        {
            UpdateEndSequence();
            UpdateEnemies();
            return;
        }

        var previousMarioY = MarioY;
        var grounded = IsGrounded;
        if (grounded)
            m_groundedTicks++;
        var move = ((Brain)Brain).ChooseMove(m_gameState);
        var wantsJump = move.JumpSignal > JumpPressThreshold;
        m_isJumpHeld = wantsJump;
        var isUnneededEarlyJump = wantsJump && IsUnneededEarlyJump();
        if (grounded && wantsJump && !isUnneededEarlyJump && TicksSinceLastJump >= JumpCooldownTicks)
        {
            var ticksSinceLastJump = TicksSinceLastJump;
            MarioVelocityY = move.Run || MarioVelocityX > MaxWalkPixelsPerFrame
                ? RunningJumpVelocityPixelsPerFrame
                : WalkingJumpVelocityPixelsPerFrame;
            TicksSinceLastJump = 0;
            if (ticksSinceLastJump >= 42 && MarioVelocityX > MaxWalkPixelsPerFrame && MarioX - m_lastStyleJumpX >= 96.0)
                m_lastStyleJumpX = MarioX;

            grounded = false;
        }

        UpdateHorizontalVelocity(move);
        var nextX = MarioX + MarioVelocityX;
        if (Collides(nextX, MarioY, MarioCollisionWidth, MarioCollisionHeight))
        {
            MarioVelocityX = 0;
            if (grounded)
                m_wallStallTicks++;
        }
        else
        {
            MarioX = nextX;
            m_wallStallTicks = 0;
        }

        if (grounded)
        {
            MarioVelocityY = 0;
            MarioY = Math.Floor((MarioY + MarioCollisionHeight + 1) / s_level.TileSize) * s_level.TileSize - MarioCollisionHeight;
        }
        else
        {
            MarioVelocityY = Math.Min(MaxFallPixelsPerFrame, MarioVelocityY + GravityPixelsPerFrameSquared);
            ApplyVerticalMotion(MarioVelocityY);
        }

        if (MarioX > BestX + 0.25)
        {
            BestX = MarioX;
            m_ticksWithoutProgress = 0;
        }
        else
        {
            m_ticksWithoutProgress++;
        }

        if (IntersectsFlagPole())
            StartEndSequence();

        UpdateEnemies();
        HandleEnemyCollisions(previousMarioY);

        if (MarioY > ViewHeight + 32)
            m_fell = true;
    }

    public double GetBlockSensorValue(int blockDx, int blockRowFromBottom)
    {
        var blockSize = s_level.TileSize * GameState.SensorBlockTileSize;
        var blockX = (int)Math.Floor(MarioX / blockSize) + blockDx;
        var bottomBlockY = (ViewHeight - 1) / blockSize;
        var blockY = bottomBlockY - blockRowFromBottom;
        var left = blockX * blockSize;
        var top = blockY * blockSize;

        var marioCenterX = MarioX + MarioCollisionWidth * 0.5;
        var marioCenterY = MarioY + MarioCollisionHeight * 0.5;
        if (marioCenterX >= left && marioCenterX < left + blockSize &&
            marioCenterY >= top && marioCenterY < top + blockSize)
            return 0.25;
        if (IsEnemyInRect(left, top, blockSize, blockSize))
            return -1.0;
        if (IsFlagPoleInRect(left, top, blockSize, blockSize))
            return -0.5;

        var hasSolid = false;
        for (var y = 0; y < GameState.SensorBlockTileSize; y++)
        {
            for (var x = 0; x < GameState.SensorBlockTileSize; x++)
            {
                var tileX = blockX * GameState.SensorBlockTileSize + x;
                var tileY = blockY * GameState.SensorBlockTileSize + y;
                if (IsSolidTile(tileX, tileY))
                    hasSolid = true;
                if (IsUnusedQuestionTile(tileX, tileY))
                    return 0.5;
            }
        }

        return hasSolid ? 1.0 : 0.0;
    }

    public bool TryGetNearestVisibleEnemy(out double dx, out double dy, out double velocityX)
    {
        var blockSize = s_level.TileSize * GameState.SensorBlockTileSize;
        var sensorLeft = ((int)Math.Floor(MarioX / blockSize) + GameState.SensorOriginBlockDx) * blockSize;
        var sensorWidth = GameState.SensorGridSizeX * blockSize;
        var bottomBlockY = (ViewHeight - 1) / blockSize;
        var sensorTop = (bottomBlockY - GameState.SensorBottomBlockRow - GameState.SensorGridSizeY + 1) * blockSize;
        var sensorHeight = GameState.SensorGridSizeY * blockSize;
        var marioCenterX = MarioX + MarioCollisionWidth * 0.5;
        var marioCenterY = MarioY + MarioCollisionHeight * 0.5;
        var nearestDistanceSquared = double.MaxValue;
        Enemy nearestEnemy = null;

        foreach (var enemy in m_enemies)
        {
            if (enemy.IsDead ||
                !Overlaps(sensorLeft, sensorTop, sensorWidth, sensorHeight,
                    enemy.X, enemy.Y, EnemyCollisionWidth, EnemyCollisionHeight))
                continue;

            var enemyDx = enemy.X + EnemyCollisionWidth * 0.5 - marioCenterX;
            var enemyDy = enemy.Y + EnemyCollisionHeight * 0.5 - marioCenterY;
            var distanceSquared = enemyDx * enemyDx + enemyDy * enemyDy;
            if (distanceSquared >= nearestDistanceSquared)
                continue;

            nearestDistanceSquared = distanceSquared;
            nearestEnemy = enemy;
        }

        if (nearestEnemy == null)
        {
            dx = 0.0;
            dy = 0.0;
            velocityX = 0.0;
            return false;
        }

        dx = Math.Clamp(
            (nearestEnemy.X + EnemyCollisionWidth * 0.5 - marioCenterX) / sensorWidth,
            -1.0,
            1.0);
        dy = Math.Clamp(
            (nearestEnemy.Y + EnemyCollisionHeight * 0.5 - marioCenterY) / sensorHeight,
            -1.0,
            1.0);
        velocityX = Math.Clamp(nearestEnemy.VelocityX / EnemyWalkPixelsPerFrame, -1.0, 1.0);
        return true;
    }

    public bool IsBlockBroken(int tileX, int tileY) =>
        m_brokenBlockTiles.Contains((tileX, tileY));

    public bool IsQuestionBlockUsed(int tileX, int tileY) =>
        m_usedQuestionBlocks.Contains((tileX - tileX % 2, tileY - tileY % 2));

    public int CurrentEnemyCount => m_enableEnemies ? EnemyCount : 0;

    private void UpdateHorizontalVelocity(Brain.Move move)
    {
        var direction = move.Right == move.Left ? 0 : move.Right ? 1 : -1;
        var maxSpeed = move.Run ? MaxRunPixelsPerFrame : MaxWalkPixelsPerFrame;
        if (direction == 0)
        {
            MarioVelocityX = ApproachZero(MarioVelocityX, DecelerationPixelsPerFrameSquared);
            return;
        }

        if (Math.Sign(MarioVelocityX) != 0 && Math.Sign(MarioVelocityX) != direction)
            MarioVelocityX += direction * SkidDecelerationPixelsPerFrameSquared;
        else
            MarioVelocityX += direction * (move.Run ? RunAccelerationPixelsPerFrameSquared : AccelerationPixelsPerFrameSquared);

        MarioVelocityX = Math.Clamp(MarioVelocityX, -maxSpeed, maxSpeed);
    }

    private bool IsUnneededEarlyJump()
    {
        if (MarioX >= 112.0)
            return false;
        if (FindDistanceToNextObstacle(128) < 28 || FindDistanceToNextGap(128) < 28)
            return false;
        foreach (var enemy in m_enemies)
        {
            if (enemy.IsDead)
                continue;

            var dx = enemy.X - MarioX;
            if (dx is >= -8.0 and <= 48.0 && Math.Abs(enemy.Y - MarioY) < 32.0)
                return false;
        }

        return true;
    }

    private void ApplyVerticalMotion(double distance)
    {
        const double maxStep = 2.0;
        var remaining = distance;
        while (Math.Abs(remaining) > 0.001)
        {
            var step = Math.Clamp(remaining, -maxStep, maxStep);
            var nextY = MarioY + step;
            if (step < 0 && Collides(MarioX, nextY, MarioCollisionWidth, MarioCollisionHeight))
            {
                RecordHeadHit(nextY);
                MarioVelocityY = 0;
                MarioY = Math.Ceiling(nextY / s_level.TileSize) * s_level.TileSize;
                return;
            }

            if (step > 0 && Collides(MarioX, nextY, MarioCollisionWidth, MarioCollisionHeight))
            {
                var bottom = nextY + MarioCollisionHeight;
                MarioY = Math.Floor(bottom / s_level.TileSize) * s_level.TileSize - MarioCollisionHeight;
                MarioVelocityY = 0;
                return;
            }

            MarioY = nextY;
            remaining -= step;
        }
    }

    private static double ApproachZero(double value, double amount)
    {
        if (value > 0)
            return Math.Max(0, value - amount);
        if (value < 0)
            return Math.Min(0, value + amount);
        return 0;
    }

    public override IEnumerable<(string Name, string Value)> ExtraGameStats()
    {
        yield return ("X", BestX.ToString("F0"));
        yield return ("Ticks", Ticks.ToString());
        yield return ("Grounded", (m_groundedTicks / (double)Math.Max(1, Ticks)).ToString("F3"));
        yield return ("TimeBonus", (HasReachedFlagPole ? Math.Max(0.0, 1.0 - Ticks / (double)MaxTicks) : 0.0).ToString("F3"));
        yield return ("Questions", m_questionBlockHits.ToString());
        yield return ("Stomps", m_enemyStomps.ToString());
        if (HasReachedFlagPole)
            yield return ("Flag", GetFlagHeightScore().ToString("F0"));
        yield return ("Finished", HasReachedFlagPole ? "1" : "0");
        if (m_ticksWithoutProgress > 0)
            yield return ("Stuck", m_ticksWithoutProgress.ToString());
        if (m_wallStallTicks > 0)
            yield return ("Wall", m_wallStallTicks.ToString());
        if (m_fell)
            yield return ("Fell", "1");
        if (IsMarioDead)
            yield return ("Died", "1");
        if (m_killedByEnemy)
            yield return ("EnemyDeath", "1");
    }

    private IEnumerable<EnemySnapshot> GetEnemySnapshots()
    {
        foreach (var enemy in m_enemies)
            yield return new EnemySnapshot(enemy.X, enemy.Y, enemy.IsDead);
    }

    private bool IsEnemyInRect(int left, int top, int width, int height)
    {
        foreach (var enemy in m_enemies)
        {
            if (enemy.IsDead)
                continue;

            if (Overlaps(left, top, width, height, enemy.X, enemy.Y, EnemyCollisionWidth, EnemyCollisionHeight))
                return true;
        }

        return false;
    }

    private static bool IsFlagPoleInRect(int left, int top, int width, int height)
    {
        var right = left + width - 1;
        var bottom = top + height - 1;

        return right >= FlagPoleX &&
               left <= FlagPoleRightX &&
               bottom >= FlagPoleTopY &&
               top <= FlagPoleBottomY;
    }

    private bool IsUnusedQuestionTile(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= s_level.WidthTiles || tileY < 0 || tileY >= s_level.HeightTiles)
            return false;

        var tileId = s_level.Map[tileY][tileX];
        if (!IsQuestionTile(tileId))
            return false;

        var blockX = tileX - tileX % 2;
        var blockY = tileY - tileY % 2;
        return !m_usedQuestionBlocks.Contains((blockX, blockY));
    }

    private bool IsSolidTile(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= s_level.WidthTiles || tileY < 0 || tileY >= s_level.HeightTiles)
            return false;
        if (m_brokenBlockTiles.Contains((tileX, tileY)))
            return false;

        return s_solidMap[tileY][tileX];
    }

    private void ResetEnemies()
    {
        m_enemies.Clear();
        if (!m_enableEnemies)
            return;

        for (var i = 0; i < EnemyCount; i++)
        {
            var x = GameRand.Next(280, FlagPoleX - 240);
            var y = GameRand.Next(-160, -24);
            m_enemies.Add(new Enemy(x, y, -EnemyWalkPixelsPerFrame));
        }
    }

    private void UpdateEnemies()
    {
        for (var i = m_enemies.Count - 1; i >= 0; i--)
        {
            var enemy = m_enemies[i];
            if (enemy.IsDead)
            {
                enemy.VelocityY = Math.Min(MaxFallPixelsPerFrame, enemy.VelocityY + GravityPixelsPerFrameSquared);
                enemy.Y += enemy.VelocityY;
                if (enemy.Y > ViewHeight + 48)
                    m_enemies.RemoveAt(i);
                continue;
            }

            enemy.VelocityY = Math.Min(MaxFallPixelsPerFrame, enemy.VelocityY + GravityPixelsPerFrameSquared);
            MoveEnemyVertically(enemy);
            var nextX = enemy.X + enemy.VelocityX;
            if (EnemyCollides(nextX, enemy.Y))
            {
                enemy.VelocityX = -enemy.VelocityX;
            }
            else
            {
                enemy.X = nextX;
            }
        }
    }

    private void MoveEnemyVertically(Enemy enemy)
    {
        const double maxStep = 2.0;
        var remaining = enemy.VelocityY;
        while (Math.Abs(remaining) > 0.001)
        {
            var step = Math.Clamp(remaining, -maxStep, maxStep);
            var nextY = enemy.Y + step;
            if (step > 0 && EnemyCollides(enemy.X, nextY))
            {
                var bottom = nextY + EnemyCollisionHeight;
                enemy.Y = Math.Floor(bottom / s_level.TileSize) * s_level.TileSize - EnemyCollisionHeight;
                enemy.VelocityY = 0;
                return;
            }

            if (step < 0 && EnemyCollides(enemy.X, nextY))
            {
                enemy.VelocityY = 0;
                return;
            }

            enemy.Y = nextY;
            remaining -= step;
        }
    }

    private void HandleEnemyCollisions(double previousMarioY)
    {
        if (IsMarioDead || HasReachedFlagPole)
            return;

        var previousMarioBottom = previousMarioY + MarioCollisionHeight;
        List<Enemy> overlappingEnemies = null;
        foreach (var enemy in m_enemies)
        {
            if (enemy.IsDead || !Overlaps(MarioX, MarioY, MarioCollisionWidth, MarioCollisionHeight, enemy.X, enemy.Y, EnemyCollisionWidth, EnemyCollisionHeight))
                continue;

            overlappingEnemies ??= [];
            overlappingEnemies.Add(enemy);
        }

        if (overlappingEnemies == null)
            return;

        var isStomp = false;
        foreach (var enemy in overlappingEnemies)
        {
            var enemyTop = enemy.Y + 3;
            if (MarioVelocityY > 0 && previousMarioBottom <= enemyTop)
            {
                isStomp = true;
                break;
            }
        }

        if (!isStomp)
        {
            m_killedByEnemy = true;
            KillMario();
            return;
        }

        foreach (var enemy in overlappingEnemies)
        {
            enemy.IsDead = true;
            enemy.VelocityX = 0;
            enemy.VelocityY = -4.0;
            m_enemyStomps++;
        }

        MarioVelocityY = -3.7;
        TicksSinceLastJump = 0;
    }

    private void KillMario()
    {
        IsMarioDead = true;
        MarioVelocityX = 0;
        MarioVelocityY = -5.5;
    }

    private bool EnemyCollides(double x, double y) =>
        IsSolidPixel((int)Math.Floor(x), (int)Math.Floor(y)) ||
        IsSolidPixel((int)Math.Floor(x + EnemyCollisionWidth - 1), (int)Math.Floor(y)) ||
        IsSolidPixel((int)Math.Floor(x), (int)Math.Floor(y + EnemyCollisionHeight - 1)) ||
        IsSolidPixel((int)Math.Floor(x + EnemyCollisionWidth - 1), (int)Math.Floor(y + EnemyCollisionHeight - 1)) ||
        IsSolidPixel((int)Math.Floor(x + EnemyCollisionWidth / 2.0), (int)Math.Floor(y + EnemyCollisionHeight - 1));

    private static bool Overlaps(double ax, double ay, int aw, int ah, double bx, double by, int bw, int bh) =>
        ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;

    private void RecordHeadHit(double nextY)
    {
        if (!TryGetHeadHitTile(MarioX, nextY, MarioCollisionWidth, out var tileX, out var tileY, out var tileId))
            return;

        var blockX = tileX - tileX % 2;
        var blockY = tileY - tileY % 2;
        var isQuestionBlock = IsQuestionTile(tileId);
        KillEnemiesOnBlock(blockX, blockY);
        if (m_scoredBlockHitTiles.Add((blockX, blockY)))
        {
            if (isQuestionBlock)
                m_questionBlockHits++;
        }

        if (isQuestionBlock)
        {
            m_usedQuestionBlocks.Add((blockX, blockY));
            LastBlockHitTick = 0;
            return;
        }

        for (var y = blockY; y < blockY + 2; y++)
        {
            for (var x = blockX; x < blockX + 2; x++)
                m_brokenBlockTiles.Add((x, y));
        }

        LastBlockHitTick = Ticks;
        LastBlockHitTileX = blockX;
        LastBlockHitTileY = blockY;
        LastBlockHitTileId = tileId;
        LastBlockHitSizeTiles = 2;
    }

    private void KillEnemiesOnBlock(int blockX, int blockY)
    {
        var blockLeft = blockX * s_level.TileSize;
        var blockTop = blockY * s_level.TileSize;
        var blockWidth = s_level.TileSize * 2;
        foreach (var enemy in m_enemies)
        {
            if (enemy.IsDead)
                continue;

            var enemyBottom = enemy.Y + EnemyCollisionHeight;
            var isStandingOnBlock = enemyBottom >= blockTop - 2.0 && enemyBottom <= blockTop + 6.0;
            var overlapsBlockX = enemy.X < blockLeft + blockWidth && enemy.X + EnemyCollisionWidth > blockLeft;
            if (!isStandingOnBlock || !overlapsBlockX)
                continue;

            enemy.IsDead = true;
            enemy.VelocityX = 0;
            enemy.VelocityY = -4.0;
            m_enemyStomps++;
        }
    }

    private bool TryGetHeadHitTile(double x, double y, int width, out int tileX, out int tileY, out int tileId)
    {
        var left = (int)Math.Floor(x);
        var right = (int)Math.Floor(x + width - 1);
        var top = (int)Math.Floor(y);
        var samples = new[] { left, (left + right) / 2, right };
        foreach (var sampleX in samples)
        {
            if (!IsSolidPixel(sampleX, top))
                continue;

            tileX = sampleX / s_level.TileSize;
            tileY = top / s_level.TileSize;
            tileId = s_level.Map[tileY][tileX];
            return true;
        }

        tileX = 0;
        tileY = 0;
        tileId = 0;
        return false;
    }

    private static bool IsQuestionTile(int tileId) =>
        tileId is 19 or 20 or 24 or 25;

    private int FindGroundY(int x)
    {
        for (var y = 0; y < s_level.HeightPixels; y++)
        {
            if (IsSolidPixel(x, y))
                return y;
        }

        return s_level.HeightPixels;
    }

    private double FindDistanceToNextObstacle(int maxDistance)
    {
        var footY = (int)(MarioY + MarioCollisionHeight);
        for (var dx = 1; dx <= maxDistance; dx++)
        {
            var x = (int)(MarioX + MarioCollisionWidth + dx);
            if (IsSolidPixel(x, footY - 4) || IsSolidPixel(x, footY - 12))
                return dx;
        }

        return maxDistance;
    }

    private double FindDistanceToNextGap(int maxDistance)
    {
        var footY = (int)(MarioY + MarioCollisionHeight);
        for (var dx = 1; dx <= maxDistance; dx++)
        {
            var x = (int)(MarioX + MarioCollisionWidth + dx);
            if (!IsSolidPixel(x, footY + 1))
                return dx;
        }

        return maxDistance;
    }

    private bool Collides(double x, double y, int width, int height)
    {
        var left = (int)Math.Floor(x);
        var right = (int)Math.Floor(x + width - 1);
        var top = (int)Math.Floor(y);
        var bottom = (int)Math.Floor(y + height - 1);

        return IsSolidPixel(left, top) ||
               IsSolidPixel(right, top) ||
               IsSolidPixel(left, bottom) ||
               IsSolidPixel(right, bottom) ||
               IsSolidPixel((left + right) / 2, bottom);
    }

    private bool IntersectsFlagPole()
    {
        var visualLeft = MarioX;
        var visualRight = MarioX + 16;
        var visualTop = MarioY;
        var visualBottom = MarioY + MarioCollisionHeight;
        return visualRight >= FlagPoleX &&
               visualLeft <= FlagPoleRightX &&
               visualBottom >= FlagPoleTopY &&
               visualTop <= FlagPoleBottomY;
    }

    private void StartEndSequence()
    {
        HasReachedFlagPole = true;
        m_flagGrabVelocityY = MarioVelocityY;
        m_flagGrabHeightScore = Math.Max(0.0, ViewHeight - (MarioY + MarioCollisionHeight));
        if (m_useTrainingTimeouts)
            return;

        m_endSequenceState = EndSequenceState.SlidingFlag;
        MarioX = FlagPoleX - MarioCollisionWidth;
        MarioVelocityX = 0;
        MarioVelocityY = 1.6;
    }

    private void UpdateEndSequence()
    {
        if (m_endSequenceState == EndSequenceState.SlidingFlag)
        {
            MarioVelocityX = 0;
            MarioVelocityY = 1.8;
            MarioY = Math.Min(FlagPoleBottomY - MarioCollisionHeight, MarioY + MarioVelocityY);
            if (MarioY >= FlagPoleBottomY - MarioCollisionHeight)
            {
                m_endSequenceState = EndSequenceState.WalkingToCastle;
                MarioVelocityX = 1.4;
                MarioVelocityY = 0;
            }

            return;
        }

        if (m_endSequenceState != EndSequenceState.WalkingToCastle)
            return;

        MarioVelocityX = 1.4;
        MarioVelocityY = 0;
        MarioX += MarioVelocityX;
        if (MarioX > BestX)
            BestX = MarioX;
        if (MarioX >= CastleDoorX)
            m_endSequenceState = EndSequenceState.Complete;
    }

    private bool IsSolidPixel(int x, int y)
    {
        if (x < 0 || x >= s_level.WidthPixels || y < 0 || y >= s_level.HeightPixels)
            return false;

        var tileX = x / s_level.TileSize;
        var tileY = y / s_level.TileSize;
        if (m_brokenBlockTiles.Contains((tileX, tileY)))
            return false;

        return s_solidMap[tileY][tileX];
    }

    private static void EnsureLevelLoaded()
    {
        if (s_level != null)
            return;

        lock (s_levelLock)
        {
            if (s_level != null)
                return;

            var jsonPath = Path.Combine(AppContext.BaseDirectory, "Assets", "PixelLevels", "world_1_1.json");
            var level = JsonConvert.DeserializeObject<LevelData>(File.ReadAllText(jsonPath));
            var tileKinds = new string[level.Tiles.Length];
            foreach (var tile in level.Tiles)
                tileKinds[tile.Id] = tile.Kind;

            var solidMap = new bool[level.HeightTiles][];
            for (var y = 0; y < level.HeightTiles; y++)
            {
                solidMap[y] = new bool[level.WidthTiles];
                for (var x = 0; x < level.WidthTiles; x++)
                    solidMap[y][x] = tileKinds[level.Map[y][x]] == "orange";
            }

            for (var x = 0; x < level.WidthTiles; x++)
            {
                int? pipeTopY = null;
                for (var y = 0; y < level.HeightTiles; y++)
                {
                    if (level.Map[y][x] is >= 31 and <= 39)
                    {
                        pipeTopY = y;
                        break;
                    }
                }

                if (!pipeTopY.HasValue)
                    continue;

                for (var y = pipeTopY.Value; y < level.HeightTiles; y++)
                {
                    if (tileKinds[level.Map[y][x]] == "green")
                        solidMap[y][x] = true;
                }
            }

            s_solidMap = solidMap;
            s_level = level;
        }
    }

    private class LevelData
    {
        public int TileSize { get; [UsedImplicitly] set; }
        public int WidthPixels { get; [UsedImplicitly] set; }
        public int HeightPixels { get; [UsedImplicitly] set; }
        public int WidthTiles { get; [UsedImplicitly] set; }
        public int HeightTiles { get; [UsedImplicitly] set; }
        public TileData[] Tiles { get; [UsedImplicitly] set; }
        public int[][] Map { get; [UsedImplicitly] set; }
    }

    private class TileData
    {
        public int Id { get; [UsedImplicitly] set; }
        public string Kind { get; [UsedImplicitly] set; }
    }

    private sealed class Enemy(double x, double y, double velocityX)
    {
        public double X = x;
        public double Y = y;
        public double VelocityX = velocityX;
        public double VelocityY;
        public bool IsDead;
    }

    private enum EndSequenceState
    {
        Playing,
        SlidingFlag,
        WalkingToCastle,
        Complete
    }
}
