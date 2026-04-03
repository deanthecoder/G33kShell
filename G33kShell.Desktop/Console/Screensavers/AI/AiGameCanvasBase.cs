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
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
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
/// by the game's <see cref="AiGameBase.Rating"/>, then validating the strongest candidates
/// on a separate fixed seed set before building the next generation from:
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
    private const int DefaultValidationGamesPerBrain = 4;
    private const int DefaultValidationCandidateCount = 3;
    
    private readonly List<(double Rating, AiBrainBase Brain)> m_goatBrains = new List<(double Rating, AiBrainBase Brain)>(MaxGoatBrains);
    private int m_generation;
    private double m_savedRating;
    private int m_generationsSinceImprovement;
    private int m_explorationBoostGenerationsRemaining;
    private int m_currentPopSize;
    private string m_lastExplorationReason;
    private Task m_trainingTask;
    private bool m_stopTraining;
    private List<AiBrainBase> m_nextGenBrains;
    private Random m_breedingRandom;
    private readonly ParallelOptions m_parallelOptions = new ParallelOptions();
    private readonly object m_bestObservedMetricLock = new object();
    private (string Name, double Value, string Format)? m_bestObservedMetric;
    private long m_lastGoatImprovementTimestamp;

    protected int ArenaWidth { get; }
    protected int ArenaHeight { get; }

    protected AiGameCanvasBase(int width, int height, int targetFps = 30) : base(width, height, targetFps)
    {
        ArenaWidth = width;
        ArenaHeight = height;
        m_parallelOptions.MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1);
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
        screen.PrintAt(0, 1, $"Generation: {m_generation}");
        var trainingStatus = GetTrainingStatusText(m_generation);
        if (!string.IsNullOrWhiteSpace(trainingStatus))
            screen.PrintAt(0, 2, trainingStatus);
        var bestObservedMetric = GetBestObservedMetricHudText();
        var nextHudLine = string.IsNullOrWhiteSpace(trainingStatus) ? 2 : 3;
        if (!string.IsNullOrWhiteSpace(bestObservedMetric))
        {
            screen.PrintAt(0, nextHudLine, bestObservedMetric);
            nextHudLine++;
        }

        var goatAge = GetGoatAgeHudText();
        if (!string.IsNullOrWhiteSpace(goatAge))
            screen.PrintAt(0, nextHudLine, goatAge);
        
        if (m_trainingTask != null)
        {
            // We're already training - Do nothing.
            return;
        }
        
        System.Console.WriteLine(IsFreshTraining()
            ? "Starting training from a fresh brain..."
            : "Starting training...");
        var brain = CreateBrain();
        System.Console.WriteLine($"Brain layers: {brain.InputSize} : {brain.HiddenLayers.ToCsv(' ')} : {brain.OutputSize}");
        ThreadPool.GetMinThreads(out var currentMinWorkers, out var currentMinIo);
        var desiredMinWorkers = Math.Max(currentMinWorkers, m_parallelOptions.MaxDegreeOfParallelism);
        if (desiredMinWorkers != currentMinWorkers)
            ThreadPool.SetMinThreads(desiredMinWorkers, currentMinIo);

        m_currentPopSize = GetInitialPopulationSize();
        var breedingSeed = GetBreedingRandomSeed();
        m_breedingRandom = breedingSeed.HasValue ? new Random(breedingSeed.Value) : null;
        m_bestObservedMetric = null;
        m_lastGoatImprovementTimestamp = Stopwatch.GetTimestamp();
        
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
            System.Console.WriteLine($"  Since GOAT: {GetGoatAgeSeconds()}s");
            var bestObservedMetric = GetBestObservedMetricSummaryText();
            if (!string.IsNullOrEmpty(bestObservedMetric))
                System.Console.WriteLine(bestObservedMetric);
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
        var validationGamesPerBrain = GetValidationGamesPerBrain();
        
        var gameResults = new (double AverageRating, double AverageDegeneracy, string DegeneracyReason, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats)[m_nextGenBrains.Count];
        var trainingTotals = new double[m_nextGenBrains.Count];
        var trainingDegeneracyTotals = new double[m_nextGenBrains.Count];
        var trainingBestRatings = new double[m_nextGenBrains.Count];
        var trainingBestSeeds = new int[m_nextGenBrains.Count];
        var trainingBestStats = new string[m_nextGenBrains.Count];
        var trainingReasons = new string[m_nextGenBrains.Count];
        var trainingLocks = new object[m_nextGenBrains.Count];
        for (var i = 0; i < m_nextGenBrains.Count; i++)
        {
            trainingBestRatings[i] = double.MinValue;
            trainingLocks[i] = new object();
        }

        Parallel.For(0, m_nextGenBrains.Count * gamesPerBrain, m_parallelOptions, trainingJobIndex =>
        {
            var brainIndex = trainingJobIndex / gamesPerBrain;
            var gameIndex = trainingJobIndex % gamesPerBrain;
            try
            {
                var seed = GetTrainingSeed(m_generation, brainIndex, gameIndex);
                var evaluationBrain = m_nextGenBrains[brainIndex].Clone();
                var game = CreateGameWithSeed(seed, evaluationBrain, m_generation, false, brainIndex, gameIndex);
                while (!game.IsGameOver && !m_stopTraining)
                    game.Tick();

                UpdateBestObservedMetric(game.BestObservedMetric);
                var gameStats = game.ExtraGameStats().Select(o => $"{o.Name} {o.Value}").ToCsv('|');
                lock (trainingLocks[brainIndex])
                {
                    trainingTotals[brainIndex] += game.Rating;
                    trainingDegeneracyTotals[brainIndex] += game.DegeneracyScore;
                    if (string.IsNullOrEmpty(trainingReasons[brainIndex]) && !string.IsNullOrEmpty(game.DegeneracyReason))
                        trainingReasons[brainIndex] = game.DegeneracyReason;
                    if (game.Rating > trainingBestRatings[brainIndex])
                    {
                        trainingBestRatings[brainIndex] = game.Rating;
                        trainingBestSeeds[brainIndex] = seed;
                        trainingBestStats[brainIndex] = gameStats;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Instance.Exception("Training failed.", e);
            }
        });

        for (var i = 0; i < m_nextGenBrains.Count; i++)
        {
            gameResults[i] = (
                trainingTotals[i] / gamesPerBrain,
                trainingDegeneracyTotals[i] / gamesPerBrain,
                trainingReasons[i],
                trainingBestRatings[i],
                trainingBestSeeds[i],
                m_nextGenBrains[i],
                trainingBestStats[i] ?? string.Empty);
        }

        // Select the breeders.
        var orderedGames = gameResults.OrderByDescending(GetSelectionFitness).ToArray();
        var theBest = orderedGames[0];
        var bestTrainingFitness = GetSelectionFitness(theBest);
        var useTrainingScoreForGoat = UseTrainingScoreForGoat();
        var skippedValidation = useTrainingScoreForGoat || ShouldSkipValidation(bestTrainingFitness);
        var bestValidation = theBest;
        if (!useTrainingScoreForGoat && !skippedValidation)
        {
            var validationCandidateCount = Math.Min(GetValidationCandidateCount(), orderedGames.Length);
            var validationResults = new (double AverageRating, double AverageDegeneracy, string DegeneracyReason, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats)[validationCandidateCount];
            var validationTotals = new double[validationCandidateCount];
            var validationDegeneracyTotals = new double[validationCandidateCount];
            var validationBestRatings = new double[validationCandidateCount];
            var validationBestSeeds = new int[validationCandidateCount];
            var validationBestStats = new string[validationCandidateCount];
            var validationReasons = new string[validationCandidateCount];
            var validationLocks = new object[validationCandidateCount];
            for (var i = 0; i < validationCandidateCount; i++)
            {
                validationBestRatings[i] = double.MinValue;
                validationLocks[i] = new object();
            }

            Parallel.For(0, validationCandidateCount * validationGamesPerBrain, m_parallelOptions, validationJobIndex =>
            {
                var candidateIndex = validationJobIndex / validationGamesPerBrain;
                var gameIndex = validationJobIndex % validationGamesPerBrain;
                try
                {
                    var seed = GetValidationSeed(m_generation, candidateIndex, gameIndex);
                    var evaluationBrain = orderedGames[candidateIndex].Brain.Clone();
                    var game = CreateGameWithSeed(seed, evaluationBrain, m_generation, true, candidateIndex, gameIndex);
                    while (!game.IsGameOver && !m_stopTraining)
                        game.Tick();

                    UpdateBestObservedMetric(game.BestObservedMetric);
                    var gameStats = game.ExtraGameStats().Select(o => $"{o.Name} {o.Value}").ToCsv('|');
                    lock (validationLocks[candidateIndex])
                    {
                        validationTotals[candidateIndex] += game.Rating;
                        validationDegeneracyTotals[candidateIndex] += game.DegeneracyScore;
                        if (string.IsNullOrEmpty(validationReasons[candidateIndex]) && !string.IsNullOrEmpty(game.DegeneracyReason))
                            validationReasons[candidateIndex] = game.DegeneracyReason;
                        if (game.Rating > validationBestRatings[candidateIndex])
                        {
                            validationBestRatings[candidateIndex] = game.Rating;
                            validationBestSeeds[candidateIndex] = seed;
                            validationBestStats[candidateIndex] = gameStats;
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Instance.Exception("Validation failed.", e);
                }
            });
            for (var i = 0; i < validationCandidateCount; i++)
            {
                validationResults[i] = (
                    validationTotals[i] / validationGamesPerBrain,
                    validationDegeneracyTotals[i] / validationGamesPerBrain,
                    validationReasons[i],
                    validationBestRatings[i],
                    validationBestSeeds[i],
                    orderedGames[i].Brain,
                    validationBestStats[i] ?? string.Empty);
            }

            bestValidation = validationResults.OrderByDescending(GetSelectionFitness).First();
        }
        UpdateExplorationBoost(bestValidation);
        var mutationRate = GetEffectiveMutationRate();
        var randomFraction = GetEffectiveRandomFraction();
        var bestEvalFitness = useTrainingScoreForGoat
            ? bestTrainingFitness
            : skippedValidation
                ? 0.0
                : GetSelectionFitness(bestValidation);

        // Report summary of results.
        var evalText = useTrainingScoreForGoat
            ? "train"
            : skippedValidation
                ? "skip"
                : bestValidation.AverageRating.ToString("F1");
        var fitText = useTrainingScoreForGoat
            ? bestEvalFitness.ToString("F1")
            : skippedValidation
                ? "skip"
                : bestEvalFitness.ToString("F1");
        var degText = useTrainingScoreForGoat
            ? theBest.AverageDegeneracy.ToString("F2")
            : skippedValidation
                ? "skip"
                : bestValidation.AverageDegeneracy.ToString("F2");
        var stats = $"Gen {m_generation}|Pop {m_currentPopSize}|GOAT {m_savedRating:F1}|SinceGOAT {GetGoatAgeSeconds()}s|Train {theBest.AverageRating:F1}|Eval {evalText}|Fit {fitText}|Mut {mutationRate:F3}|Rnd {randomFraction:F2}|Deg {degText}|Seed {theBest.GameSeed}";
        stats += GetBestObservedMetricInlineText();
        if (!string.IsNullOrEmpty(theBest.GameStats))
            stats += $"|{theBest.GameStats}";
        if (useTrainingScoreForGoat)
            stats += "|TrainGOAT";
        else if (skippedValidation)
            stats += "|EvalSkip";
        if (m_explorationBoostGenerationsRemaining > 0)
            stats += $"|Boost {m_lastExplorationReason}";
        System.Console.WriteLine(stats);
        
        // Remember the GOAT brains.
        var worstGoatRating = m_goatBrains.Count > 0 ? m_goatBrains.FastFindMin(o => o.Rating).Rating : 0.0;
        for (var i = 0; i < orderedGames.Length; i++)
        {
            var candidateFitness = GetSelectionFitness(orderedGames[i]);
            if (candidateFitness > worstGoatRating)
                m_goatBrains.Add((candidateFitness, orderedGames[i].Brain));
        }

        while (m_goatBrains.Count > MaxGoatBrains)
        {
            var toRemove = m_goatBrains.FastFindMin(o => o.Rating);
            m_goatBrains.Remove(toRemove);
        }

        if (useTrainingScoreForGoat)
            PersistBrainImprovement(theBest, saveBrainBytes);
        else if (!skippedValidation)
            PersistBrainImprovement(bestValidation, saveBrainBytes);
        else
            m_generationsSinceImprovement++;

        // Build the brains for the next generation.
        m_nextGenBrains = UseHarnessStyleEvolution()
            ? BuildHarnessStyleNextGeneration(orderedGames, mutationRate, randomFraction)
            : BuildLegacyNextGeneration(orderedGames, mutationRate, randomFraction);

        if (m_explorationBoostGenerationsRemaining > 0)
            m_explorationBoostGenerationsRemaining--;
    }

    private void PersistBrainImprovement((double AverageRating, double AverageDegeneracy, string DegeneracyReason, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats) theBest, Action<byte[]> saveBrainBytes)
    {
        var effectiveFitness = GetSelectionFitness(theBest);
        if (effectiveFitness > m_savedRating)
        {
            m_savedRating = effectiveFitness;
            m_lastGoatImprovementTimestamp = Stopwatch.GetTimestamp();
            System.Console.WriteLine("Saved.");
            saveBrainBytes(theBest.Brain.Save());

            m_generationsSinceImprovement = 0;
            return;
        }

        m_generationsSinceImprovement++;
    }

    private List<AiBrainBase> BuildLegacyNextGeneration((double AverageRating, double AverageDegeneracy, string DegeneracyReason, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats)[] orderedGames, double mutationRate, double randomFraction)
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
        nextBrains.AddRange(Enumerable.Range(0, (int)(m_currentPopSize * randomFraction)).Select(_ => CreateBrain()));
            
        // Elite get to be parents.
        var breeders = orderedGames.Select(o => (Rating: GetSelectionFitness(o), o.Brain)).ToList();
        breeders.AddRange(m_goatBrains);
        while (nextBrains.Count < m_currentPopSize)
        {
            var mumBrain = Random.Shared.RouletteSelection(breeders, o => o.Rating).Brain;
            var dadBrain = Random.Shared.RouletteSelection(breeders, o => o.Rating).Brain;
            var childBrain = mumBrain.Clone().CrossWith(dadBrain, GetCrossoverRate(), m_breedingRandom).Mutate(mutationRate, m_breedingRandom);
            nextBrains.Add(childBrain);
        }

        return nextBrains;
    }

    private List<AiBrainBase> BuildHarnessStyleNextGeneration((double AverageRating, double AverageDegeneracy, string DegeneracyReason, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats)[] orderedGames, double mutationRate, double randomFraction)
    {
        var nextBrains = new List<AiBrainBase>(m_currentPopSize);
        var eliteCount = Math.Min(GetEliteCount(), orderedGames.Length);
        for (var i = 0; i < eliteCount; i++)
            nextBrains.Add(orderedGames[i].Brain.Clone());

        var randomCount = Math.Max(1, (int)(m_currentPopSize * randomFraction));
        for (var i = 0; i < randomCount && nextBrains.Count < m_currentPopSize; i++)
            nextBrains.Add(CreateBrain());

        while (nextBrains.Count < m_currentPopSize)
        {
            var mumBrain = SelectParent(orderedGames);
            var dadBrain = SelectParent(orderedGames);
            var childBrain = mumBrain.Clone().CrossWith(dadBrain, GetCrossoverRate(), m_breedingRandom).Mutate(mutationRate, m_breedingRandom);
            nextBrains.Add(childBrain);
        }

        return nextBrains;
    }

    private AiBrainBase SelectParent((double AverageRating, double AverageDegeneracy, string DegeneracyReason, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats)[] orderedGames)
    {
        var totalFitness = 0.0;
        for (var i = 0; i < orderedGames.Length; i++)
            totalFitness += Math.Max(1.0, GetSelectionFitness(orderedGames[i]));

        var random = m_breedingRandom;
        var target = (random?.NextDouble() ?? Random.Shared.NextDouble()) * totalFitness;
        var cumulative = 0.0;
        for (var i = 0; i < orderedGames.Length; i++)
        {
            cumulative += Math.Max(1.0, GetSelectionFitness(orderedGames[i]));
            if (target <= cumulative)
                return orderedGames[i].Brain;
        }

        return orderedGames[^1].Brain;
    }

    private AiGameBase CreateGameWithSeed(int seed, AiBrainBase brain, int generation, bool isValidation, int candidateIndex, int gameIndex)
    {
        var game = CreateTrainingGame(brain ?? CreateBrain(), generation, isValidation, candidateIndex, gameIndex);
        game.GameRand = new Random(seed);
        game.ResetGame();
        return game;
    }

    private double GetEffectiveMutationRate()
    {
        var baseRate = GetMutationRate();
        var floorRate = Math.Max(0.01, baseRate * 0.35);
        var decay = Math.Min(1.0, Math.Max(0, m_generation - 1) / 400.0);
        var rate = baseRate + (floorRate - baseRate) * decay;
        if (m_generationsSinceImprovement >= 40)
            rate = Math.Min(baseRate, rate * 1.35);
        if (m_explorationBoostGenerationsRemaining > 0)
            rate = Math.Min(0.18, Math.Max(rate, baseRate * 2.5));

        return rate;
    }

    private static double GetSelectionFitness((double AverageRating, double AverageDegeneracy, string DegeneracyReason, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats) result)
    {
        var degeneracy = Math.Clamp(result.AverageDegeneracy, 0.0, 1.0);
        if (degeneracy >= 0.95)
            return 0.0;

        var penaltyFactor = 1.0 - degeneracy * degeneracy * 0.9;
        return result.AverageRating * penaltyFactor;
    }

    private double GetEffectiveRandomFraction()
    {
        var randomFraction = GetRandomFraction();
        if (m_explorationBoostGenerationsRemaining > 0)
            randomFraction = Math.Max(randomFraction, 0.18);

        return randomFraction;
    }

    private void UpdateExplorationBoost((double AverageRating, double AverageDegeneracy, string DegeneracyReason, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats) bestValidation)
    {
        if (m_explorationBoostGenerationsRemaining > 0)
            return;

        var severeDegeneracy = bestValidation.AverageDegeneracy >= 0.85;
        var stubbornDegeneracy = bestValidation.AverageDegeneracy >= 0.60 && m_generationsSinceImprovement >= 12;
        var hardStagnation = m_generationsSinceImprovement >= 40;
        if (!severeDegeneracy && !stubbornDegeneracy && !hardStagnation)
            return;

        m_explorationBoostGenerationsRemaining = severeDegeneracy ? 20 : 12;
        m_lastExplorationReason = !string.IsNullOrWhiteSpace(bestValidation.DegeneracyReason)
            ? bestValidation.DegeneracyReason
            : hardStagnation
                ? "stagnation"
                : "degenerate";
        System.Console.WriteLine($"Exploration boost: {m_lastExplorationReason} detected. Increasing mutation/randomness for {m_explorationBoostGenerationsRemaining} generations.");
    }

    private int GetDefaultSeedBase()
    {
        var text = GetType().FullName ?? GetType().Name;
        unchecked
        {
            var hash = (int)2166136261;
            for (var i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619;
            }

            return hash & 0x7FFFFFFF;
        }
    }

    private bool IsFreshTraining() =>
        ActivationName.Contains("_trainfresh", StringComparison.OrdinalIgnoreCase);

    private void UpdateBestObservedMetric((string Name, double Value, string Format)? metric)
    {
        if (!metric.HasValue)
            return;

        lock (m_bestObservedMetricLock)
        {
            if (!m_bestObservedMetric.HasValue || metric.Value.Value > m_bestObservedMetric.Value.Value)
                m_bestObservedMetric = metric;
        }
    }

    private string GetBestObservedMetricInlineText()
    {
        lock (m_bestObservedMetricLock)
        {
            if (!m_bestObservedMetric.HasValue)
                return string.Empty;

            var metric = m_bestObservedMetric.Value;
            return $"|Best{metric.Name} {metric.Value.ToString(metric.Format)}";
        }
    }

    private string GetBestObservedMetricSummaryText()
    {
        lock (m_bestObservedMetricLock)
        {
            if (!m_bestObservedMetric.HasValue)
                return string.Empty;

            var metric = m_bestObservedMetric.Value;
            return $"   Best {metric.Name}: {metric.Value.ToString(metric.Format)}";
        }
    }

    private string GetBestObservedMetricHudText()
    {
        lock (m_bestObservedMetricLock)
        {
            if (!m_bestObservedMetric.HasValue)
                return string.Empty;

            var metric = m_bestObservedMetric.Value;
            return $"Best {metric.Name}: {metric.Value.ToString(metric.Format)}";
        }
    }

    private int GetGoatAgeSeconds()
    {
        var timestamp = Interlocked.Read(ref m_lastGoatImprovementTimestamp);
        if (timestamp <= 0)
            return 0;

        var elapsedSeconds = (Stopwatch.GetTimestamp() - timestamp) / (double)Stopwatch.Frequency;
        return Math.Max(0, (int)elapsedSeconds);
    }

    private string GetGoatAgeHudText() =>
        $"Since GOAT: {GetGoatAgeSeconds()}s";

    private bool ShouldSkipValidation(double bestTrainingFitness)
    {
        if (m_savedRating <= 0.0)
            return false;
        if (m_generation < GetValidationSkipWarmupGenerations())
            return false;

        var interval = GetForcedValidationInterval();
        if (interval > 0 && m_generation % interval == 0)
            return false;

        return bestTrainingFitness < m_savedRating * GetValidationSkipThresholdRatio();
    }

    protected virtual IEnumerable<AiBrainBase> CreateInitialPopulation()
    {
        var initialPopulationSize = GetInitialPopulationSize();
        var brains = new List<AiBrainBase>(initialPopulationSize);
        var savedBrainBytes = IsFreshTraining() ? null : GetSavedBrainBytes();
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
    /// Allows training runs to adjust the simulated environment based on generation or phase.
    /// </summary>
    /// <remarks>
    /// Most games can ignore this and use the simpler overload above. Games with curriculum
    /// training can override this to make early generations easier while keeping validation
    /// on the real target difficulty.
    /// </remarks>
    protected virtual AiGameBase CreateTrainingGame(AiBrainBase brain, int generation, bool isValidation, int candidateIndex, int gameIndex) =>
        CreateTrainingGame(brain);

    /// <summary>
    /// Returns the seed for one candidate game during training.
    /// </summary>
    /// <remarks>
    /// The default is deterministic so repeated runs are easier to compare.
    /// Games can override this if they want a custom seed schedule.
    /// </remarks>
    protected virtual int GetTrainingSeed(int generation, int brainIndex, int gameIndex) =>
        unchecked(GetDefaultSeedBase() + generation * 10_000 + brainIndex * 101 + gameIndex * 17);
    protected virtual int GetValidationSeed(int generation, int candidateIndex, int gameIndex) =>
        unchecked(GetDefaultSeedBase() + 1_000_000 + candidateIndex * 1009 + gameIndex * 37);

    protected virtual int GetInitialPopulationSize() => DefaultInitialPopSize;
    protected virtual int GetMinPopulationSize() => DefaultMinPopSize;
    protected virtual int GetGamesPerBrain() => DefaultGamesPerBrain;
    protected virtual int GetValidationGamesPerBrain() => DefaultValidationGamesPerBrain;
    protected virtual int GetValidationCandidateCount() => DefaultValidationCandidateCount;
    protected virtual int GetEliteCount() => MaxGoatBrains;
    protected virtual double GetRandomFraction() => 0.05;
    protected virtual double GetCrossoverRate() => 0.5;
    protected virtual double GetMutationRate() => 0.05;
    protected virtual bool UseTrainingScoreForGoat() => false;
    protected virtual double GetValidationSkipThresholdRatio() => 0.75;
    protected virtual int GetValidationSkipWarmupGenerations() => 30;
    protected virtual int GetForcedValidationInterval() => 10;
    protected virtual string GetTrainingStatusText(int generation) => string.Empty;
    protected virtual bool UseHarnessStyleEvolution() => true;
    protected virtual int? GetBreedingRandomSeed() => GetDefaultSeedBase();
    protected virtual byte[] GetSavedBrainBytes() => null;
    protected abstract AiGameBase CreateGame(AiBrainBase brain);
    protected abstract AiBrainBase CreateBrain();
}
