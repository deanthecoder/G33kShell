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
    private GameState m_gameState;
    private int m_totalMoves;
    private int m_totalScore;
    private int m_lives = StartingLives;
    private int m_starvationDeaths;
    
    public Snake Snake { get; private set; }
    public IntPoint FoodPosition { get; private set; }
    public int HighScore { get; private set; }
    public int Score { get; private set; }

    /// <summary>
    /// Scores brains mostly on food collected, with a smaller bonus for peak run quality and
    /// efficiency, and a penalty for repeated starvation resets.
    /// </summary>
    public override double Rating
    {
        get
        {
            if (m_totalScore == 0)
                return 0;

            var efficiency = (double)m_totalScore / Math.Max(1, m_totalMoves);
            return m_totalScore * 8.0 +
                   HighScore * 3.0 +
                   efficiency * 400.0 -
                   m_starvationDeaths * 2.0;
        }
    }

    public override bool IsGameOver =>
        m_lives== 0 && Snake.IsDead;

    public override IEnumerable<(string Name, string Value)> ExtraGameStats()
    {
        yield return ("HighScore", HighScore.ToString());
        yield return ("TotalScore", m_totalScore.ToString());
        yield return ("Moves", m_totalMoves.ToString());
        yield return ("Efficiency", ((double)m_totalScore / Math.Max(1, m_totalMoves)).ToString("F4"));
    }

    public Game(int arenaWidth, int arenaHeight, AiBrainBase brain, bool limitLives = true) : base(arenaWidth, arenaHeight, brain)
    {
        m_limitLives = limitLives;
    }

    /// <summary>
    /// Resets the current life while keeping long-run training totals so a brain is judged over
    /// several short attempts rather than a single lucky path.
    /// </summary>
    public override AiGameBase ResetGame()
    {
        Brain.ResetTemporalState();
        Snake ??= new Snake(ArenaWidth, ArenaHeight);
        Snake.Reset();
        m_gameState ??= new GameState(Snake, FoodPosition);
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
            if (Snake.StepsSinceFood >= Snake.TotalStepsToStarvation)
                m_starvationDeaths++;

            if (m_limitLives && --m_lives <= 0)
                return;
            
            ResetGame();
        }

        m_gameState.Reset(Snake, FoodPosition);
        var newDirection = ((Brain)Brain).ChooseMove(m_gameState);
        if (Snake.Move(newDirection, FoodPosition))
        {
            Score++;
            m_totalScore++;
            HighScore = Math.Max(HighScore, Score);
            SpawnFood();
        }

        m_totalMoves++;
    }
}
