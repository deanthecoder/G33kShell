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
using CSharp.Core.Extensions;
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
    private const int PopulationSize = 100;

    protected AiGameBase[] m_games;

    protected int ArenaWidth { get; }
    protected int ArenaHeight { get; }

    protected abstract void DrawGame(ScreenData screen, AiGameBase game);

    protected AiGameCanvasBase(int width, int height, int targetFps = 30) : base(width, height, targetFps)
    {
        ArenaWidth = width;
        ArenaHeight = height;
    }

    [UsedImplicitly]
    protected void TrainAi(ScreenData screen, Action<byte[]> saveBrainBytes, Func<AiBrainBase> createBrain)
    {
        m_games ??= Enumerable.Range(0, PopulationSize).Select(_ => CreateGameWithSeed(m_generation)).ToArray();

        m_games.AsParallel().ForAll(o =>
        {
            while (!o.IsGameOver)
                o.Tick();
        });

        DrawGame(screen, m_games[0]);
        
        // Select the breeders.
        var orderedGames = m_games.OrderByDescending(o => o.Rating).ToArray();
        var gameCount = orderedGames.Length;
        var eliteGames = orderedGames.Take((int)(gameCount * 0.1)).ToArray();
        var losers = orderedGames.Except(eliteGames);
        var luckyLosers = losers.OrderBy(_ => m_rand.Next()).Take((int)(gameCount * 0.05)).ToArray();
        
        // Report summary of results.
        m_generation++;
        var veryBest = eliteGames[0];
        var stats = $"Gen {m_generation}, MaxRating: {veryBest.Rating:F1}";
        var extraStats = veryBest.ExtraGameStats().Select(o => $" {o.Name}: {o.Value}").ToArray().ToCsv().Trim();
        if (!string.IsNullOrEmpty(extraStats))
            stats += $", {extraStats}";
        System.Console.WriteLine(stats);

        // Persist brain improvements.
        if (veryBest.Rating > m_savedRating)
        {
            m_savedRating = veryBest.Rating * 1.05;
            System.Console.WriteLine("Saved.");
            saveBrainBytes(veryBest.Brain.Save());
        }

        // Build the brains for the next generation.
        var nextBrains = new List<AiBrainBase>(m_games.Length);
        
        // Best brains get a free pass.
        nextBrains.AddRange(eliteGames.Select(o => o.Brain));
        nextBrains.AddRange(eliteGames.Select(o => createBrain().InitWithNudgedWeights(o.Brain)));

        // Lucky losers get to survive too.
        nextBrains.AddRange(luckyLosers.Select(o => createBrain().InitWithNudgedWeights(o.Brain)));
            
        // Spawn some randoms.
        nextBrains.AddRange(Enumerable.Range(0, (int)(gameCount * 0.1)).Select(_ => createBrain()));
            
        // Best brains get to be parents.
        var breeders = nextBrains.ToArray();
        while (nextBrains.Count < PopulationSize)
        {
            var mumBrain = breeders[m_rand.Next(breeders.Length)];
            var dadBrain = breeders[m_rand.Next(breeders.Length)];
            var childBrain = m_rand.Next(2) switch
            {
                0 => createBrain().InitWithAveraged(mumBrain, dadBrain),
                1 => createBrain().InitWithMixed(mumBrain, dadBrain),
                _ => throw new InvalidOperationException("Unknown brain merge mode.")
            };

            nextBrains.Add(childBrain.NudgeWeights());
        }
        
        // Make the next generation of games.
        m_games = nextBrains.Select(o =>
        {
            var newGame = CreateGameWithSeed(m_generation);
            newGame.Brain = o;
            return newGame;
        }).ToArray();
    }

    private AiGameBase CreateGameWithSeed(int seed)
    {
        var game = CreateGame();
        game.GameRand = new Random(seed);
        game.ResetGame();
        return game;
    }

    protected abstract AiGameBase CreateGame();
}