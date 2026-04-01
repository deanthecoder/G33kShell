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
using System.Diagnostics;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Breakout;

/// <summary>
/// Breakout simulation used for both play and neuroevolution training.
/// </summary>
[DebuggerDisplay("Rating = {Rating}, Score = {Score}, Lives = {Lives}")]
public class Game : AiGameBase
{
    private const int BrickWidth = 5;
    private const int BrickHeight = 1;
    private const int BrickGap = 0;
    private const int ClearRowsAboveWall = 3;
    private const int MaxTicksWithoutBrick = 1400;
    private const int MaxTicksPerGame = 5000;
    private const double PaddleSpeed = 1.05;
    private const double LiveBounceJitterDegrees = 2.0;
    private const double PaddleDeflectionScale = 1.18;
    private const double PaddleDeflectionCurve = 0.7;

    private readonly bool m_useTrainingTimeouts;
    private GameState m_gameState;
    private int m_bricksAtLevelStart;
    private double m_cumulativeLandingError;
    private int m_descendingTicks;
    private int m_misses;
    private int m_paddleHits;
    private int m_ticks;
    private int m_ticksSinceBrick;
    private int m_bricksBroken;
    private int m_remainingBricks;

    public const int MaxLives = 3;
    public const double MaxBallSpeed = 1.1;

    public double BallDx { get; private set; }
    public double BallDy { get; private set; }
    public double BallX { get; private set; }
    public double BallY { get; private set; }
    public int BrickCols { get; private set; }
    public double BrickCompletionRatio => m_bricksAtLevelStart == 0 ? 1.0 : (double)m_bricksBroken / m_bricksAtLevelStart;
    public int BrickOffsetX { get; private set; }
    public int BrickOffsetY { get; private set; }
    public int BrickRows { get; private set; }
    public int Lives { get; private set; }
    public string Message { get; private set; }
    public int MessageFrames { get; private set; }

    public double PaddleX { get; private set; }
    public int PaddleWidth { get; private set; }
    public int PaddleY { get; private set; }
    public int Score { get; private set; }
    public int Level { get; private set; }

    private double TrackingQuality => m_descendingTicks == 0
        ? 0.0
        : 1.0 - Math.Min(1.0, m_cumulativeLandingError / m_descendingTicks * 3.0);

    internal int ArenaPixelHeight => ArenaHeight;
    internal int ArenaPixelWidth => ArenaWidth;
    internal bool[,] Bricks { get; private set; }

    public override double Rating
    {
        get
        {
            if (m_paddleHits < 2)
                return 0.0;

            var survivalScore = Math.Min(m_ticks, MaxTicksPerGame) * 0.025;
            var levelScore = (Level - 1) * 300.0;
            var controlFactor = Math.Min(1.0, m_paddleHits / 8.0);
            var controlScore = m_paddleHits * 320.0;
            var progressScore = (Score * 0.18 + m_bricksBroken * 30.0 + levelScore) * controlFactor;
            var stabilityPenalty = m_misses * (280.0 - 80.0 * controlFactor);

            return progressScore +
                   controlScore +
                   TrackingQuality * 1200.0 +
                   survivalScore +
                   stabilityPenalty * -1.0;
        }
    }

    public override bool IsGameOver =>
        Lives <= 0 ||
        (m_useTrainingTimeouts && (m_ticks >= MaxTicksPerGame || m_ticksSinceBrick >= MaxTicksWithoutBrick));

    public Game(int arenaWidth, int arenaHeight, Brain brain, bool useTrainingTimeouts = false) : base(arenaWidth, arenaHeight, brain)
    {
        m_useTrainingTimeouts = useTrainingTimeouts;
    }

    public override IEnumerable<(string Name, string Value)> ExtraGameStats()
    {
        yield return ("Score", Score.ToString());
        yield return ("Level", Level.ToString());
        yield return ("Lives", Lives.ToString());
        yield return ("Bricks", m_bricksBroken.ToString());
        yield return ("PaddleHits", m_paddleHits.ToString());
        yield return ("Track", TrackingQuality.ToString("P1"));
        yield return ("Ticks", m_ticks.ToString());
    }

