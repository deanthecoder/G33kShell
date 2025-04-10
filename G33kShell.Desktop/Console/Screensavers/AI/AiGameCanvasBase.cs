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
using System.Linq;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers.AI;

/// <summary>
/// Base class for AI-powered games.
/// </summary>
public abstract class AiGameCanvasBase : ScreensaverBase
{
    private readonly Random m_rand = new Random();
    private int m_generation;
    private double m_savedRating;
    private const int PopulationSize = 200;

    protected List<AiGameBase> m_games;

    protected int ArenaWidth { get; }
    protected int ArenaHeight { get; }

    protected abstract void DrawGame(ScreenData screen, AiGameBase game);

    protected AiGameCanvasBase(int width, int height, int targetFps = 30) : base(width, height, targetFps)
    {
        ArenaWidth = width;
        ArenaHeight = height;
    }

    [UsedImplicitly]
    protected void TrainAi(ScreenData screen, AiGameBase game, Action<byte[]> saveBrainBytes)
    {
        m_games ??= Enumerable.Range(0, PopulationSize).Select(_ => CreateGame()).ToList();

        m_games.AsParallel().ForAll(o =>
        {
            while (!o.IsGameOver)
                o.Tick();
        });

        DrawGame(screen, m_games[0]);
        
        var isAllGamesEnded = m_games.All(o => o.IsGameOver);
        if (!isAllGamesEnded)
            return;
        
        // Select the breeders.
        var orderedGames = m_games.OrderByDescending(o => o.Rating).ToArray();
        var gameCount = orderedGames.Length;
        var bestGames = orderedGames.Take((int)(gameCount * 0.1)).ToArray();
        var losers = orderedGames.Except(bestGames);
        var luckyLosers = losers.OrderBy(_ => m_rand.Next()).Take((int)(gameCount * 0.05)).ToArray();
        
        // Report summary of results.
        m_generation++;
        var veryBest = bestGames[0];
        System.Console.WriteLine($"Gen {m_generation}, Rating: {veryBest.Rating:F2}, Range: {bestGames.Min(o => o.Rating):F1} -> {bestGames.Max(o => o.Rating):F1}");

        if (veryBest.Rating > m_savedRating && veryBest.Rating > 100)
        {
            m_savedRating = veryBest.Rating * 1.05;
            System.Console.WriteLine("Saved.");
            saveBrainBytes(veryBest.Brain.Save());
        }

        // Build the games for the next generation.
        m_games.Clear();
        
        // Best brains get a free pass.
        m_games.AddRange(bestGames);

        // Lucky losers get to survive too.
        m_games.AddRange(luckyLosers);
            
        // Spawn some randoms.
        m_games.AddRange(Enumerable.Range(0, (int)(gameCount * 0.2)).Select(_ => CreateGame()));
            
        // Best games get to be parents.
        while (m_games.Count < PopulationSize)
        {
            var mum = bestGames[m_rand.Next(bestGames.Length)];
            var dad = bestGames[m_rand.Next(bestGames.Length)];
            m_games.Add(mum.MergeWith(dad));
        }
        
        // ...and go again...
        m_games = m_games.Select(o => o.Resurrect()).ToList();
    }

    protected abstract AiGameBase CreateGame();
}