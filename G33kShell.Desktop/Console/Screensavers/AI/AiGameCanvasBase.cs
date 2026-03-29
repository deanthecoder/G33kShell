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
using DTC.Core;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers.AI;

/// <summary>
/// Base class for AI-powered games that train via simple neuroevolution.
/// </summary>
/// <remarks>
/// Training does not use gradient descent against a replay buffer. Instead, each generation
/// evaluates a population of brains by letting them play several seeded games, ranking them
/// by the game's <see cref="AiGameBase.Rating"/>, then building the next generation from:
/// cloned elite "GOAT" brains, a small slice of fresh random brains, and crossover/mutation
/// children sampled from the best performers. The best serialized brain is persisted so a
/// screensaver can resume from the strongest known policy on the next run.
/// </remarks>
public abstract class AiGameCanvasBase : ScreensaverBase
{
    private const int DefaultInitialPopSize = 160;
    private const int DefaultMinPopSize = 80;
    private const int MaxGoatBrains = 5;
    private const int DefaultGamesPerBrain = 4;
    
    private readonly List<(double Rating, AiBrainBase Brain)> m_goatBrains = new List<(double Rating, AiBrainBase Brain)>(MaxGoatBrains);
    private int m_generation;
    private double m_savedRating;
    private int m_generationsSinceImprovement;
    private int m_currentPopSize;
    private Task m_trainingTask;
    private bool m_stopTraining;
    private List<AiBrainBase> m_nextGenBrains;
    private Random m_breedingRandom;

    protected int ArenaWidth { get; }
    protected int ArenaHeight { get; }

    protected AiGameCanvasBase(int width, int height, int targetFps = 30) : base(width, height, targetFps)
    {
        ArenaWidth = width;
        ArenaHeight = height;
    }

    /// <summary>
    /// Starts the background evolutionary training loop for the current screensaver.
    /// </summary>
    /// <remarks>
    /// The on-screen output only shows a spinner; all useful progress data is written to the
    /// console log as generation summaries. Improvements are saved through <paramref name="saveBrainBytes"/>
    /// whenever a generation beats the best persisted rating.
    /// </remarks>
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
        var brain = CreateBrain();
        System.Console.WriteLine($"Brain layers: {brain.InputSize} : {brain.HiddenLayers.ToCsv(' ')} : {brain.OutputSize}");

        m_currentPopSize = GetInitialPopulationSize();
        var breedingSeed = GetBreedingRandomSeed();
        m_breedingRandom = breedingSeed.HasValue ? new Random(breedingSeed.Value) : null;
        
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
        m_nextGenBrains ??= CreateInitialPopulation().ToList();
        m_generation++;
        var gamesPerBrain = GetGamesPerBrain();
        
        var gameResults = new (double AverageRating, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats)[m_nextGenBrains.Count];
        Parallel.For(0, gameResults.Length, i =>
        {
            try
            {
                // Play a few games.
                var brain = m_nextGenBrains[i];
                var totalRating = 0.0;
                var bestRating = double.MinValue;
                var bestGameSeed = 0;
                var bestGameStats = string.Empty;

                for (var gameIndex = 0; gameIndex < gamesPerBrain; gameIndex++)
                {
                    var seed = GetTrainingSeed(m_generation, i, gameIndex);
                    var game = CreateGameWithSeed(seed, brain);
                    while (!game.IsGameOver && !m_stopTraining)
                        game.Tick();

                    totalRating += game.Rating;
                    if (game.Rating <= bestRating)
                        continue;

                    bestRating = game.Rating;
                    bestGameSeed = seed;
                    bestGameStats = game.ExtraGameStats().Select(o => $"{o.Name} {o.Value}").ToCsv('|');
                }

                var averageRating = totalRating / gamesPerBrain;
                gameResults[i] = (averageRating, bestRating, bestGameSeed, brain, bestGameStats);
            }
            catch (Exception e)
            {
                Logger.Instance.Exception("Training failed.", e);
            }
        });

        // Select the breeders.
        var orderedGames = gameResults.OrderByDescending(o => o.AverageRating).ToArray();
        var theBest = orderedGames[0];

        // Report summary of results.
        var stats = $"Gen {m_generation}|Pop {m_currentPopSize}|GOAT {m_savedRating:F1}|Rating {theBest.AverageRating:F1}|Seed {theBest.GameSeed}";
        if (!string.IsNullOrEmpty(theBest.GameStats))
            stats += $"|{theBest.GameStats}";
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

        PersistBrainImprovement(theBest, saveBrainBytes);

