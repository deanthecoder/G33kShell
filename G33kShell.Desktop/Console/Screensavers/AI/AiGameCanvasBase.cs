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
/// <see cref="AiGameBase.Rating"/>, then validates the strongest candidates and the persisted
/// champion on a separate shared seed set. The evolving population starts independently and each
/// next generation contains current elites, fresh random brains, and crossover/mutation children.
/// The persisted champion remains an external benchmark and is replaced only when a challenger
/// beats it on the same validation games.
/// </remarks>
public abstract class AiGameCanvasBase : ScreensaverBase
{
    protected readonly record struct TrainingGenerationResult(
        int Generation,
        double? CurrentRating,
        double GoatRating);
    private readonly record struct TrainingPoint(double? CurrentRating, double GoatRating);

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
    private readonly object m_trainingHistoryLock = new object();
    private readonly List<TrainingPoint> m_trainingHistory = new List<TrainingPoint>();
    private int m_generation;
    private double m_championRating;
    private double m_goatRatingTotal;
    private int m_goatRatingCount;
    private int m_generationsSinceImprovement;
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

    public sealed override void UpdateFrame(ScreenData screen)
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
        m_goatRatingTotal = 0.0;
        m_goatRatingCount = 0;
        System.Console.WriteLine("Starting training from random brains...");
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
                System.Console.WriteLine($"  Brain layers: {brain.InputSize} : {brain.HiddenLayers.ToCsv(' ')} : {brain.OutputSize}");
                System.Console.WriteLine($"   Generations: {m_generation}");
                System.Console.WriteLine($"    GOAT rating: {GetAverageGoatRating():F1}");
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
        var gamesPerBrain = GetGamesPerBrain();
        var validationGamesPerBrain = GetValidationGamesPerBrain();
        
        var gameResults = new (double AverageRating, double AverageDegeneracy, string DegeneracyReason, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats, string AverageStats)[m_nextGenBrains.Count];
        var trainingTotals = new double[m_nextGenBrains.Count];
        var trainingDegeneracyTotals = new double[m_nextGenBrains.Count];
        var trainingBestRatings = new double[m_nextGenBrains.Count];
        var trainingBestSeeds = new int[m_nextGenBrains.Count];
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

        if (AbortInterruptedGeneration())
            return;

        for (var i = 0; i < m_nextGenBrains.Count; i++)
        {
            gameResults[i] = (
                trainingTotals[i] / gamesPerBrain,
                trainingDegeneracyTotals[i] / gamesPerBrain,
                trainingReasons[i],
                trainingBestRatings[i],
                trainingBestSeeds[i],
                m_nextGenBrains[i],
                trainingBestStats[i] ?? string.Empty,
                FormatAverageStats(trainingStatsTotals[i], gamesPerBrain));
        }

        // Select the breeders.
        var orderedGames = gameResults.OrderByDescending(GetSelectionFitness).ToArray();
        var theBest = orderedGames[0];
        var bestTrainingFitness = GetSelectionFitness(theBest);
        var useTrainingScoreForGoat = UseTrainingScoreForGoat();
        var skippedValidation = useTrainingScoreForGoat ||
                                (m_persistedChampionBrain == null && ShouldSkipValidation(bestTrainingFitness, m_championRating));
        var bestValidation = theBest;
        if (!useTrainingScoreForGoat && !skippedValidation)
        {
            var validationCandidateCount = Math.Min(GetValidationCandidateCount(), orderedGames.Length);
            var championValidationIndex = m_persistedChampionBrain == null ? -1 : validationCandidateCount;
            var validationResultCount = validationCandidateCount + (championValidationIndex >= 0 ? 1 : 0);
            var validationResults = new (double AverageRating, double AverageDegeneracy, string DegeneracyReason, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats, string AverageStats)[validationResultCount];
            var validationTotals = new double[validationResultCount];
            var validationDegeneracyTotals = new double[validationResultCount];
            var validationBestRatings = new double[validationResultCount];
            var validationBestSeeds = new int[validationResultCount];
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
                    var sourceBrain = candidateIndex == championValidationIndex
                        ? m_persistedChampionBrain
                        : orderedGames[candidateIndex].Brain;
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

            if (AbortInterruptedGeneration())
                return;

            for (var i = 0; i < validationResultCount; i++)
            {
                var sourceBrain = i == championValidationIndex
                    ? m_persistedChampionBrain
                    : orderedGames[i].Brain;
                validationResults[i] = (
                    validationTotals[i] / validationGamesPerBrain,
                    validationDegeneracyTotals[i] / validationGamesPerBrain,
                    validationReasons[i],
                    validationBestRatings[i],
                    validationBestSeeds[i],
                    sourceBrain,
                    validationBestStats[i] ?? string.Empty,
                    FormatAverageStats(validationStatsTotals[i], validationGamesPerBrain));
            }

            bestValidation = validationResults.Take(validationCandidateCount).OrderByDescending(GetSelectionFitness).First();
            if (championValidationIndex >= 0)
                UpdateChampionRating(GetSelectionFitness(validationResults[championValidationIndex]));
        }

        if (AbortInterruptedGeneration())
            return;

        UpdateExplorationBoost(bestValidation);
        var mutationRate = GetEffectiveMutationRate();
        var randomFraction = GetEffectiveRandomFraction();
        var bestEvalFitness = useTrainingScoreForGoat
            ? bestTrainingFitness
            : skippedValidation
                ? 0.0
                : GetSelectionFitness(bestValidation);
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
        var hadGoat = m_persistedChampionBrain != null;
        if (useTrainingScoreForGoat)
            savedImprovement = PersistBrainImprovement(theBest, saveBrainBytes);
        else if (!skippedValidation)
            savedImprovement = PersistBrainImprovement(bestValidation, saveBrainBytes);
        else
            m_generationsSinceImprovement++;

