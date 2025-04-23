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
using System.Threading.Tasks;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers.AI;

/// <summary>
/// Base class for AI-powered games.
/// </summary>
public abstract class AiGameCanvasBase : ScreensaverBase
{
    private const int InitialPopSize = 300;
    private const int MinPopSize = 150;
    private const int MaxGoatBrains = 5;
    
    private readonly List<(double Rating, AiBrainBase Brain)> m_goatBrains = new List<(double Rating, AiBrainBase Brain)>(MaxGoatBrains);
    private int m_generation;
    private double m_savedRating;
    private int m_generationsSinceImprovement;
    private int m_currentPopSize = InitialPopSize;
    private Task m_trainingTask;
    private bool m_stopTraining;
    private List<AiBrainBase> m_nextGenBrains;

    protected int ArenaWidth { get; }
    protected int ArenaHeight { get; }

    protected AiGameCanvasBase(int width, int height, int targetFps = 30) : base(width, height, targetFps)
    {
        ArenaWidth = width;
        ArenaHeight = height;
    }

    [UsedImplicitly]
    protected void TrainAi(ScreenData screen, Action<byte[]> saveBrainBytes)
    {
        const string animChars = "/-\\|";
        var animFrame = Environment.TickCount64 / 100 % animChars.Length;
        screen.PrintAt(0, 0, $"Training... {animChars[(int)animFrame]}");
        
        if (m_trainingTask != null)
        {
            // We're already training - Do nothing.
            return;
        }
        
        System.Console.WriteLine("Starting training...");
        var brain = CreateGame().Brain;
        System.Console.WriteLine($"Brain layers: {brain.InputSize} : {brain.HiddenLayers.ToCsv(' ')} : {brain.OutputSize}");
        
        m_stopTraining = false;
        m_trainingTask = Task.Run(() =>
        {
            while (!m_stopTraining)
                TrainAiImpl(saveBrainBytes);
            
            System.Console.WriteLine("Training complete.");
            System.Console.WriteLine("Summary:");
            System.Console.WriteLine($"  Brain layers: {brain.InputSize} : {brain.HiddenLayers.ToCsv(' ')} : {brain.OutputSize}");
            System.Console.WriteLine($"   Generations: {m_generation}");
            System.Console.WriteLine($"        Rating: {m_savedRating:F1}");
        });
    }

    public override void StopScreensaver()
    {
        base.StopScreensaver();
        
        m_stopTraining = true;
    }

    private void TrainAiImpl(Action<byte[]> saveBrainBytes)
    {
        m_nextGenBrains ??= Enumerable.Range(0, InitialPopSize).Select(_ => CreateGame().Brain).ToList();
        
        var games = new (double AverageRating, AiGameBase Game, AiBrainBase Brain)[m_nextGenBrains.Count];
        Parallel.For(0, games.Length, i =>
        {
            // Play the base game.
            var baseGame = CreateGameWithSeed(m_generation);
            while (!baseGame.IsGameOver && !m_stopTraining)
                baseGame.Tick();

            var totalRating = baseGame.Rating;
            if (baseGame.Rating > 0.0)
            {
                // Play several more games.
                var gameCount = 1;
                for (var trial = 0; trial < 4 && !m_stopTraining; trial++, gameCount++)
                {
                    var game = CreateGameWithSeed(Random.Shared.Next());
                    game.Brain = baseGame.Brain;
                    while (!game.IsGameOver)
                        game.Tick();
                    totalRating += game.Rating;
                    if (game.Rating <= 0.0001)
                        break; // No score, no point in continuing.
                }

                totalRating /= gameCount;
            }

            games[i] = (totalRating, baseGame, baseGame.Brain);
        });

        // Select the breeders.
        var orderedGames = games.OrderByDescending(o => o.AverageRating).ToArray();
        var theBest = orderedGames[0];

        // Report summary of results.
        m_generation++;
        var stats = $"Gen {m_generation}|Pop {m_currentPopSize}|Rating {theBest.AverageRating:F1}|GOAT {m_savedRating:F1}";
        var extraStats = theBest.Game.ExtraGameStats().Select(o => $" {o.Name}: {o.Value}").ToArray().ToCsv().Trim();
        if (!string.IsNullOrEmpty(extraStats))
            stats += $"|{extraStats}";
        System.Console.WriteLine(stats);
        
        // Remember the GOAT brains.
        var worstGoatRating = m_goatBrains.Count > 0 ? m_goatBrains.FastFindMin(o => o.Rating).Rating : 0.0;
        for (var i = 0; i < orderedGames.Length; i++)
        {
            if (orderedGames[i].AverageRating > worstGoatRating)
                m_goatBrains.Add((orderedGames[i].AverageRating, orderedGames[i].Brain));
        }

        while (m_goatBrains.Count > MaxGoatBrains)
        {
            var toRemove = m_goatBrains.FastFindMin(o => o.Rating);
            m_goatBrains.Remove(toRemove);
        }

        // Persist brain improvements.
        if (theBest.AverageRating > m_savedRating)
        {
            m_savedRating = theBest.AverageRating;
            System.Console.WriteLine("Saved.");
            saveBrainBytes(theBest.Brain.Save());

            m_generationsSinceImprovement = 0;
        }
        else
        {
            m_generationsSinceImprovement++;
            
            m_currentPopSize = Math.Max(m_currentPopSize - 1, MinPopSize);
            if (m_generationsSinceImprovement >= 100)
            {
                m_generationsSinceImprovement = 0;
                m_currentPopSize = InitialPopSize;
                System.Console.WriteLine("Stagnation detected - Increasing population size.");
            }
        }

        // Build the brains for the next generation.
        var nextBrains = new List<AiBrainBase>(games.Length);
        nextBrains.AddRange(m_goatBrains.Select(o => o.Brain.Clone()));

        // Spawn 5% pure randoms.
        nextBrains.AddRange(Enumerable.Range(0, (int)(m_currentPopSize * 0.05)).Select(_ => CreateGameWithSeed(0).Brain.Clone().Randomize()));
            
        // Elite get to be parents.
        var breeders = orderedGames.Select(o => (o.AverageRating, o.Brain)).ToList();
        breeders.AddRange(m_goatBrains);
        while (nextBrains.Count < m_currentPopSize)
        {
            var mumBrain = Random.Shared.RouletteSelection(breeders, o => o.AverageRating).Brain;
            var dadBrain = Random.Shared.RouletteSelection(breeders, o => o.AverageRating).Brain;
            var childBrain = mumBrain.Clone().CrossWith(dadBrain, 0.5).Mutate(0.05);
            nextBrains.Add(childBrain);
        }
        
        // Make the next generation of games.
        m_nextGenBrains = nextBrains;
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