        // Build the brains for the next generation.
        m_nextGenBrains = UseHarnessStyleEvolution()
            ? BuildHarnessStyleNextGeneration(orderedGames)
            : BuildLegacyNextGeneration(orderedGames);
    }

    private void PersistBrainImprovement((double AverageRating, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats) theBest, Action<byte[]> saveBrainBytes)
    {
        if (theBest.AverageRating > m_savedRating)
        {
            m_savedRating = theBest.AverageRating;
            System.Console.WriteLine("Saved.");
            saveBrainBytes(theBest.Brain.Save());

            m_generationsSinceImprovement = 0;
            return;
        }

        m_generationsSinceImprovement++;
    }

    private List<AiBrainBase> BuildLegacyNextGeneration((double AverageRating, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats)[] orderedGames)
    {
        m_currentPopSize = Math.Max(m_currentPopSize - 1, GetMinPopulationSize());
        if (m_generationsSinceImprovement >= 100)
        {
            m_generationsSinceImprovement = 0;
            m_currentPopSize = GetInitialPopulationSize();
            System.Console.WriteLine("Stagnation detected - Increasing population size.");
        }

        var nextBrains = new List<AiBrainBase>(orderedGames.Length);
        nextBrains.AddRange(m_goatBrains.Select(o => o.Brain.Clone()));

        // Spawn 5% pure randoms.
        nextBrains.AddRange(Enumerable.Range(0, (int)(m_currentPopSize * GetRandomFraction())).Select(_ => CreateBrain()));
            
        // Elite get to be parents.
        var breeders = orderedGames.Select(o => (o.AverageRating, o.Brain)).ToList();
        breeders.AddRange(m_goatBrains);
        while (nextBrains.Count < m_currentPopSize)
        {
            var mumBrain = Random.Shared.RouletteSelection(breeders, o => o.AverageRating).Brain;
            var dadBrain = Random.Shared.RouletteSelection(breeders, o => o.AverageRating).Brain;
            var childBrain = mumBrain.Clone().CrossWith(dadBrain, GetCrossoverRate()).Mutate(GetMutationRate());
            nextBrains.Add(childBrain);
        }

        return nextBrains;
    }

    private List<AiBrainBase> BuildHarnessStyleNextGeneration((double AverageRating, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats)[] orderedGames)
    {
        var nextBrains = new List<AiBrainBase>(m_currentPopSize);
        var eliteCount = Math.Min(GetEliteCount(), orderedGames.Length);
        for (var i = 0; i < eliteCount; i++)
            nextBrains.Add(orderedGames[i].Brain.Clone());

        var randomCount = Math.Max(1, (int)(m_currentPopSize * GetRandomFraction()));
        for (var i = 0; i < randomCount && nextBrains.Count < m_currentPopSize; i++)
            nextBrains.Add(CreateBrain());

        while (nextBrains.Count < m_currentPopSize)
        {
            var mumBrain = SelectParent(orderedGames);
            var dadBrain = SelectParent(orderedGames);
            var childBrain = mumBrain.Clone().CrossWith(dadBrain, GetCrossoverRate()).Mutate(GetMutationRate());
            nextBrains.Add(childBrain);
        }

        return nextBrains;
    }

    private AiBrainBase SelectParent((double AverageRating, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats)[] orderedGames)
    {
        var totalFitness = 0.0;
        for (var i = 0; i < orderedGames.Length; i++)
            totalFitness += Math.Max(1.0, orderedGames[i].AverageRating);

        var random = m_breedingRandom;
        var target = (random?.NextDouble() ?? Random.Shared.NextDouble()) * totalFitness;
        var cumulative = 0.0;
        for (var i = 0; i < orderedGames.Length; i++)
        {
            cumulative += Math.Max(1.0, orderedGames[i].AverageRating);
            if (target <= cumulative)
                return orderedGames[i].Brain;
        }

        return orderedGames[^1].Brain;
    }

    private AiGameBase CreateGameWithSeed(int seed, AiBrainBase brain = null)
    {
        var game = CreateTrainingGame(brain ?? CreateBrain());
        game.GameRand = new Random(seed);
        game.ResetGame();
        return game;
    }

    protected virtual IEnumerable<AiBrainBase> CreateInitialPopulation()
    {
        var initialPopulationSize = GetInitialPopulationSize();
        var brains = new List<AiBrainBase>(initialPopulationSize);
        var savedBrainBytes = GetSavedBrainBytes();
        if (savedBrainBytes != null && savedBrainBytes.Length > 0)
            brains.Add(CreateBrain().Load(savedBrainBytes));

        while (brains.Count < initialPopulationSize)
            brains.Add(CreateBrain());

        return brains;
    }

    /// <summary>
    /// Allows a game to pin training to a fixed arena rather than the user's current terminal size.
    /// </summary>
    /// <remarks>
    /// Snake uses this so in-app training is comparable to its tuning runs, while on-screen play
    /// still uses the live terminal dimensions.
    /// </remarks>
    protected virtual AiGameBase CreateTrainingGame(AiBrainBase brain) => CreateGame(brain);

    /// <summary>
    /// Returns the seed for one candidate game during training.
    /// </summary>
    /// <remarks>
    /// The default remains stochastic. Games can override this to make training runs easier to
    /// compare from one code change to the next.
    /// </remarks>
    protected virtual int GetTrainingSeed(int generation, int brainIndex, int gameIndex) => Random.Shared.Next(10000);

    protected virtual int GetInitialPopulationSize() => DefaultInitialPopSize;
    protected virtual int GetMinPopulationSize() => DefaultMinPopSize;
    protected virtual int GetGamesPerBrain() => DefaultGamesPerBrain;
    protected virtual int GetEliteCount() => MaxGoatBrains;
    protected virtual double GetRandomFraction() => 0.05;
    protected virtual double GetCrossoverRate() => 0.5;
    protected virtual double GetMutationRate() => 0.05;
    protected virtual bool UseHarnessStyleEvolution() => false;
    protected virtual int? GetBreedingRandomSeed() => null;
    protected virtual byte[] GetSavedBrainBytes() => null;
    protected abstract AiGameBase CreateGame(AiBrainBase brain);
    protected abstract AiBrainBase CreateBrain();
}