    public override Game ResetGame()
    {
        Brain.ResetTemporalState();
        Score = 0;
        Lives = MaxLives;
        Level = 1;
        m_bricksBroken = 0;
        m_cumulativeLandingError = 0.0;
        m_descendingTicks = 0;
        m_misses = 0;
        m_paddleHits = 0;
        m_ticks = 0;
        m_ticksSinceBrick = 0;
        Message = string.Empty;
        MessageFrames = 0;

        CreateLevel(announceLevel: false);
        return this;
    }

    public override void Tick()
    {
        if (IsGameOver)
            return;

        if (MessageFrames > 0)
            MessageFrames--;

        m_gameState ??= new GameState(this);
        m_gameState.Reset(this);
        var move = ((Brain)Brain).ChooseMove(m_gameState);
        PaddleX = (PaddleX + move * PaddleSpeed).Clamp(GetMinPaddleX(), GetMaxPaddleX());

        if (BallDy > 0.0)
        {
            var landingError = Math.Abs(PredictLandingX() - PaddleX) / Math.Max(1.0, ArenaWidth);
            m_cumulativeLandingError += landingError;
            m_descendingTicks++;
        }

        AdvanceBall();
        m_ticks++;
        m_ticksSinceBrick++;
    }

    public double PredictLandingX()
    {
        if (BallDy <= 0.0)
            return PaddleX;

        var x = BallX;
        var y = BallY;
        var dx = BallDx;
        var dy = BallDy;
        var leftWall = 1.0;
        var rightWall = ArenaWidth - 2.0;

        for (var i = 0; i < 256 && y < PaddleY; i++)
        {
            x += dx;
            y += dy;
            if (x < leftWall)
            {
                x = leftWall + (leftWall - x);
                dx = -dx;
            }
            else if (x > rightWall)
            {
                x = rightWall - (x - rightWall);
                dx = -dx;
            }

            if (y < 2.0)
            {
                y = 2.0 + (2.0 - y);
                dy = -dy;
            }
        }

        return x;
    }

    public bool TryFindNearestBrick(out double brickX, out double brickY)
    {
        brickX = 0.0;
        brickY = 0.0;
        var nearestDistance = double.MaxValue;
        for (var row = 0; row < BrickRows; row++)
        {
            for (var col = 0; col < BrickCols; col++)
            {
                if (!Bricks[col, row])
                    continue;

                var x = BrickOffsetX + col * (BrickWidth + BrickGap) + (BrickWidth - 1) / 2.0;
                var y = BrickCellY(row) + (BrickHeight - 1) / 2.0;
                var dx = x - BallX;
                var dy = y - BallY;
                var distance = dx * dx + dy * dy;
                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                brickX = x;
                brickY = y;
            }
        }

        return nearestDistance < double.MaxValue;
    }

    public double GetBrickDensityAroundBall(int columnOffset)
    {
        if (m_remainingBricks == 0)
            return 0.0;

        var column = FindBrickColumn(BallX) + columnOffset;
        if (column < 0 || column >= BrickCols)
            return 0.0;

        var hits = 0;
        for (var row = 0; row < BrickRows; row++)
        {
            if (Bricks[column, row])
                hits++;
        }

        return hits / (double)Math.Max(1, BrickRows);
    }

