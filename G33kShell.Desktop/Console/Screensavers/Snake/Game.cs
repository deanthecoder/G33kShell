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
using DTC.Core;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

[DebuggerDisplay("HighScore = {HighScore}, Rating = {Rating}")]
public class Game : AiGameBase
{
    private readonly bool m_limitLives;
    private const int StartingLives = 10;
    private int m_totalMoves;
    private int m_totalScore;
    private int m_lives = StartingLives;
    
    public Snake Snake { get; private set; }
    public IntPoint FoodPosition { get; private set; }
    public int HighScore { get; private set; }
    public int Score { get; private set; }

    /// <summary>
    /// Combination of high score, average score, etc.
    /// </summary>
    public override double Rating =>
        m_totalScore > 0
            ? (double)m_totalScore / StartingLives * 2.0 + HighScore * 0.3 + m_totalMoves * 0.001
            : 0; // no reward for circling and dying

    public override bool IsGameOver =>
        m_lives== 0 && Snake.IsDead;

    public override IEnumerable<(string Name, string Value)> ExtraGameStats()
    {
        yield return ("HighScore", HighScore.ToString());
    }

    public Game(int arenaWidth, int arenaHeight, AiBrainBase brain, bool limitLives = true) : base(arenaWidth, arenaHeight, brain)
    {
        m_limitLives = limitLives;
    }

    public override AiGameBase ResetGame()
    {
        Snake = new Snake(ArenaWidth, ArenaHeight);
        Snake.FoodEaten += (_, _) =>
        {
            Score++;
            m_totalScore++;
            HighScore = Math.Max(HighScore, Score);
            SpawnFood();
        };
        Score = 0;

        SpawnFood();
        
        return this;
    }

    private void SpawnFood()
    {
        while (true)
        {
            FoodPosition = new IntPoint(GameRand.Next(0, ArenaWidth), GameRand.Next(0, ArenaHeight));
            if (!Snake.IsCollision(FoodPosition))
                return;
        }
    }

    public override void Tick()
    {
        if (Snake.IsDead)
        {
            if  (m_limitLives)
            {
                if (m_lives == 0)
                    return;
                m_lives--;
            }
            
            ResetGame();
        }
        
        var gameState = new GameState(Snake, FoodPosition);
        var newDirection = ((Brain)Brain).ChooseMove(gameState);
        Snake.Move(newDirection, FoodPosition);
        m_totalMoves++;
    }
}