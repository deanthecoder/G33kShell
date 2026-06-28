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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DTC.Core;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using G33kShell.Desktop.Skins;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers.AI;

/// <summary>
/// Base class for AI-powered games that train via simple neuroevolution.
/// </summary>
/// <remarks>
/// Training does not use gradient descent against a replay buffer. Instead, each generation
/// evaluates a population of brains on a shared set of seeded games, ranks them by the game's
/// <see cref="AiGameBase.Rating"/>, then validates the strongest candidates on a separate fixed
/// seed set. The evolving population starts independently and each
/// next generation contains current elites, fresh random brains, and crossover/mutation children.
/// The persisted champion's benchmark result is cached and it is replaced only when a challenger
/// beats that result on the same validation games.
/// </remarks>
public abstract class AiGameCanvasBase : ScreensaverBase
{
    protected readonly record struct TrainingGenerationResult(
        int Generation,
        double? CurrentRating,
        double GoatRating);
    protected readonly record struct ExplorationProgress(double Primary, double Secondary);
    private readonly record struct TrainingPoint(double? CurrentRating, double GoatRating);
    private readonly record struct ArchivedBrain(AiBrainBase Brain, ExplorationProgress Progress, double Fitness);
    private readonly record struct EvaluationResult(
        double AverageDegeneracy,
        string DegeneracyReason,
        AiBrainBase Brain,
        string GameStats,
        string AverageStats,
        double Fitness);

    private const int DefaultInitialPopSize = 160;
    private const int DefaultMinPopSize = 80;
    private const int MaxGoatBrains = 5;
    private const int DefaultGamesPerBrain = 4;
    private const int DefaultValidationGamesPerBrain = 4;
    private const int DefaultValidationCandidateCount = 3;
    private const int TrainingGraphWidth = 256;
    private const int TrainingGraphHeight = 240;
    private const int TrainingGraphLeft = 12;
    private const int TrainingGraphTop = 70;
    private const int TrainingGraphRight = 6;
    private const int TrainingGraphBottom = 12;
    private const int TrainingGraphGenerationSpacing = 5;
    
    private readonly List<(double Rating, AiBrainBase Brain)> m_goatBrains = new List<(double Rating, AiBrainBase Brain)>(MaxGoatBrains);
    private readonly List<ArchivedBrain> m_completingBrainArchive = [];
    private readonly object m_trainingHistoryLock = new object();
    private readonly List<TrainingPoint> m_trainingHistory = new List<TrainingPoint>();
    private int m_generation;
    private double m_championRating;
    private int m_generationsSinceImprovement;
    private int m_generationsSinceExplorationProgress;
    private ExplorationProgress? m_bestExplorationProgress;
    private bool m_hasCustomExplorationProgress;
    private bool m_resetCompletingArchiveAfterBreeding;
    private bool m_resetChampionBenchmarkAfterBreeding;
    private int m_explorationBoostGenerationsRemaining;
    private int m_currentPopSize;
    private string m_lastExplorationReason;
    private string m_lastPersistBlockReason;
    private Task m_trainingTask;
    private volatile bool m_stopTraining;
    private List<AiBrainBase> m_nextGenBrains;
    private Random m_breedingRandom;
    private readonly ParallelOptions m_parallelOptions = new ParallelOptions();
    private readonly object m_bestObservedMetricLock = new object();
    private (string Name, double Value, string Format)? m_bestObservedMetric;
    private long m_lastGoatImprovementTimestamp;
    private byte[] m_cachedSavedBrainBytes;
    private int m_cachedSavedBrainLength = -1;
    private bool m_cachedHasSavedBrainData;
    private WindowManager m_windowManager;
    private PixelScreenDataLock m_trainingPixelScreen;
    private AiBrainBase m_persistedChampionBrain;

    protected int ArenaWidth { get; }
    protected int ArenaHeight { get; }
    protected int TrainingGeneration => m_generation;
    protected bool IsTraining => m_trainingTask != null;

    public override bool IsReadyToRun => HasSavedBrainData();