    private void CreateLevel(bool announceLevel)
    {
        PaddleWidth = Math.Clamp(ArenaWidth / 8, 7, 13);
        PaddleY = ArenaHeight - 3;

        var usableWidth = ArenaWidth - 4;
        BrickCols = Math.Max(6, usableWidth / (BrickWidth + BrickGap));
        var baseRows = Math.Clamp((ArenaHeight - 10) / 2, 4, 7);
        BrickRows = Math.Max(5, Math.Clamp(baseRows * 2, 8, 12) - ClearRowsAboveWall);
        BrickOffsetX = (ArenaWidth - (BrickCols * (BrickWidth + BrickGap) - BrickGap)) / 2;
        BrickOffsetY = 3 + ClearRowsAboveWall;
        Bricks = new bool[BrickCols, BrickRows];

        m_remainingBricks = 0;
        var pattern = GameRand.Next(4);
        var mirror = GameRand.NextBool();
        var stripePhase = GameRand.Next(3);
        var tighten = GameRand.NextDouble().Lerp(0.35, 0.65);
        var center = (BrickCols - 1) / 2.0;
        for (var row = 0; row < BrickRows; row++)
        {
            for (var col = 0; col < BrickCols; col++)
            {
                var sampleCol = mirror ? BrickCols - 1 - col : col;
                var active = pattern switch
                {
                    0 => true,
                    1 => Math.Abs(sampleCol - center) <= center - row * tighten,
                    2 => (sampleCol + row + stripePhase) % 3 != 1 || row < 2,
                    _ => row < 2 || Math.Abs(sampleCol - center) >= row * tighten
                };
                if (!active)
                    continue;

                Bricks[col, row] = true;
                m_remainingBricks++;
            }
        }

        m_bricksAtLevelStart = m_remainingBricks;
        PaddleX = (ArenaWidth / 2.0 + GameRand.NextDouble().Lerp(-ArenaWidth * 0.08, ArenaWidth * 0.08)).Clamp(GetMinPaddleX(), GetMaxPaddleX());
        ResetBall();

        if (announceLevel)
            SetMessage($"LEVEL {Level}", 45);
    }

    private void ResetBall()
    {
        BallX = (ArenaWidth / 2.0 + GameRand.NextDouble().Lerp(-ArenaWidth * 0.1, ArenaWidth * 0.1)).Clamp(3.0, ArenaWidth - 4.0);
        BallY = PaddleY - 2;
        BallDx = GameRand.NextBool() ? -1.0 : 1.0;
        BallDx *= GameRand.NextDouble().Lerp(0.55, 0.95);
        BallDy = -GameRand.NextDouble().Lerp(0.6, 0.95);
        NormalizeBallSpeed();
    }

    private void AdvanceBall()
    {
        for (var i = 0; i < 2; i++)
        {
            var nextX = BallX + BallDx * 0.5;
            var nextY = BallY + BallDy * 0.5;

            if (nextX < 1.0)
            {
                nextX = 1.0 + (1.0 - nextX);
                BallDx = -BallDx;
                ApplyLiveBounceJitter();
            }
            else if (nextX > ArenaWidth - 2.0)
            {
                nextX = ArenaWidth - 2.0 - (nextX - (ArenaWidth - 2.0));
                BallDx = -BallDx;
                ApplyLiveBounceJitter();
            }

            if (nextY < 2.0)
            {
                nextY = 2.0 + (2.0 - nextY);
                BallDy = -BallDy;
                ApplyLiveBounceJitter();
            }

            if (TryHitBrick(nextX, nextY, out var bounceX))
            {
                if (bounceX)
                    BallDx = -BallDx;
                else
                    BallDy = -BallDy;
                ApplyLiveBounceJitter();
                nextX = BallX + BallDx * 0.5;
                nextY = BallY + BallDy * 0.5;
            }

            var paddleLeft = GetPaddleLeft();
            var paddleRight = paddleLeft + PaddleWidth - 1;
            if (BallDy > 0.0 && BallY <= PaddleY - 0.4 && nextY >= PaddleY - 1.0 &&
                nextX >= paddleLeft - 0.6 && nextX <= paddleRight + 0.6)
            {
                var paddleCenter = paddleLeft + (PaddleWidth - 1) / 2.0;
                var hitOffset = ((nextX - paddleCenter) / Math.Max(1.0, PaddleWidth / 2.0)).Clamp(-1.0, 1.0);
                var curvedHitOffset = Math.Sign(hitOffset) * Math.Pow(Math.Abs(hitOffset), PaddleDeflectionCurve);
                BallDx = curvedHitOffset.Lerp(-PaddleDeflectionScale, PaddleDeflectionScale);
                BallDy = -Math.Abs(BallDy) - 0.05;
                NormalizeBallSpeed();
                nextY = PaddleY - 1.05;
                m_paddleHits++;
            }
            else if (nextY >= ArenaHeight - 1.0)
            {
                Lives--;
                m_misses++;
                if (Lives > 0)
                {
                    SetMessage("MISS", 30);
                    ResetBall();
                }
                else
                {
                    SetMessage("GAME OVER", 60);
                }
                return;
            }

            BallX = nextX;
            BallY = nextY;
        }
    }