        if (savedImprovement)
            ResetGoatRatingAverage(bestEvalFitness);
        else if (hadGoat && (useTrainingScoreForGoat || !skippedValidation))
            AddGoatRatingSample(evaluatedGoatRating);

        OnTrainingGenerationComplete(new TrainingGenerationResult(
            m_generation,
            useTrainingScoreForGoat ? bestTrainingFitness : !skippedValidation ? bestEvalFitness : null,
            GetAverageGoatRating()));

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
        var stats = $"Gen {m_generation}|Pop {m_currentPopSize}|GOAT {GetAverageGoatRating():F1}|Current {currentText}|VsGOAT {currentVsGoatText}|SinceGOAT {GetGoatAgeSeconds()}s";
        if (UseDegeneracyPenalty())
            stats += $"|Deg {degText}";
        stats += $"|Mut {mutationRate:F3}|Rnd {randomFraction:F2}";
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
        System.Console.WriteLine(stats);

        // Build the brains for the next generation.
        m_nextGenBrains = UseHarnessStyleEvolution()
            ? BuildHarnessStyleNextGeneration(orderedGames, mutationRate, randomFraction)
            : BuildLegacyNextGeneration(orderedGames, mutationRate, randomFraction);

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

    private static string FormatAverageStats(Dictionary<string, double> totals, int sampleCount)
    {
        if (sampleCount <= 0 || totals.Count == 0)
            return string.Empty;

        return totals.Select(o => $"{o.Key} {o.Value / sampleCount:0.###}").ToCsv('|');
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
            m_windowManager.ClearPixelScreen();
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

    private bool PersistBrainImprovement((double AverageRating, double AverageDegeneracy, string DegeneracyReason, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats, string AverageStats) theBest, Action<byte[]> saveBrainBytes)
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

            UpdateChampionRating(effectiveFitness);
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

    private void UpdateChampionRating(double rating)
    {
        m_championRating = rating;
    }

    private void AddGoatRatingSample(double rating)
    {
        m_goatRatingTotal += rating;
        m_goatRatingCount++;
    }

    private void ResetGoatRatingAverage(double rating)
    {
        m_goatRatingTotal = rating;
        m_goatRatingCount = 1;
    }

    private double GetAverageGoatRating() =>
        m_goatRatingCount > 0 ? m_goatRatingTotal / m_goatRatingCount : 0.0;

    protected virtual string GetPersistBlockReason(double averageDegeneracy, string bestStats, string averageStats) => null;

    private List<AiBrainBase> BuildLegacyNextGeneration((double AverageRating, double AverageDegeneracy, string DegeneracyReason, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats, string AverageStats)[] orderedGames, double mutationRate, double randomFraction)
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

    private List<AiBrainBase> BuildHarnessStyleNextGeneration((double AverageRating, double AverageDegeneracy, string DegeneracyReason, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats, string AverageStats)[] orderedGames, double mutationRate, double randomFraction)
    {
        var nextBrains = new List<AiBrainBase>(m_currentPopSize);
        var eliteCount = Math.Min(GetEliteCount(), orderedGames.Length);
        for (var i = 0; i < eliteCount && nextBrains.Count < m_currentPopSize; i++)
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

    private AiBrainBase SelectParent((double AverageRating, double AverageDegeneracy, string DegeneracyReason, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats, string AverageStats)[] orderedGames)
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

    private double GetSelectionFitness((double AverageRating, double AverageDegeneracy, string DegeneracyReason, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats, string AverageStats) result)
    {
        if (!UseDegeneracyPenalty())
            return result.AverageRating;

        var degeneracy = Math.Clamp(result.AverageDegeneracy, 0.0, 1.0);
        var penaltyFactor = Math.Max(0.02, 1.0 - degeneracy * degeneracy * 0.98);
        return result.AverageRating * penaltyFactor;
    }

    private double GetEffectiveRandomFraction()
    {
        var randomFraction = GetRandomFraction();
        if (m_explorationBoostGenerationsRemaining > 0)
            randomFraction = Math.Max(randomFraction, 0.18);

        return randomFraction;
    }

    private void UpdateExplorationBoost((double AverageRating, double AverageDegeneracy, string DegeneracyReason, double BestRating, int GameSeed, AiBrainBase Brain, string GameStats, string AverageStats) bestValidation)
    {
        if (m_explorationBoostGenerationsRemaining > 0)
            return;

        var useDegeneracy = UseDegeneracyPenalty();
        var severeDegeneracy = useDegeneracy && bestValidation.AverageDegeneracy >= 0.85;
        var stubbornDegeneracy = useDegeneracy && bestValidation.AverageDegeneracy >= 0.60 && m_generationsSinceImprovement >= 12;
        var hardStagnation = m_generationsSinceImprovement >= 40;
        if (!severeDegeneracy && !stubbornDegeneracy && !hardStagnation)
            return;

        m_explorationBoostGenerationsRemaining = severeDegeneracy ? 20 : 12;
        m_lastExplorationReason = !string.IsNullOrWhiteSpace(bestValidation.DegeneracyReason)
            ? bestValidation.DegeneracyReason
            : hardStagnation
                ? "stagnation"
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
    protected virtual bool UseDegeneracyPenalty() => true;
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
    protected virtual void OnAfterDrawTrainingGraph(PixelScreenData screen) { }
    protected abstract void UpdateGameFrame(ScreenData screen);
    protected abstract void SaveBrainBytes(byte[] brainBytes);
    protected abstract AiGameBase CreateGame(AiBrainBase brain);
    protected abstract AiBrainBase CreateBrain();
}
