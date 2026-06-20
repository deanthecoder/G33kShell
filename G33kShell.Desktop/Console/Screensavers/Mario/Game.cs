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
    public readonly record struct SensorSample(int X, int Y);
    public readonly record struct EnemySnapshot(double X, double Y, bool IsDead);

    public const int ViewWidth = 256;
    public const int ViewHeight = 240;
    public const double MaxWalkPixelsPerFrame = 1.5;
    public const double MaxRunPixelsPerFrame = 2.5;
    public const double AccelerationPixelsPerFrameSquared = 0.055;
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
    public const int FlagPoleX = 3175;
    private const int FlagPoleRightX = 3176;
    private const int FlagPoleTopY = 40;
    private const int FlagPoleBottomY = 208;
    private const int CastleDoorX = 3264;

    public static readonly SensorSample[] SensorSamples =
    [
        new(12, -4), new(24, -4), new(36, -4), new(52, -4),
        new(12, -12), new(24, -12), new(36, -12), new(52, -12),
        new(12, 1), new(24, 1), new(36, 1), new(52, 1)
    ];

    private const int MaxTicks = 5400;
    private const int MaxTicksWithoutProgress = 180;
    private const int MaxTicksSinceJumpBeforePenalty = 120;
    private const int JumpCooldownTicks = 6;
    private const int InitialEnemyCount = 4;
    private readonly bool m_useTrainingTimeouts;
    private readonly bool m_enableEnemies;
    private readonly GameState m_gameState;
    private readonly List<Enemy> m_enemies = [];
    private int m_ticksWithoutProgress;
    private int m_jumpCount;
    private int m_airTicks;
    private int m_slowTicks;
    private int m_fastTicks;
    private int m_walkSpeedTicks;
    private int m_runSpeedTicks;
    private int m_wallStallTicks;
    private int m_blockHits;
    private int m_questionBlockHits;
    private int m_pogoJumpCount;
    private int m_styleJumpCount;
    private int m_unneededEarlyJumpCount;
    private double m_lastStyleJumpX;
    private readonly HashSet<(int X, int Y)> m_scoredBlockHitTiles = [];
    private readonly HashSet<(int X, int Y)> m_brokenBlockTiles = [];
    private readonly HashSet<(int X, int Y)> m_usedQuestionBlocks = [];
    private int m_enemyCount = InitialEnemyCount;
    private bool m_pendingEnemyIncrease;
    private bool m_fell;
    private EndSequenceState m_endSequenceState;

    private static LevelData s_level;
    private static bool[][] s_solidMap;
    private static readonly object s_levelLock = new object();

    public double MarioX { get; private set; }
    public double MarioY { get; private set; }
    public double MarioVelocityX { get; private set; }
    public double MarioVelocityY { get; private set; }
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
    
    public IEnumerable<EnemySnapshot> Enemies => GetEnemySnapshots();
    public bool IsGrounded => Collides(MarioX, MarioY + 1, MarioCollisionWidth, MarioCollisionHeight);
    public double DistanceToNextObstacle => FindDistanceToNextObstacle(128);
    public double DistanceToNextGap => FindDistanceToNextGap(128);
    public double DistanceToNextOverheadBlock => FindDistanceToNextOverheadBlock(128);
    public double DistanceToNextPipe => FindDistanceToNextPipe(192);
    public double NextGroundDeltaY => FindGroundY((int)MarioX + MarioCollisionWidth + 32) - (MarioY + MarioCollisionHeight);
    public bool IsBlockedAhead => DistanceToNextObstacle < 24;
    public bool IsGapAhead => DistanceToNextGap < 24;
    public bool HasCeilingAbove => DistanceToNextOverheadBlock < 24;
    public double RunMood { get; private set; }

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
            var progress = BestX * 10.0;
            var progressPerTick = Ticks == 0 ? 0.0 : Math.Max(0.0, BestX - 24.0) / Ticks;
            var speedBonus = Math.Max(0.0, MarioVelocityX) * 40.0;
            var averageSpeedBonus = progressPerTick * 2200.0;
            var aliveBonus = Math.Min(Ticks, 1200) * 0.15;
            var stuckPenalty = m_ticksWithoutProgress * 2.0;
            var jumpSpamPenalty = Math.Max(0, MaxTicksSinceJumpBeforePenalty - TicksSinceLastJump) * 0.08;
            var jumpRatePenalty = Ticks == 0 ? 0.0 : m_jumpCount / (double)Ticks * 4000.0;
            var airRatePenalty = Ticks == 0 ? 0.0 : Math.Max(0.0, m_airTicks / (double)Ticks - 0.34) * 1600.0;
            var slowRatePenalty = Ticks == 0 ? 0.0 : m_slowTicks / (double)Ticks * 420.0;
            var fastRateBonus = Ticks == 0 ? 0.0 : m_fastTicks / (double)Ticks * 900.0;
            var walkRate = Ticks == 0 ? 0.0 : m_walkSpeedTicks / (double)Ticks;
            var runRate = Ticks == 0 ? 0.0 : m_runSpeedTicks / (double)Ticks;
            var speedVarietyBonus = Math.Min(walkRate, 0.18) * 2400.0 + Math.Min(runRate, 0.45) * 1800.0;
            var efficiencyBonus = progressPerTick * 1600.0;
            var tastefulHeadbuttBonus = Math.Min(m_blockHits, 2) * 450.0;
            var questionBlockBonus = Math.Min(m_questionBlockHits, 2) * 6500.0;
            var noQuestionBlockPenalty = HasReachedFlagPole && m_questionBlockHits == 0 ? 14000.0 : 0.0;
            var oneQuestionBlockPenalty = HasReachedFlagPole && m_questionBlockHits == 1 ? 4000.0 : 0.0;
            var styleJumpBonus = Math.Min(m_styleJumpCount, 4) * 180.0;
            var tallPipeBonus = BestX >= 944.0
                ? 4000.0 + (BestX - 944.0) * 5.0
                : BestX >= 850.0
                    ? (BestX - 850.0) * 15.0
                    : 0.0;
            var fastFinishBonus = HasReachedFlagPole ? Math.Max(0.0, MaxTicks - Ticks) * 3.0 : 0.0;
            var flagHeightBonus = HasReachedFlagPole ? Math.Max(0.0, ViewHeight - (MarioY + MarioCollisionHeight)) * 140.0 : 0.0;
            var finishBonus = HasReachedFlagPole ? 20000.0 + fastFinishBonus + flagHeightBonus : 0.0;
            var pogoPenalty = m_pogoJumpCount * 800.0 + Math.Max(0, m_jumpCount - 20) * 420.0;
            var excessiveJumpPenalty = Math.Pow(Math.Max(0, m_jumpCount - 26), 2.0) * 220.0;
            var earlyJumpPenalty = m_unneededEarlyJumpCount * 1800.0;
            var styleSpamPenalty = Math.Max(0, m_styleJumpCount - 6) * 500.0;
            var headbuttPenalty = Math.Max(0, m_blockHits - 2) * 450.0;
            var wallStallPenalty = m_wallStallTicks * 35.0;
            var fallPenalty = m_fell ? 3000.0 : 0.0;
            var reversePenalty = Math.Max(0.0, -MarioVelocityX) * 70.0;
            return Math.Max(0.0,
                progress +
                speedBonus +
                averageSpeedBonus +
                aliveBonus +
                efficiencyBonus +
                fastRateBonus +
                speedVarietyBonus +
                tastefulHeadbuttBonus +
                questionBlockBonus +
                styleJumpBonus +
                tallPipeBonus +
                finishBonus -
                stuckPenalty -
                jumpSpamPenalty -
                jumpRatePenalty -
                airRatePenalty -
                slowRatePenalty -
                pogoPenalty -
                excessiveJumpPenalty -
                earlyJumpPenalty -
                styleSpamPenalty -
                headbuttPenalty -
                noQuestionBlockPenalty -
                oneQuestionBlockPenalty -
                wallStallPenalty -
                fallPenalty -
                reversePenalty);
        }
    }

    public override double DegeneracyScore =>
        m_ticksWithoutProgress >= MaxTicksWithoutProgress ? 0.8 : 0.0;

    public override string DegeneracyReason =>
        DegeneracyScore > 0 ? "stuck" : string.Empty;

    public override (string Name, double Value, string Format)? BestObservedMetric =>
        ("X", BestX, "0");

    public Game(int arenaWidth, int arenaHeight, Brain brain, bool useTrainingTimeouts = false, bool enableEnemies = false) : base(arenaWidth, arenaHeight, brain)
    {
        m_useTrainingTimeouts = useTrainingTimeouts;
        m_enableEnemies = enableEnemies;
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
        m_jumpCount = 0;
        m_airTicks = 0;
        m_slowTicks = 0;
        m_fastTicks = 0;
        m_walkSpeedTicks = 0;
        m_runSpeedTicks = 0;
        m_wallStallTicks = 0;
        m_blockHits = 0;
        m_questionBlockHits = 0;
        m_pogoJumpCount = 0;
        m_styleJumpCount = 0;
        m_unneededEarlyJumpCount = 0;
        m_lastStyleJumpX = MarioX;
        RunMood = GameRand.NextDouble() * 2.0 - 1.0;
        m_scoredBlockHitTiles.Clear();
        m_brokenBlockTiles.Clear();
        m_usedQuestionBlocks.Clear();
        m_fell = false;
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
        var move = ((Brain)Brain).ChooseMove(m_gameState);
        var isUnneededEarlyJump = move.Jump && IsUnneededEarlyJump();
        if (grounded && isUnneededEarlyJump)
            m_unneededEarlyJumpCount++;
        if (grounded && move.Jump && !isUnneededEarlyJump && TicksSinceLastJump >= JumpCooldownTicks)
        {
            var ticksSinceLastJump = TicksSinceLastJump;
            MarioVelocityY = MarioVelocityX > MaxWalkPixelsPerFrame
                ? RunningJumpVelocityPixelsPerFrame
                : WalkingJumpVelocityPixelsPerFrame;
            TicksSinceLastJump = 0;
            m_jumpCount++;
            if (ticksSinceLastJump < 42)
                m_pogoJumpCount++;
            else if (MarioVelocityX > MaxWalkPixelsPerFrame && MarioX - m_lastStyleJumpX >= 96.0)
            {
                m_styleJumpCount++;
                m_lastStyleJumpX = MarioX;
            }

            grounded = false;
        }

        if (!grounded)
            m_airTicks++;
        if (Math.Abs(MarioVelocityX) < 0.35)
            m_slowTicks++;
        if (MarioVelocityX > 1.8)
            m_fastTicks++;
        if (MarioVelocityX is >= 0.65 and <= MaxWalkPixelsPerFrame)
            m_walkSpeedTicks++;
        if (MarioVelocityX > MaxWalkPixelsPerFrame)
            m_runSpeedTicks++;

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

    public bool IsSolidAtOffset(int dx, int dy)
    {
        var x = (int)(MarioX + MarioCollisionWidth + dx);
        var y = (int)(MarioY + MarioCollisionHeight + dy);
        return IsSolidPixel(x, y);
    }

    public bool IsBlockBroken(int tileX, int tileY) =>
        m_brokenBlockTiles.Contains((tileX, tileY));

    public bool IsQuestionBlockUsed(int tileX, int tileY) =>
        m_usedQuestionBlocks.Contains((tileX - tileX % 2, tileY - tileY % 2));

    public int CurrentEnemyCount => m_enableEnemies ? m_enemyCount : 0;

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
            MarioVelocityX += direction * AccelerationPixelsPerFrameSquared;

        MarioVelocityX = Math.Clamp(MarioVelocityX, -maxSpeed, maxSpeed);
    }

    private bool IsUnneededEarlyJump()
    {
        if (MarioX >= 112.0)
            return false;
        if (DistanceToNextObstacle < 28 || DistanceToNextGap < 28)
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
        yield return ("Stuck", m_ticksWithoutProgress.ToString());
        yield return ("Jumps", m_jumpCount.ToString());
        yield return ("Early", m_unneededEarlyJumpCount.ToString());
        yield return ("Pogo", m_pogoJumpCount.ToString());
        yield return ("Style", m_styleJumpCount.ToString());
        yield return ("Wall", m_wallStallTicks.ToString());
        yield return ("Walk", m_walkSpeedTicks.ToString());
        yield return ("Run", m_runSpeedTicks.ToString());
        yield return ("Blocks", m_blockHits.ToString());
        yield return ("Questions", m_questionBlockHits.ToString());
        yield return ("Enemies", m_enemies.Count.ToString());
        yield return ("Finished", HasReachedFlagPole ? "1" : "0");
        yield return ("Fell", m_fell ? "1" : "0");
    }

    private IEnumerable<EnemySnapshot> GetEnemySnapshots()
    {
        foreach (var enemy in m_enemies)
            yield return new EnemySnapshot(enemy.X, enemy.Y, enemy.IsDead);
    }

    private void ResetEnemies()
    {
        m_enemies.Clear();
        if (!m_enableEnemies)
            return;

        if (m_pendingEnemyIncrease)
        {
            m_enemyCount += 2;
            m_pendingEnemyIncrease = false;
        }

        for (var i = 0; i < m_enemyCount; i++)
        {
            var x = GameRand.Next(280, FlagPoleX - 240);
            var y = GameRand.Next(-160, -24);
            m_enemies.Add(new Enemy(x, y, -0.55));
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
        foreach (var enemy in m_enemies)
        {
            if (enemy.IsDead || !Overlaps(MarioX, MarioY, MarioCollisionWidth, MarioCollisionHeight, enemy.X, enemy.Y, EnemyCollisionWidth, EnemyCollisionHeight))
                continue;

            var enemyTop = enemy.Y + 3;
            if (MarioVelocityY > 0 && previousMarioBottom <= enemyTop)
            {
                enemy.IsDead = true;
                enemy.VelocityX = 0;
                enemy.VelocityY = -4.0;
                MarioVelocityY = -3.7;
                TicksSinceLastJump = 0;
                return;
            }

            KillMario();
            return;
        }
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
        if (m_scoredBlockHitTiles.Add((blockX, blockY)))
        {
            m_blockHits++;
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

    private double FindDistanceToNextOverheadBlock(int maxDistance)
    {
        var headY = (int)MarioY;
        for (var dx = 1; dx <= maxDistance; dx++)
        {
            var x = (int)(MarioX + MarioCollisionWidth + dx);
            if (IsSolidPixel(x, headY - 1) || IsSolidPixel(x, headY - 9))
                return dx;
        }

        return maxDistance;
    }

    private double FindDistanceToNextPipe(int maxDistance)
    {
        var currentTileX = (int)(MarioX + MarioCollisionWidth) / s_level.TileSize;
        var maxTileDx = maxDistance / s_level.TileSize;
        for (var dx = 0; dx <= maxTileDx; dx++)
        {
            var tileX = currentTileX + dx;
            if (tileX < 0 || tileX >= s_level.WidthTiles)
                break;

            for (var tileY = 0; tileY < s_level.HeightTiles; tileY++)
            {
                if (s_level.Map[tileY][tileX] is >= 31 and <= 39)
                    return Math.Max(0, tileX * s_level.TileSize - (MarioX + MarioCollisionWidth));
            }
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
        if (m_useTrainingTimeouts)
            return;

        m_pendingEnemyIncrease = true;
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
