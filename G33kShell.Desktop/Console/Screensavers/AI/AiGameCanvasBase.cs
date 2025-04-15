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
using CSharp.Core.AI;
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
    private const int InitialPopSize = 300;
    private const int MinPopSize = 80;
    private int m_generationsSinceImprovement;
    private int m_currentPopSize = InitialPopSize;

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
        m_games ??= Enumerable.Range(0, m_currentPopSize).Select(_ => CreateGameWithSeed(m_generation)).ToArray();

        m_games.AsParallel().ForAll(o =>
        {
            while (!o.IsGameOver)
                o.Tick();
        });

        DrawGame(screen, m_games[0]);
        
        // Select the breeders.
        var orderedGames = m_games.OrderByDescending(o => o.Rating).ToArray();
        var eliteGames = orderedGames.Take((int)(m_currentPopSize * 0.1)).ToArray();
        
        // Report summary of results.
        m_generation++;
        var veryBest = eliteGames[0];
        var stats = $"Gen {m_generation} | Pop {m_currentPopSize} | GOAT {m_savedRating:F1} | Best {veryBest.Rating:F1}";
        var extraStats = veryBest.ExtraGameStats().Select(o => $" {o.Name}: {o.Value}").ToArray().ToCsv().Trim();
        if (!string.IsNullOrEmpty(extraStats))
            stats += $" | {extraStats}";
        System.Console.WriteLine(stats);

        // Persist brain improvements.
        if (veryBest.Rating > m_savedRating)
        {
            m_savedRating = veryBest.Rating;
            System.Console.WriteLine("Saved.");
            saveBrainBytes(veryBest.Brain.Save());

            m_generationsSinceImprovement = 0;
        }
        else
        {
            m_generationsSinceImprovement++;
            
            m_currentPopSize = Math.Max(m_currentPopSize - 2, MinPopSize);
            if (m_generationsSinceImprovement >= 100)
            {
                m_generationsSinceImprovement = 0;
                m_currentPopSize = InitialPopSize;
                System.Console.WriteLine("Stagnation detected — Perturbing entire population.");
            }
        }

        // Build the brains for the next generation.
        var nextBrains = new List<AiBrainBase>(m_games.Length);
        
        // Elite 10% brains get a free pass.
        nextBrains.AddRange(eliteGames.Select(o => o.Brain));
        
        // ...and again with a small variation, (10%)
        nextBrains.AddRange(eliteGames.Select(o => createBrain().InitWithNudgedWeights(o.Brain, NeuralNetwork.NudgeFactor.Low)));

        // Spawn 5% pure randoms.
        nextBrains.AddRange(Enumerable.Range(0, (int)(m_currentPopSize * 0.05)).Select(_ => createBrain()));
            
        // Elite get to be parents.
        var breeders = eliteGames.Select(o => o.Brain).ToList();
        var minBreederCount = Math.Max(5, breeders.Count * 0.25);
        while (nextBrains.Count < m_currentPopSize)
        {
            var mumBrain = breeders[m_rand.Next(breeders.Count)];
            var dadBrain = breeders[m_rand.Next(breeders.Count)];
            var childBrain = m_rand.Next(0, 4) switch
            {
                0 => createBrain().InitWithLerp(mumBrain, dadBrain, 0.5),
                1 => createBrain().InitWithLerp(mumBrain, dadBrain, m_rand.NextDouble()),
                2 => createBrain().InitWithSpliced(mumBrain, dadBrain),
                3 => createBrain().InitWithSpliced(mumBrain, dadBrain).NudgeWeights(NeuralNetwork.NudgeFactor.Low),
                _ => throw new ArgumentOutOfRangeException()
            };

            nextBrains.Add(childBrain);
            
            // Reduce the set size to eliminate the worst breeder.
            if (breeders.Count > minBreederCount)
                breeders.RemoveAt(breeders.Count - 1);
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