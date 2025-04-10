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

namespace G33kShell.Desktop.Console.Screensavers.AI;

public abstract class AiGameBase
{
    protected readonly int m_arenaWidth;
    protected readonly int m_arenaHeight;
    protected readonly Random m_rand = new Random();
    
    public AiBrainBase Brain { get; set; }

    /// <summary>
    /// Gets the AI's performance score for this game.
    /// </summary>
    public abstract double Rating { get; }

    public abstract bool IsGameOver { get; }

    protected AiGameBase(int arenaWidth, int arenaHeight, AiBrainBase brain)
    {
        m_arenaWidth = arenaWidth;
        m_arenaHeight = arenaHeight;
        Brain = brain;
    }

    /// <summary>
    /// Advances the game state by one tick or frame.
    /// </summary>
    public abstract void Tick();
    
    /// <summary>
    /// Merges this game with another to produce a new game.
    /// </summary>
    public AiGameBase MergeWith(AiGameBase other)
    {
        var newGame = CreateGame(m_arenaWidth, m_arenaHeight);
        newGame.Brain = CloneBrain();
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

    /// <summary>
    /// Creates a new game using the same AI brain.
    /// </summary>
    public AiGameBase Resurrect()
    {
        var game = CreateGame(m_arenaWidth, m_arenaHeight);
        game.Brain = Brain; // New game, but keep the old bat brain.
        return game;
    }

    protected abstract AiGameBase CreateGame(int arenaWidth, int arenaHeight);
    protected abstract AiBrainBase CloneBrain();

    public void LoadBrainData(byte[] brainBytes) => Brain.Load(brainBytes);
}