    private bool TryHitBrick(double nextX, double nextY, out bool bounceX)
    {
        bounceX = false;
        var col = FindBrickColumn(nextX);
        var row = FindBrickRow(nextY);
        if (col < 0 || row < 0 || !Bricks[col, row])
            return false;

        Bricks[col, row] = false;
        m_remainingBricks--;
        m_bricksBroken++;
        m_ticksSinceBrick = 0;
        Score += 10 + (BrickRows - row) * 3;

        var brickLeft = BrickCellX(col);
        var brickRight = brickLeft + BrickWidth - 1;
        var brickTop = BrickCellY(row);
        var brickBottom = brickTop + BrickHeight - 1;
        var prevCol = FindBrickColumn(BallX);
        var prevRow = FindBrickRow(BallY);
        bounceX = prevCol != col && prevRow == row && (BallX < brickLeft || BallX > brickRight);
        if (prevCol == col && prevRow != row && (BallY < brickTop || BallY > brickBottom))
            bounceX = false;

        if (m_remainingBricks == 0)
        {
            Level++;
            Lives = Math.Min(MaxLives, Lives + 1);
            CreateLevel(announceLevel: true);
        }

        return true;
    }

    private int FindBrickColumn(double x)
    {
        var relativeX = x - BrickOffsetX;
        if (relativeX < 0)
            return -1;

        var slot = (int)(relativeX / (BrickWidth + BrickGap));
        if (slot < 0 || slot >= BrickCols)
            return -1;

        var localX = relativeX - slot * (BrickWidth + BrickGap);
        return localX <= BrickWidth - 0.1 ? slot : -1;
    }

    private int FindBrickRow(double y)
    {
        var relativeY = y - BrickOffsetY;
        if (relativeY < 0)
            return -1;

        var row = (int)(relativeY / BrickHeight);
        if (row < 0 || row >= BrickRows)
            return -1;

        var localY = relativeY - row * BrickHeight;
        return localY <= BrickHeight - 0.1 ? row : -1;
    }

    private int BrickCellX(int col) => BrickOffsetX + col * (BrickWidth + BrickGap);

    private int BrickCellY(int row) => BrickOffsetY + row * BrickHeight;

    private int GetPaddleLeft() =>
        ((int)Math.Round(PaddleX - PaddleWidth / 2.0)).Clamp(1, ArenaWidth - 1 - PaddleWidth);

    private double GetMinPaddleX() => 1 + PaddleWidth / 2.0;

    private double GetMaxPaddleX() => ArenaWidth - 2 - PaddleWidth / 2.0;

    private void NormalizeBallSpeed()
    {
        var speed = (1.08 + Math.Min(0.28, (Level - 1) * 0.025)) * 0.75;
        var length = Math.Sqrt(BallDx * BallDx + BallDy * BallDy);
        if (length <= 0.0001)
        {
            BallDx = 0.75;
            BallDy = -0.75;
            length = Math.Sqrt(BallDx * BallDx + BallDy * BallDy);
        }

        BallDx = BallDx / length * speed;
        BallDy = BallDy / length * speed;
        if (Math.Abs(BallDy) < 0.38)
            BallDy = Math.Sign(BallDy == 0 ? -1 : BallDy) * 0.38;
    }

    private void ApplyLiveBounceJitter()
    {
        if (m_useTrainingTimeouts)
            return;

        var angle = GameRand.NextDouble().Lerp(-LiveBounceJitterDegrees, LiveBounceJitterDegrees) * Math.PI / 180.0;
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        var rotatedDx = BallDx * cos - BallDy * sin;
        var rotatedDy = BallDx * sin + BallDy * cos;
        BallDx = rotatedDx;
        BallDy = rotatedDy;
        NormalizeBallSpeed();
    }

    private void SetMessage(string text, int frames)
    {
        Message = text;
        MessageFrames = frames;
    }
}
