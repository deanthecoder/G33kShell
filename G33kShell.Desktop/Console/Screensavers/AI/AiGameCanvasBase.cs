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
    private int m_generation;
    private double m_savedRating;
    private const int InitialPopSize = 300;
    private const int MinPopSize = 150;
    private int m_generationsSinceImprovement;
    private int m_currentPopSize = InitialPopSize;
    private Task m_trainingTask;
    private bool m_stopTraining;

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
        
        m_stopTraining = false;
        m_trainingTask = Task.Run(() =>
        {
            while (!m_stopTraining)
                TrainAiImpl(saveBrainBytes);
        });
    }

    public override void StopScreensaver()
    {
        base.StopScreensaver();
        
        m_stopTraining = true;
    }

    private void TrainAiImpl(Action<byte[]> saveBrainBytes)
    {
        m_games ??= Enumerable.Range(0, m_currentPopSize).Select(_ => CreateGameWithSeed(m_generation)).ToArray();
        
        Parallel.For(0, m_games.Length, i =>
        {
            var game = m_games[i];
            while (!game.IsGameOver)
                game.Tick();
        });

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
        AiBrainBase goatBrain = null;
        var increaseMutation = false;
        if (veryBest.Rating > m_savedRating)
        {
            m_savedRating = veryBest.Rating;
            System.Console.WriteLine("Saved.");
            saveBrainBytes(veryBest.Brain.Save());
            goatBrain = veryBest.Brain.Clone();

            m_generationsSinceImprovement = 0;
        }
        else
        {
            m_generationsSinceImprovement++;
            
            m_currentPopSize = Math.Max(m_currentPopSize - 2, MinPopSize);
            // if (m_generationsSinceImprovement >= 100)
            // {
            //     m_generationsSinceImprovement = 0;
            //     m_currentPopSize = InitialPopSize;
            //     //increaseMutation = true;
            //     System.Console.WriteLine("Stagnation detected — Increasing population size.");
            // }
        }

        // Build the brains for the next generation.
        var nextBrains = new List<AiBrainBase>(m_games.Length);
        
        // The GOAT lives on.
        if (goatBrain != null)
            nextBrains.Add(goatBrain.Clone());

        // Elite 10% brains get a free pass.
        nextBrains.AddRange(eliteGames.Select(o => o.Brain.Clone()));
        
        // ...and 10% more with a small mutation.
        nextBrains.AddRange(eliteGames.Select(o => o.Brain.Clone().Mutate(0.02)));

        if (increaseMutation)
        {
            // Fill 50% of the population with more mutations.
            nextBrains.AddRange(orderedGames.Take(orderedGames.Length / 2).Select(o => o.Brain.Clone().Mutate(0.5)));
        }

        // Spawn 5% pure randoms.
        nextBrains.AddRange(Enumerable.Range(0, (int)(m_currentPopSize * 0.05)).Select(_ => veryBest.Brain.Clone().Randomize()));
            
        // Elite get to be parents.
        while (nextBrains.Count < m_currentPopSize)
        {
            var mumBrain = Random.Shared.RouletteSelection(m_games, o => o.Rating).Brain;
            var dadBrain = Random.Shared.RouletteSelection(m_games, o => o.Rating).Brain;
            var childBrain = mumBrain.Clone().CrossWith(dadBrain, 0.5).Mutate(0.05);
            nextBrains.Add(childBrain);
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