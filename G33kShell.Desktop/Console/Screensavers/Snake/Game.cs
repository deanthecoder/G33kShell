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
using System.Linq;
using CSharp.Core;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

[DebuggerDisplay("HighScore = {HighScore}, Rating = {Rating}")]
public class Game
{
    private readonly int m_arenaWidth;
    private readonly int m_arenaHeight;
    private readonly bool m_limitLives;
    private readonly Random m_rand = new Random();
    private const int StartingLives = 10;
    private int m_totalMoves;
    private int m_totalScore;
    
    public int Lives = StartingLives;
    public Brain Brain { get; private init; } = new Brain();
    public Snake Snake { get; private set; }
    public IntPoint FoodPosition { get; private set; }
    public int HighScore { get; private set; }
    public int Score { get; private set; }
    public Dictionary<Snake.DeathType, int> DeathReasons { get; }

    /// <summary>
    /// Combination of high score, average score, etc.
    /// </summary>
    public double Rating =>
        m_totalScore > 0
            ? (double)m_totalScore / StartingLives * 2.0 + HighScore * 0.3 + m_totalMoves * 0.001
            : 0; // no reward for circling and dying
    
    public Game(int arenaWidth, int arenaHeight, bool limitLives = true)
    {
        m_arenaWidth = arenaWidth;
        m_arenaHeight = arenaHeight;
        m_limitLives = limitLives;

        DeathReasons =
            Enum.GetValues(typeof(Snake.DeathType))
                .Cast<Snake.DeathType>()
                .ToDictionary(k => k, _ => 0);
        
        ResetGame();
    }

    private void ResetGame()
    {
        Snake = new Snake(m_arenaWidth, m_arenaHeight);
        Snake.TerminalCollision += (_, collisionType) => DeathReasons[collisionType]++;
        Snake.FoodEaten += (_, _) =>
        {
            Score++;
            m_totalScore++;
            HighScore = Math.Max(HighScore, Score);
            SpawnFood();
        };
        Snake.Starved += (_, _) => DeathReasons[Snake.DeathType.Starved]++;
        Score = 0;

        SpawnFood();
    }

    private void SpawnFood()
    {
        while (true)
        {
            FoodPosition = new IntPoint(m_rand.Next(0, m_arenaWidth), m_rand.Next(0, m_arenaHeight));
            if (!Snake.IsCollision(FoodPosition, out _))
                return;
        }
    }

    public void Tick()
    {
        if (Snake.IsDead)
        {
            if  (m_limitLives)
            {
                if (Lives == 0)
                    return;
                Lives--;
            }
            
            ResetGame();
        }
        
        var gameState = new GameState(Snake, FoodPosition);
        var newDirection = Brain.ChooseMove(gameState);
        Snake.Move(newDirection, FoodPosition);
        m_totalMoves++;
    }

    public Game MergeWith(Game other)
    {
        var newGame = new Game(m_arenaWidth, m_arenaHeight) { Brain = Brain.Clone() };
        switch (m_rand.Next(2))
        {
            case 0: // Average weights.
                newGame.Brain.AverageWith(other.Brain);
                break;
            case 1: // Perturb weights.
                newGame.Brain.MixWith(Brain);
                break;
            default:
                throw new InvalidOperationException("Unknown NN merge mode.");
        }
        
        return newGame;
    }

    public Game Resurrect() =>
        new Game(m_arenaWidth, m_arenaHeight)
        {
            Brain = Brain, // New game, but keep the old snake brain.
            HighScore = HighScore
        };

    public void LoadBrainData(byte[] brainBytes) => Brain.Load(brainBytes);
}