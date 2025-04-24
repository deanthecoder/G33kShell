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
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers.AI;

public abstract class AiGameBase
{
    protected int ArenaWidth { get; }
    protected int ArenaHeight { get; }
    
    public AiBrainBase Brain { get; set; }

    /// <summary>
    /// Gets the AI's performance score for this game.
    /// </summary>
    public abstract double Rating { get; }

    public abstract bool IsGameOver { get; }
    
    /// <summary>
    /// Random number generator for the game to use for random events.
    /// </summary>
    /// <remarks>
    /// Multiple game instances might share the same seed (and hence have the same gameplay)
    /// when part of the same training 'generation'.
    /// </remarks>
    public Random GameRand { get; set; } = new Random();

    public abstract IEnumerable<(string Name, string Value)> ExtraGameStats();

    protected AiGameBase(int arenaWidth, int arenaHeight, [NotNull] AiBrainBase brain)
    {
        ArenaWidth = arenaWidth;
        ArenaHeight = arenaHeight;
        Brain = brain ?? throw new ArgumentNullException(nameof(brain));
    }

    /// <summary>
    /// Advances the game state by one tick or frame.
    /// </summary>
    public abstract void Tick();
    
    /// <summary>
    /// Reset all game state back to the same initial conditions.
    /// </summary>
    public abstract AiGameBase ResetGame();
}