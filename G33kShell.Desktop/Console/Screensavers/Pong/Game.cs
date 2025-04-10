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
using System.Diagnostics;
using System.Numerics;
using CSharp.Core;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Pong;

[DebuggerDisplay("Rating = {Rating}, Scores = {Scores[0]} vs {Scores[1]}")]
public class Game : AiGameBase
{
    private const int ScoreToWin = 10;
    private const float BatSpeed = 0.22f;
    private Vector2 m_ballVelocity;
    private int m_ballMoves;
    private int m_rallies;

    public const int BatHeight = 4;
    public Vector2[] BatPositions { get; } = new Vector2[2];
    public Vector2 BallPosition { get; private set; }
    public int[] Scores { get; } = new int[2];
    
    public override double Rating
    {
        get
        {
            if (Scores[0] + Scores[1] == 0)
                return 0.0; // No score - rubbish game.
            return m_rallies * 0.2                                    // Reward rallies.
                   + m_ballMoves * 0.01                             // ...and long games.
                   + (ScoreToWin - Math.Abs(Scores[0] - Scores[1])) // Reward balanced scores.
                   + (Scores[0] + Scores[1]) / (2.0 * ScoreToWin);  // Reward high score. (Note: Might be small if game times-out.)
        }
    }

    public override bool IsGameOver =>
        Scores[0] == ScoreToWin || Scores[1] == ScoreToWin || m_rallies > 10000;

    public Game(int arenaWidth, int arenaHeight) : base(arenaWidth, arenaHeight, new Brain())
    {
        ResetGame();
    }

    private void ResetGame()
    {
        BatPositions[0] = new Vector2(2, m_arenaHeight / 2.0f);
        BatPositions[1] = new Vector2(m_arenaWidth - 3, m_arenaHeight / 2.0f);
        ResetBall();
        Scores[0] = Scores[1] = 0;
        m_rallies = 0;
        m_ballMoves = 0;
    }

    private void ResetBall()
    {
        BallPosition = new Vector2(m_arenaWidth / 2.0f, m_arenaHeight / 2.0f);
        m_ballVelocity = new Vector2(m_rand.NextBool() ? -1.0f : 1.0f, (float)m_rand.NextDouble() - 0.5f);
        NormalizeBallVelocity();
    }

    public override void Tick()
    {
        if (IsGameOver)
            return;
        
        // Move the ball.
        BallPosition += m_ballVelocity;
        m_ballMoves++;
        if (BallPosition.Y < 0)
        {
            BallPosition = BallPosition with { Y = 0.0f };
            m_ballVelocity.Y *= -1.0f;
        } else if (BallPosition.Y >= m_arenaHeight)
        {
            BallPosition = BallPosition with { Y = m_arenaHeight - 1.0f };
            m_ballVelocity.Y *= -1.0f;
        }
        if (BallPosition.X < 0)
        {
            Scores[1]++;
            ResetBall();
            m_ballVelocity.X = -MathF.Abs(m_ballVelocity.X);
        } else if (BallPosition.X >= m_arenaWidth)
        {
            Scores[0]++;
            ResetBall();
            m_ballVelocity.X = MathF.Abs(m_ballVelocity.X);
        }

        NormalizeBallVelocity();

        if (IsGameOver)
            return;

        // Check for bat/ball collision.
        var ballX = (int)BallPosition.X;
        var hitFactor = CheckVCollision(BatPositions[0], BallPosition.Y);
        if (m_ballVelocity.X < 0.0f && ballX <= (int)BatPositions[0].X + 1 && hitFactor >= 0.0f && hitFactor <= 1.0f)
        {
            m_rallies++;
            m_ballVelocity.X *= -1.0f;
            m_ballVelocity.Y = hitFactor.Lerp(-1.0f, 1.0f) * 0.5f;
        }
        hitFactor = CheckVCollision(BatPositions[1], BallPosition.Y);
        if (m_ballVelocity.X > 0.0f && ballX >= (int)BatPositions[1].X - 1 && hitFactor >= 0.0f && hitFactor <= 1.0f)
        {
            m_rallies++;
            m_ballVelocity.X *= -1.0f;
            m_ballVelocity.Y = hitFactor.Lerp(-1.0f, 1.0f) * 0.5f;
        }

        NormalizeBallVelocity();

        // Move the bats.
        var gameState = new GameState(BatPositions, BallPosition, m_ballVelocity, m_arenaWidth, m_arenaHeight);
        var newDirections = ((Brain)Brain).ChooseMoves(gameState);
        var nextY = BatPositions[0].Y + BatSpeed * newDirections.LeftBat switch
        {
            Direction.Up => -1,
            Direction.Down => 1,
            _ => 0
        };
        if (nextY >= BatHeight / 2.0f && nextY < m_arenaHeight - BatHeight / 2.0f)
            BatPositions[0].Y = nextY;
        nextY = BatPositions[1].Y + BatSpeed * newDirections.RightBat switch
        {
            Direction.Up => -1,
            Direction.Down => 1,
            _ => 0
        };
        if (nextY >= BatHeight / 2.0f && nextY < m_arenaHeight - BatHeight / 2.0f)
            BatPositions[1].Y = nextY;
    }

    private void NormalizeBallVelocity() =>
        m_ballVelocity = Vector2.Normalize(m_ballVelocity) * 0.6f;

    private static float CheckVCollision(Vector2 batPosition, float ballY)
    {
        var batTop = (int)(batPosition.Y - BatHeight / 2.0f);
        var batBottom = (int)(batPosition.Y + BatHeight / 2.0f);
        return ballY.InverseLerp(batTop, batBottom);
    }

    protected override AiGameBase CreateGame(int arenaWidth, int arenaHeight) =>
        new Game(arenaWidth, arenaHeight);

    protected override AiBrainBase CloneBrain() =>
        Brain.Clone<Brain>();
}