    protected AiGameCanvasBase(int width, int height, int targetFps = 30) : base(width, height, targetFps)
    {
        ArenaWidth = width;
        ArenaHeight = height;
        m_parallelOptions.MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1);
    }

    public override sealed void UpdateFrame(ScreenData screen)
    {
        PrepareAiFrame(screen);
        if (ShouldTrainAi())
        {
            TrainAi(screen, SaveBrainBytes);
            return;
        }

        if (ShowNotReadyMessage(screen))
        {
            OnAiNotReady();
            return;
        }

        UpdateGameFrame(screen);
    }

    public override void OnLoaded(WindowManager windowManager)
    {
        m_windowManager = windowManager;
        base.OnLoaded(windowManager);
    }

    protected override void OnUnloaded()
    {
        ClearTrainingGraph();
        m_windowManager = null;
        base.OnUnloaded();
    }

    public override void OnSkinChanged(SkinBase skin)
    {
        base.OnSkinChanged(skin);
        if (m_trainingPixelScreen == null)
            return;

        using (m_trainingPixelScreen.Lock(out var graph))
            graph.SetPalette(GetTrainingGraphPalette());
    }

    public override void StartScreensaver(ScreenData shellScreen)
    {
        base.StartScreensaver(shellScreen);
        if (!ShouldTrainAi() || m_windowManager == null)
            return;

        lock (m_trainingHistoryLock)
            m_trainingHistory.Clear();
        m_trainingPixelScreen = m_windowManager.SetPixelScreen(TrainingGraphWidth, TrainingGraphHeight, GetTrainingGraphPalette());
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
        ClearTrainingTextOverlay(screen);
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
        {
            screen.PrintAt(0, nextHudLine, goatAge);
            nextHudLine++;
        }

        screen.PrintAt(0, nextHudLine, "GOAT");
        screen.PrintAt(5, nextHudLine, "current", Foreground.WithBrightness(0.62));
        DrawTrainingGraph();
        
        if (m_trainingTask != null)
        {
            // We're already training - Do nothing.
            return;
        }
        
        m_persistedChampionBrain = null;
        m_championRating = 0.0;
        m_generationsSinceImprovement = 0;
        m_explorationBoostGenerationsRemaining = 0;
        m_generationsSinceExplorationProgress = 0;
        m_bestExplorationProgress = null;
        m_hasCustomExplorationProgress = false;
        m_completingBrainArchive.Clear();
        m_resetCompletingArchiveAfterBreeding = false;
        m_resetChampionBenchmarkAfterBreeding = false;
        OnTrainingStarted();
        System.Console.WriteLine("Starting training from random brains...");
        var brain = CreateBrain();
        System.Console.WriteLine($"Brain layers: {FormatBrainLayers(brain)}");
        ThreadPool.GetMinThreads(out var currentMinWorkers, out var currentMinIo);
        var desiredMinWorkers = Math.Max(currentMinWorkers, m_parallelOptions.MaxDegreeOfParallelism);
        if (desiredMinWorkers != currentMinWorkers)
            ThreadPool.SetMinThreads(desiredMinWorkers, currentMinIo);

        m_currentPopSize = GetInitialPopulationSize();
        var breedingSeed = GetBreedingRandomSeed();
        m_breedingRandom = breedingSeed.HasValue ? new Random(breedingSeed.Value) : null;
        m_bestObservedMetric = null;
        m_lastPersistBlockReason = null;
        m_lastGoatImprovementTimestamp = Stopwatch.GetTimestamp();
        
        m_stopTraining = false;
        m_trainingTask = Task.Run(() =>
        {
            try
            {
                while (!m_stopTraining)
                    TrainAiImpl(saveBrainBytes);

                System.Console.WriteLine("Training complete.");
                System.Console.WriteLine("Summary:");
                System.Console.WriteLine($"  Brain layers: {FormatBrainLayers(brain)}");
                System.Console.WriteLine($"   Generations: {m_generation}");
                System.Console.WriteLine($"    GOAT rating: {m_championRating:F1}");
                System.Console.WriteLine($"  Since GOAT: {GetGoatAgeSeconds()}s");
                var localBestObservedMetric = GetBestObservedMetricSummaryText();
                if (!string.IsNullOrEmpty(localBestObservedMetric))
                    System.Console.WriteLine(localBestObservedMetric);
            }
            finally
            {
                m_nextGenBrains = null;
                m_goatBrains.Clear();
                m_persistedChampionBrain = null;
                m_trainingTask = null;
            }
        });
    }

    public override void StopScreensaver()
    {
        base.StopScreensaver();

        StopTraining();
        ClearTrainingGraph();
    }

    protected void StopTraining() =>
        m_stopTraining = true;

    protected bool ShouldTrainAi() =>
        HasSwitch("train");

    protected bool ShowNotReadyMessage(ScreenData screen)
    {
        if (IsReadyToRun)
            return false;

        var message = $"{Name} AI is not trained. Run 'screensaver {Name}_train' to train it.";
        screen.Clear(Foreground, Background);
        screen.PrintAt(Math.Max(0, (screen.Width - message.Length) / 2), Math.Max(0, screen.Height / 2), message);
        return true;
    }

    private bool HasSavedBrainData()
    {
        var savedBrainBytes = GetSavedBrainBytes();
        var savedBrainLength = savedBrainBytes?.Length ?? 0;
        if (ReferenceEquals(savedBrainBytes, m_cachedSavedBrainBytes) && savedBrainLength == m_cachedSavedBrainLength)
            return m_cachedHasSavedBrainData;

        m_cachedSavedBrainBytes = savedBrainBytes;
        m_cachedSavedBrainLength = savedBrainLength;
        m_cachedHasSavedBrainData = CreateBrain().CanLoad(savedBrainBytes);
        return m_cachedHasSavedBrainData;
    }

    private void TrainAiImpl(Action<byte[]> saveBrainBytes)
    {
        m_nextGenBrains ??= CreateInitialPopulation().ToList();
        m_generation++;
        OnTrainingGenerationStarted(m_generation, m_nextGenBrains.Count);
        var gamesPerBrain = GetGamesPerBrain();
        var validationGamesPerBrain = GetValidationGamesPerBrain();
        
        var gameResults = new EvaluationResult[m_nextGenBrains.Count];
        var trainingTotals = new double[m_nextGenBrains.Count];
        var trainingDegeneracyTotals = new double[m_nextGenBrains.Count];
        var trainingBestRatings = new double[m_nextGenBrains.Count];
        var trainingBestStats = new string[m_nextGenBrains.Count];
        var trainingStatsTotals = new Dictionary<string, double>[m_nextGenBrains.Count];
        var trainingReasons = new string[m_nextGenBrains.Count];
        var trainingLocks = new object[m_nextGenBrains.Count];
        for (var i = 0; i < m_nextGenBrains.Count; i++)
        {
            trainingBestRatings[i] = double.MinValue;
            trainingStatsTotals[i] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            trainingLocks[i] = new object();
        }

        Parallel.For(0, m_nextGenBrains.Count * gamesPerBrain, m_parallelOptions, trainingJobIndex =>
        {
            var brainIndex = trainingJobIndex / gamesPerBrain;
            var gameIndex = trainingJobIndex % gamesPerBrain;
            try
            {
                var seed = GetTrainingSeed(m_generation, 0, gameIndex);
                var evaluationBrain = m_nextGenBrains[brainIndex].Clone();
                var game = CreateGameWithSeed(seed, evaluationBrain, m_generation, false, brainIndex, gameIndex);
                while (!game.IsGameOver && !m_stopTraining)
                {
                    game.Tick();
                    OnTrainingTick(brainIndex, game);
                }
                if (!m_stopTraining)
                    OnTrainingGameComplete(brainIndex, game);

                UpdateBestObservedMetric(game.BestObservedMetric);
                var extraStats = game.ExtraGameStats().ToArray();
                var gameStats = extraStats.Select(o => $"{o.Name} {o.Value}").ToCsv('|');
                lock (trainingLocks[brainIndex])
                {
                    trainingTotals[brainIndex] += game.Rating;
                    trainingDegeneracyTotals[brainIndex] += game.DegeneracyScore;
                    AccumulateStats(trainingStatsTotals[brainIndex], extraStats);
                    if (string.IsNullOrEmpty(trainingReasons[brainIndex]) && !string.IsNullOrEmpty(game.DegeneracyReason))
                        trainingReasons[brainIndex] = game.DegeneracyReason;
                    if (game.Rating > trainingBestRatings[brainIndex])
                    {
                        trainingBestRatings[brainIndex] = game.Rating;
                        trainingBestStats[brainIndex] = gameStats;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Instance.Exception("Training failed.", e);
            }
        });

        if (AbortInterruptedGeneration())
            return;

        for (var i = 0; i < m_nextGenBrains.Count; i++)
        {
            var averageRating = trainingTotals[i] / gamesPerBrain;
            var averageDegeneracy = trainingDegeneracyTotals[i] / gamesPerBrain;
            var averageStats = AverageStats(trainingStatsTotals[i], gamesPerBrain);
            gameResults[i] = new EvaluationResult(
                averageDegeneracy,
                trainingReasons[i],
                m_nextGenBrains[i],
                trainingBestStats[i] ?? string.Empty,
                FormatAverageStats(trainingStatsTotals[i], gamesPerBrain),
                GetSelectionFitness(averageRating, averageDegeneracy, averageStats, gamesPerBrain));
        }

        // Select the breeders.
        var orderedGames = gameResults.OrderByDescending(GetSelectionFitness).ToArray();
        var theBest = orderedGames[0];
        var useTrainingScoreForGoat = UseTrainingScoreForGoat();
        var bestTrainingIndex = Array.FindIndex(gameResults, result => ReferenceEquals(result.Brain, theBest.Brain));
        IReadOnlyDictionary<string, double> currentProgressStats = useTrainingScoreForGoat
            ? AverageStats(trainingStatsTotals[bestTrainingIndex], gamesPerBrain)
            : null;
        var bestTrainingFitness = GetSelectionFitness(theBest);
        var skippedValidation = useTrainingScoreForGoat ||
                                (m_persistedChampionBrain == null && ShouldSkipValidation(bestTrainingFitness, m_championRating));
        var bestValidation = theBest;
        if (!useTrainingScoreForGoat && !skippedValidation)
        {
            var validationCandidateCount = Math.Min(GetValidationCandidateCount(), orderedGames.Length);
            var validationResultCount = validationCandidateCount;
            var validationResults = new EvaluationResult[validationResultCount];
            var validationTotals = new double[validationResultCount];
            var validationDegeneracyTotals = new double[validationResultCount];
            var validationBestRatings = new double[validationResultCount];
            var validationBestStats = new string[validationResultCount];
            var validationReasons = new string[validationResultCount];
            var validationStatsTotals = new Dictionary<string, double>[validationResultCount];
            var validationLocks = new object[validationResultCount];
            for (var i = 0; i < validationResultCount; i++)
            {
                validationBestRatings[i] = double.MinValue;
                validationStatsTotals[i] = new Dictionary<string, double>();
                validationLocks[i] = new object();
            }

            Parallel.For(0, validationResultCount * validationGamesPerBrain, m_parallelOptions, validationJobIndex =>
            {
                var candidateIndex = validationJobIndex / validationGamesPerBrain;
                var gameIndex = validationJobIndex % validationGamesPerBrain;
                try
                {
                    var seed = GetValidationSeed(m_generation, 0, gameIndex);
                    var sourceBrain = orderedGames[candidateIndex].Brain;
                    var evaluationBrain = sourceBrain.Clone();
                    var game = CreateGameWithSeed(seed, evaluationBrain, m_generation, true, candidateIndex, gameIndex);
                    while (!game.IsGameOver && !m_stopTraining)
                        game.Tick();

                    UpdateBestObservedMetric(game.BestObservedMetric);
                    var gameStats = game.ExtraGameStats().Select(o => $"{o.Name} {o.Value}").ToCsv('|');
                    lock (validationLocks[candidateIndex])
                    {
                        validationTotals[candidateIndex] += game.Rating;
                        validationDegeneracyTotals[candidateIndex] += game.DegeneracyScore;
                        AccumulateStats(validationStatsTotals[candidateIndex], game.ExtraGameStats());
                        if (string.IsNullOrEmpty(validationReasons[candidateIndex]) && !string.IsNullOrEmpty(game.DegeneracyReason))
                            validationReasons[candidateIndex] = game.DegeneracyReason;
                        if (game.Rating > validationBestRatings[candidateIndex])
                        {
                            validationBestRatings[candidateIndex] = game.Rating;
                            validationBestStats[candidateIndex] = gameStats;
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Instance.Exception("Validation failed.", e);
                }
            });

            if (AbortInterruptedGeneration())
                return;

            for (var i = 0; i < validationResultCount; i++)
            {
                var averageRating = validationTotals[i] / validationGamesPerBrain;
                var averageDegeneracy = validationDegeneracyTotals[i] / validationGamesPerBrain;
                var averageStats = AverageStats(validationStatsTotals[i], validationGamesPerBrain);
                validationResults[i] = new EvaluationResult(
                    averageDegeneracy,
                    validationReasons[i],
                    orderedGames[i].Brain,
                    validationBestStats[i] ?? string.Empty,
                    FormatAverageStats(validationStatsTotals[i], validationGamesPerBrain),
                    GetSelectionFitness(averageRating, averageDegeneracy, averageStats, validationGamesPerBrain));
            }

            var bestValidationIndex = Enumerable.Range(0, validationCandidateCount)
                .OrderByDescending(i => GetSelectionFitness(validationResults[i]))
                .First();
            bestValidation = validationResults[bestValidationIndex];
            currentProgressStats = AverageStats(validationStatsTotals[bestValidationIndex], validationGamesPerBrain);

            var promotionGamesPerBrain = GetPromotionValidationGamesPerBrain();
            if (promotionGamesPerBrain > validationGamesPerBrain)
            {
                var promotion = EvaluatePromotionCandidate(
                    bestValidation.Brain,
                    promotionGamesPerBrain);
                bestValidation = promotion.Result;
                currentProgressStats = promotion.AverageStats;
            }
        }

        if (AbortInterruptedGeneration())
            return;

        UpdateExplorationProgress(currentProgressStats);
        UpdateExplorationBoost(bestValidation);
        var mutationRate = GetEffectiveMutationRate();
        var randomFraction = GetEffectiveRandomFraction();
        var bestEvalFitness = useTrainingScoreForGoat
            ? bestTrainingFitness
            : skippedValidation
                ? 0.0
                : GetSelectionFitness(bestValidation);
        if (!skippedValidation)
        {
            var archiveBrain = bestValidation.Brain;
            UpdateCompletingBrainArchive(archiveBrain, currentProgressStats, bestEvalFitness);
        }
        var evaluatedGoatRating = m_championRating;

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

        var savedImprovement = false;
        if (useTrainingScoreForGoat)
            savedImprovement = PersistBrainImprovement(theBest, saveBrainBytes);
        else if (!skippedValidation)
            savedImprovement = PersistBrainImprovement(bestValidation, saveBrainBytes);
        else
            m_generationsSinceImprovement++;

        OnTrainingGenerationComplete(new TrainingGenerationResult(
            m_generation,
            useTrainingScoreForGoat ? bestTrainingFitness : !skippedValidation ? bestEvalFitness : null,
            m_championRating));

        // Report summary of results.
        var currentText = useTrainingScoreForGoat
            ? bestTrainingFitness.ToString("F1")
            : skippedValidation
                ? "skip"
                : bestEvalFitness.ToString("F1");
        var currentVsGoat = GetCurrentVsGoat(useTrainingScoreForGoat, skippedValidation, bestEvalFitness, evaluatedGoatRating);
        var currentVsGoatText = currentVsGoat.HasValue
            ? $"{(currentVsGoat.Value - 1.0) * 100.0:+0.0;-0.0;0.0}%"
            : "skip";
        var degText = useTrainingScoreForGoat
            ? theBest.AverageDegeneracy.ToString("F2")
            : skippedValidation
                ? "skip"
                : bestValidation.AverageDegeneracy.ToString("F2");
        var reportStats = !useTrainingScoreForGoat && !skippedValidation && !string.IsNullOrWhiteSpace(bestValidation.AverageStats)
            ? bestValidation.AverageStats
            : string.Empty;
        var stats = $"Gen {m_generation}|Pop {m_currentPopSize}|GOAT {m_championRating:F1}|Current {currentText}";
        if (IncludeVsGoatInTrainingLog())
            stats += $"|VsGOAT {currentVsGoatText}";
        if (IncludeGoatAgeInTrainingLog())
            stats += $"|SinceGOAT {GetGoatAgeSeconds()}s";
        if (UseDegeneracyPenalty())
            stats += $"|Deg {degText}";
        if (IncludeEvolutionParametersInTrainingLog())
            stats += $"|Mut {mutationRate:F3}|Rnd {randomFraction:F2}";
        if (IncludeBestObservedMetricInTrainingLog())
            stats += GetBestObservedMetricInlineText();
        if (!string.IsNullOrEmpty(reportStats))
        {
            stats += "|Candidate";
            stats += $"|{reportStats}";
        }
        if (useTrainingScoreForGoat)
            stats += "|TrainGOAT";
        else if (skippedValidation)
            stats += "|EvalSkip";
        if (savedImprovement)
            stats += "|Saved";
        else if (!string.IsNullOrWhiteSpace(m_lastPersistBlockReason))
            stats += $"|NoSave {m_lastPersistBlockReason}";
        if (m_explorationBoostGenerationsRemaining > 0)
            stats += $"|Boost {m_lastExplorationReason}";
        if (m_completingBrainArchive.Count > 0)
            stats += $"|Archive {m_completingBrainArchive.Count}";
        System.Console.WriteLine(stats);

        // Build the brains for the next generation.
        m_nextGenBrains = UseHarnessStyleEvolution()
            ? BuildHarnessStyleNextGeneration(orderedGames, mutationRate, randomFraction)
            : BuildLegacyNextGeneration(orderedGames, mutationRate, randomFraction);
        if (m_resetCompletingArchiveAfterBreeding)
        {
            m_completingBrainArchive.Clear();
            m_resetCompletingArchiveAfterBreeding = false;
        }
        if (m_resetChampionBenchmarkAfterBreeding)
        {
            ResetChampionBenchmark();
            m_resetChampionBenchmarkAfterBreeding = false;
        }

        if (m_explorationBoostGenerationsRemaining > 0)
            m_explorationBoostGenerationsRemaining--;
    }

    private static double? GetCurrentVsGoat(bool useTrainingScoreForGoat, bool skippedValidation, double currentRating, double goatRating)
    {
        if (!useTrainingScoreForGoat && skippedValidation)
            return null;
        if (goatRating <= 0.0)
            return 1.0;

        return currentRating / goatRating;
    }

    private static string FormatBrainLayers(AiBrainBase brain) =>
        brain.HiddenLayers.Length == 0
            ? $"{brain.InputSize} : {brain.OutputSize}"
            : $"{brain.InputSize} : {brain.HiddenLayers.ToCsv(' ')} : {brain.OutputSize}";

    private bool AbortInterruptedGeneration()
    {
        if (!m_stopTraining)
            return false;

        m_generation = Math.Max(0, m_generation - 1);
        return true;
    }

    private static void AccumulateStats(Dictionary<string, double> totals, IEnumerable<(string Name, string Value)> stats)
    {
        foreach (var stat in stats)
        {
            if (double.TryParse(stat.Value, out var value))
                totals[stat.Name] = totals.TryGetValue(stat.Name, out var current) ? current + value : value;
        }
    }

    private string FormatAverageStats(Dictionary<string, double> totals, int sampleCount)
    {
        if (sampleCount <= 0 || totals.Count == 0)
            return string.Empty;

        var reportedStats = GetReportedTrainingStats();
        if (reportedStats == null)
            return totals.Select(o => $"{o.Key} {o.Value / sampleCount:0.###}").ToCsv('|');

        return reportedStats
            .Where(totals.ContainsKey)
            .Select(name => $"{name} {totals[name] / sampleCount:0.###}")
            .ToCsv('|');
    }

    private static IReadOnlyDictionary<string, double> AverageStats(Dictionary<string, double> totals, int sampleCount) =>
        totals.ToDictionary(o => o.Key, o => o.Value / sampleCount, StringComparer.OrdinalIgnoreCase);

    private (EvaluationResult Result, IReadOnlyDictionary<string, double> AverageStats) EvaluatePromotionCandidate(
        AiBrainBase sourceBrain,
        int gameCount)
    {
        var totalRating = 0.0;
        var totalDegeneracy = 0.0;
        var bestRating = double.MinValue;
        var bestStats = string.Empty;
        var degeneracyReason = string.Empty;
        var statsTotals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var resultLock = new object();

        Parallel.For(0, gameCount, m_parallelOptions, gameIndex =>
        {
            try
            {
                var seed = GetPromotionValidationSeed(m_generation, gameIndex);
                var game = CreateGameWithSeed(
                    seed,
                    sourceBrain.Clone(),
                    m_generation,
                    true,
                    0,
                    gameIndex);
                while (!game.IsGameOver && !m_stopTraining)
                    game.Tick();

                UpdateBestObservedMetric(game.BestObservedMetric);
                var extraStats = game.ExtraGameStats().ToArray();
                var gameStats = extraStats.Select(o => $"{o.Name} {o.Value}").ToCsv('|');
                lock (resultLock)
                {
                    totalRating += game.Rating;
                    totalDegeneracy += game.DegeneracyScore;
                    AccumulateStats(statsTotals, extraStats);
                    if (string.IsNullOrEmpty(degeneracyReason) && !string.IsNullOrEmpty(game.DegeneracyReason))
                        degeneracyReason = game.DegeneracyReason;
                    if (game.Rating > bestRating)
                    {
                        bestRating = game.Rating;
                        bestStats = gameStats;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Instance.Exception("Promotion validation failed.", e);
            }
        });

        var averageRating = totalRating / gameCount;
        var averageDegeneracy = totalDegeneracy / gameCount;
        var averageStats = AverageStats(statsTotals, gameCount);
        return (
            new EvaluationResult(
                averageDegeneracy,
                degeneracyReason,
                sourceBrain,
                bestStats,
                FormatAverageStats(statsTotals, gameCount),
                GetSelectionFitness(averageRating, averageDegeneracy, averageStats, gameCount)),
            averageStats);
    }

    private Rgb[] GetTrainingGraphPalette() =>
    [
        Background,
        Foreground.WithBrightness(0.28),
        Foreground.WithBrightness(0.62),
        Foreground
    ];

    private void DrawTrainingGraph()
    {
        if (m_trainingPixelScreen == null)
            return;

        TrainingPoint[] points;
        lock (m_trainingHistoryLock)
            points = m_trainingHistory.ToArray();

        using (m_trainingPixelScreen.Lock(out var screen))
        {
            screen.Clear();
            OnAfterDrawTrainingGraph(screen);
            var width = screen.Width - TrainingGraphLeft - TrainingGraphRight;
            var height = screen.Height - TrainingGraphTop - TrainingGraphBottom;
            if (width < 2 || height < 2)
                return;

            for (var gridLine = 0; gridLine <= 4; gridLine++)
            {
                var y = TrainingGraphTop + gridLine * (height - 1) / 4;
                screen.DrawLine(TrainingGraphLeft, y, TrainingGraphLeft + width - 1, y, 1);
            }
            screen.DrawLine(TrainingGraphLeft, TrainingGraphTop, TrainingGraphLeft, TrainingGraphTop + height - 1, 2);
            screen.DrawLine(TrainingGraphLeft, TrainingGraphTop + height - 1, TrainingGraphLeft + width - 1, TrainingGraphTop + height - 1, 2);

            if (points.Length == 0)
                return;

            var visibleCount = Math.Min((width - 1) / TrainingGraphGenerationSpacing + 1, points.Length);
            var visible = points[^visibleCount..];
            var maxRating = visible.Max(o => Math.Max(o.GoatRating, o.CurrentRating ?? 0.0));
            var scaleMax = GetGraphScaleMaximum(maxRating);
            DrawTrainingSeries(screen, visible, height, scaleMax, o => o.CurrentRating, 2);
            DrawTrainingSeries(screen, visible, height, scaleMax, o => o.GoatRating, 3);
        }
    }

    private static void DrawTrainingSeries(
        PixelScreenData screen,
        TrainingPoint[] points,
        int height,
        double scaleMax,
        Func<TrainingPoint, double?> valueSelector,
        byte colorIndex)
    {
        int? previousX = null;
        int? previousY = null;
        double? previousValue = null;
        for (var i = 0; i < points.Length; i++)
        {
            var value = valueSelector(points[i]) ?? previousValue;
            if (!value.HasValue)
                continue;

            var x = TrainingGraphLeft + i * TrainingGraphGenerationSpacing;
            var normalized = Math.Clamp(value.Value / scaleMax, 0.0, 1.0);
            var y = TrainingGraphTop + height - 1 - (int)Math.Round(normalized * (height - 1));
            if (previousX.HasValue)
                screen.DrawLine(previousX.Value, previousY.Value, x, y, colorIndex);
            else
                screen.SetPixel(x, y, colorIndex);
            previousX = x;
            previousY = y;
            previousValue = value;
        }
    }

    private static double GetGraphScaleMaximum(double value)
    {
        if (value <= 0.0)
            return 1.0;

        var magnitude = Math.Pow(10.0, Math.Floor(Math.Log10(value)));
        return Math.Ceiling(value / magnitude) * magnitude;
    }

    private void ClearTrainingGraph()
    {
        if (m_trainingPixelScreen == null)
            return;

        if (ReferenceEquals(m_windowManager?.PixelScreen, m_trainingPixelScreen))
            m_windowManager?.ClearPixelScreen();
        m_trainingPixelScreen = null;
    }

    private static void ClearTrainingTextOverlay(ScreenData screen)
    {
        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Width; x++)
            {
                var attr = screen.Chars[y][x];
                attr.Set(' ');
                attr.Foreground = null;
                attr.Background = null;
            }
        }
    }

    private bool PersistBrainImprovement(EvaluationResult theBest, Action<byte[]> saveBrainBytes)
    {
        var effectiveFitness = GetSelectionFitness(theBest);
        if (effectiveFitness > m_championRating)
        {
            var persistBlockReason = GetPersistBlockReason(theBest.AverageDegeneracy, theBest.GameStats, theBest.AverageStats);
            if (!string.IsNullOrWhiteSpace(persistBlockReason))
            {
                m_lastPersistBlockReason = persistBlockReason;
                m_generationsSinceImprovement++;
                return false;
            }

            m_championRating = effectiveFitness;
            m_lastGoatImprovementTimestamp = Stopwatch.GetTimestamp();
            System.Console.WriteLine("Saved.");
            saveBrainBytes(theBest.Brain.Save());
            m_persistedChampionBrain = theBest.Brain.Clone();

            m_lastPersistBlockReason = null;
            m_generationsSinceImprovement = 0;
            return true;
        }

        m_lastPersistBlockReason = null;
        m_generationsSinceImprovement++;
        return false;
    }

    private void ResetChampionBenchmark()
    {
        m_persistedChampionBrain = null;
        m_championRating = 0.0;
        m_generationsSinceImprovement = 0;
        m_lastGoatImprovementTimestamp = Stopwatch.GetTimestamp();
        lock (m_trainingHistoryLock)
            m_trainingHistory.Clear();
        System.Console.WriteLine("GOAT benchmark reset for the new training stage.");
    }

    private void UpdateCompletingBrainArchive(
        AiBrainBase brain,
        IReadOnlyDictionary<string, double> averageStats,
        double fitness)
    {
        var archiveSize = GetCompletingArchiveSize();
        if (archiveSize <= 0 || brain == null || averageStats == null)
            return;

        var progress = GetExplorationProgress(averageStats);
        if (!progress.HasValue || !IsCompletingArchiveCandidate(progress.Value))
            return;

        var candidate = new ArchivedBrain(brain.Clone(), progress.Value, fitness);
        if (m_completingBrainArchive.Count < archiveSize)
        {
            m_completingBrainArchive.Add(candidate);
        }
        else
        {
            var worstIndex = 0;
            for (var i = 1; i < m_completingBrainArchive.Count; i++)
            {
                if (CompareArchivedBrains(m_completingBrainArchive[i], m_completingBrainArchive[worstIndex]) < 0)
                    worstIndex = i;
            }

            if (CompareArchivedBrains(candidate, m_completingBrainArchive[worstIndex]) <= 0)
                return;
            m_completingBrainArchive[worstIndex] = candidate;
        }

        m_completingBrainArchive.Sort((left, right) => CompareArchivedBrains(right, left));
    }

    private static int CompareArchivedBrains(ArchivedBrain left, ArchivedBrain right)
    {
        var primary = left.Progress.Primary.CompareTo(right.Progress.Primary);
        if (primary != 0)
            return primary;
        var secondary = left.Progress.Secondary.CompareTo(right.Progress.Secondary);
        return secondary != 0 ? secondary : left.Fitness.CompareTo(right.Fitness);
    }

    protected virtual string GetPersistBlockReason(double averageDegeneracy, string bestStats, string averageStats) => null;

    private List<AiBrainBase> BuildLegacyNextGeneration(EvaluationResult[] orderedGames, double mutationRate, double randomFraction)
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
        var offspringIndex = 0;
        while (nextBrains.Count < m_currentPopSize)
        {
            var mumBrain = Random.Shared.RouletteSelection(breeders, o => o.Rating).Brain;
            var dadBrain = Random.Shared.RouletteSelection(breeders, o => o.Rating).Brain;
            var childBrain = BreedChild(mumBrain, dadBrain, mutationRate, offspringIndex++);
            nextBrains.Add(childBrain);
        }

        return nextBrains;
    }

    private List<AiBrainBase> BuildHarnessStyleNextGeneration(EvaluationResult[] orderedGames, double mutationRate, double randomFraction)
    {
        var nextBrains = new List<AiBrainBase>(m_currentPopSize);
        foreach (var archived in m_completingBrainArchive)
        {
            if (nextBrains.Count >= m_currentPopSize)
                break;
            nextBrains.Add(archived.Brain.Clone());
        }

        var eliteCount = Math.Min(GetEliteCount(), orderedGames.Length);
        for (var i = 0; i < eliteCount && nextBrains.Count < m_currentPopSize; i++)
            nextBrains.Add(orderedGames[i].Brain.Clone());

        var randomCount = Math.Max(1, (int)(m_currentPopSize * randomFraction));
        for (var i = 0; i < randomCount && nextBrains.Count < m_currentPopSize; i++)
            nextBrains.Add(CreateBrain());

        var offspringIndex = 0;
        var archiveDescendantCount = m_completingBrainArchive.Count == 0
            ? 0
            : Math.Min(
                (int)Math.Round(m_currentPopSize * GetCompletingArchiveDescendantFraction()),
                m_currentPopSize - nextBrains.Count);
        for (var i = 0; i < archiveDescendantCount; i++)
        {
            var parent = m_completingBrainArchive[i % m_completingBrainArchive.Count].Brain;
            nextBrains.Add(BreedChild(parent, parent, mutationRate, offspringIndex++, true));
        }

        while (nextBrains.Count < m_currentPopSize)
        {
            var mumBrain = SelectParent(orderedGames);
            var dadBrain = SelectParent(orderedGames);
            var childBrain = BreedChild(mumBrain, dadBrain, mutationRate, offspringIndex++);
            nextBrains.Add(childBrain);
        }

        return nextBrains;
    }

    private AiBrainBase BreedChild(
        AiBrainBase primaryParent,
        AiBrainBase secondaryParent,
        double mutationRate,
        int offspringIndex,
        bool conservativeMutation = false)
    {
        var child = primaryParent.Clone();
        var crossoverRate = GetCrossoverRate();
        if (crossoverRate > 0.0)
            child.CrossWith(secondaryParent, crossoverRate, m_breedingRandom);
        var effectiveMutationRate = Math.Clamp(
            mutationRate * GetMutationRateMultiplier(offspringIndex),
            0.0,
            1.0);
        return MutateChild(child, effectiveMutationRate, offspringIndex, conservativeMutation, m_breedingRandom);
    }

    private AiBrainBase SelectParent(EvaluationResult[] orderedGames)
    {
        var parentPoolSize = Math.Clamp(
            (int)Math.Ceiling(orderedGames.Length * GetParentPoolFraction()),
            1,
            orderedGames.Length);
        var totalFitness = 0.0;
        for (var i = 0; i < parentPoolSize; i++)
            totalFitness += Math.Max(1.0, GetSelectionFitness(orderedGames[i]));

        var random = m_breedingRandom;
        var target = (random?.NextDouble() ?? Random.Shared.NextDouble()) * totalFitness;
        var cumulative = 0.0;
        for (var i = 0; i < parentPoolSize; i++)
        {
            cumulative += Math.Max(1.0, GetSelectionFitness(orderedGames[i]));
            if (target <= cumulative)
                return orderedGames[i].Brain;
        }

        return orderedGames[parentPoolSize - 1].Brain;
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
        var floorRate = Math.Max(GetMinimumMutationRate(), baseRate * 0.35);
        var decay = Math.Min(1.0, Math.Max(0, m_generation - 1) / 400.0);
        var rate = baseRate + (floorRate - baseRate) * decay;
        if (GetExplorationStagnationGenerations() >= GetExplorationStagnationThreshold())
            rate = Math.Min(baseRate, rate * 1.35);
        if (m_explorationBoostGenerationsRemaining > 0)
            rate = Math.Min(0.18, Math.Max(rate, baseRate * 2.5));

        return rate;
    }

    private static double GetSelectionFitness(EvaluationResult result) =>
        result.Fitness;

    private double GetSelectionFitness(
        double averageRating,
        double averageDegeneracy,
        IReadOnlyDictionary<string, double> averageStats,
        int sampleCount)
    {
        var fitness = GetEvaluationFitness(averageRating, averageStats, sampleCount);
        if (!UseDegeneracyPenalty())
            return fitness;

        var degeneracy = Math.Clamp(averageDegeneracy, 0.0, 1.0);
        var penaltyFactor = Math.Max(0.02, 1.0 - degeneracy * degeneracy * 0.98);
        return fitness * penaltyFactor;
    }

    private double GetEffectiveRandomFraction()
    {
        var randomFraction = GetRandomFraction();
        if (m_explorationBoostGenerationsRemaining > 0)
            randomFraction = Math.Max(randomFraction, 0.18);

        return randomFraction;
    }

    private void UpdateExplorationProgress(IReadOnlyDictionary<string, double> averageStats)
    {
        if (averageStats == null)
        {
            if (m_hasCustomExplorationProgress)
                m_generationsSinceExplorationProgress++;
            return;
        }

        var progress = GetExplorationProgress(averageStats);
        if (!progress.HasValue)
            return;

        m_hasCustomExplorationProgress = true;
        if (!m_bestExplorationProgress.HasValue ||
            IsMeaningfulExplorationProgress(progress.Value, m_bestExplorationProgress.Value))
        {
            m_bestExplorationProgress = progress;
            m_generationsSinceExplorationProgress = 0;
            return;
        }

        m_generationsSinceExplorationProgress++;
    }

    private int GetExplorationStagnationGenerations() =>
        m_hasCustomExplorationProgress
            ? m_generationsSinceExplorationProgress
            : m_generationsSinceImprovement;

    private void UpdateExplorationBoost(EvaluationResult bestValidation)
    {
        if (m_explorationBoostGenerationsRemaining > 0)
            return;
        if (m_completingBrainArchive.Count > 0 && !UseExplorationBoostWithCompletingArchive())
            return;

        var stagnationGenerations = GetExplorationStagnationGenerations();
        var useDegeneracy = UseDegeneracyPenalty();
        var severeDegeneracy = useDegeneracy && bestValidation.AverageDegeneracy >= 0.85;
        var stubbornDegeneracy = useDegeneracy && bestValidation.AverageDegeneracy >= 0.60 && stagnationGenerations >= 12;
        var hardStagnation = stagnationGenerations >= GetExplorationStagnationThreshold();
        if (!severeDegeneracy && !stubbornDegeneracy && !hardStagnation)
            return;

        m_explorationBoostGenerationsRemaining = severeDegeneracy ? 20 : 12;
        m_lastExplorationReason = (severeDegeneracy || stubbornDegeneracy) &&
                                  !string.IsNullOrWhiteSpace(bestValidation.DegeneracyReason)
            ? bestValidation.DegeneracyReason
            : hardStagnation
                ? GetExplorationStagnationReason()
                : "degenerate";
        System.Console.WriteLine($"Exploration boost: {m_lastExplorationReason} detected. Using Mut {GetEffectiveMutationRate():F3}/Rnd {GetEffectiveRandomFraction():F2} for {m_explorationBoostGenerationsRemaining} generations.");
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

    protected int SecondsSinceGoat => GetGoatAgeSeconds();

    private bool ShouldSkipValidation(double bestTrainingFitness, double comparisonFitness)
    {
        if (comparisonFitness <= 0.0)
            return false;
        if (m_generation < GetValidationSkipWarmupGenerations())
            return false;

        var interval = GetForcedValidationInterval();
        if (interval > 0 && m_generation % interval == 0)
            return false;

        return bestTrainingFitness < comparisonFitness * GetValidationSkipThresholdRatio();
    }

    protected virtual IEnumerable<AiBrainBase> CreateInitialPopulation()
    {
        var initialPopulationSize = GetInitialPopulationSize();
        var brains = new List<AiBrainBase>(initialPopulationSize);
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
    protected virtual int GetPromotionValidationSeed(int generation, int gameIndex) =>
        GetValidationSeed(generation, 0, gameIndex);

    protected virtual int GetInitialPopulationSize() => DefaultInitialPopSize;
    protected virtual int GetMinPopulationSize() => DefaultMinPopSize;
    protected virtual int GetGamesPerBrain() => DefaultGamesPerBrain;
    protected virtual int GetValidationGamesPerBrain() => DefaultValidationGamesPerBrain;
    protected virtual int GetPromotionValidationGamesPerBrain() => GetValidationGamesPerBrain();
    protected virtual int GetValidationCandidateCount() => DefaultValidationCandidateCount;
    protected virtual int GetEliteCount() => MaxGoatBrains;
    protected virtual double GetRandomFraction() => 0.05;
    protected virtual double GetParentPoolFraction() => 1.0;
    protected virtual double GetCrossoverRate() => 0.5;
    protected virtual double GetMutationRate() => 0.05;
    protected virtual double GetMinimumMutationRate() => 0.01;
    protected virtual double GetMutationRateMultiplier(int offspringIndex) => 1.0;
    protected virtual AiBrainBase MutateChild(
        AiBrainBase child,
        double mutationRate,
        int offspringIndex,
        bool conservativeMutation,
        Random random) =>
        child.Mutate(mutationRate, random);
    protected virtual ExplorationProgress? GetExplorationProgress(IReadOnlyDictionary<string, double> averageStats) => null;
    protected virtual bool IsMeaningfulExplorationProgress(ExplorationProgress candidate, ExplorationProgress best) =>
        candidate.Primary > best.Primary ||
        candidate.Primary.Equals(best.Primary) && candidate.Secondary > best.Secondary;
    protected virtual int GetExplorationStagnationThreshold() => 40;
    protected virtual string GetExplorationStagnationReason() => "stagnation";
    protected virtual int GetCompletingArchiveSize() => 0;
    protected virtual double GetCompletingArchiveDescendantFraction() => 0.0;
    protected virtual bool IsCompletingArchiveCandidate(ExplorationProgress progress) => false;
    protected virtual bool UseExplorationBoostWithCompletingArchive() => true;
    protected virtual bool UseTrainingScoreForGoat() => false;
    protected virtual double GetEvaluationFitness(
        double averageRating,
        IReadOnlyDictionary<string, double> averageStats,
        int sampleCount) =>
        averageRating;
    protected virtual double GetValidationSkipThresholdRatio() => 0.75;
    protected virtual int GetValidationSkipWarmupGenerations() => 30;
    protected virtual int GetForcedValidationInterval() => 10;
    protected virtual bool UseDegeneracyPenalty() => true;
    protected virtual IReadOnlyList<string> GetReportedTrainingStats() => null;
    protected virtual bool IncludeVsGoatInTrainingLog() => true;
    protected virtual bool IncludeGoatAgeInTrainingLog() => true;
    protected virtual bool IncludeEvolutionParametersInTrainingLog() => true;
    protected virtual bool IncludeBestObservedMetricInTrainingLog() => true;
    protected virtual string GetTrainingStatusText(int generation) => string.Empty;
    protected virtual void OnTrainingGenerationComplete(TrainingGenerationResult result)
    {
        lock (m_trainingHistoryLock)
            m_trainingHistory.Add(new TrainingPoint(result.CurrentRating, result.GoatRating));
    }

    protected virtual bool UseHarnessStyleEvolution() => true;
    protected virtual int? GetBreedingRandomSeed() => GetDefaultSeedBase();
    protected virtual byte[] GetSavedBrainBytes() => null;
    protected virtual void PrepareAiFrame(ScreenData screen) => screen.ClearChars();
    protected virtual void OnAiNotReady()
    {
    }
    protected virtual void OnTrainingTick(int brainIndex, AiGameBase game) { }
    protected virtual void OnTrainingStarted() { }
    protected virtual void OnTrainingGenerationStarted(int generation, int populationCount) { }
    protected virtual void OnTrainingGameComplete(int brainIndex, AiGameBase game) { }
    protected virtual void OnAfterDrawTrainingGraph(PixelScreenData screen) { }
    protected abstract void UpdateGameFrame(ScreenData screen);
    protected abstract void SaveBrainBytes(byte[] brainBytes);
    protected abstract AiGameBase CreateGame(AiBrainBase brain);
    protected abstract AiBrainBase CreateBrain();

    protected void ResetExplorationProgressTracking()
    {
        m_generationsSinceExplorationProgress = 0;
        m_bestExplorationProgress = null;
        m_hasCustomExplorationProgress = false;
        m_explorationBoostGenerationsRemaining = 0;
    }

    protected void ResetCompletingArchiveAfterBreeding() =>
        m_resetCompletingArchiveAfterBreeding = true;

    protected void ResetChampionBenchmarkAfterBreeding() =>
        m_resetChampionBenchmarkAfterBreeding = true;
}
