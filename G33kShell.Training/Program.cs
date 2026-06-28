using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using G33kShell.Desktop.Console;
using G33kShell.Desktop.Console.Screensavers.AI;
using G33kShell.Desktop.Console.Screensavers.Mario;

var options = TrainingOptions.Parse(args);
if (options.AnalyzeOffspring)
{
    AnalyzeOffspring(options);
    return;
}

if (options.AnalyzeMutationMatrix)
{
    AnalyzeMutationMatrix(options);
    return;
}

if (options.TraceBrain)
{
    TraceBrain(options);
    return;
}

if (options.EvaluateGames > 0)
{
    EvaluateBrain(options);
    return;
}

var screen = new ScreenData(options.ScreenWidth, options.ScreenHeight);
var seedBrainBytes = options.SeedPopulation ? LoadBrainBytes(options) : null;
var canvas = new HeadlessMarioTrainer(options.ScreenWidth, options.ScreenHeight, options.BrainPath, seedBrainBytes);
using var monitor = new TrainingMonitor(options.Generations);

canvas.StartTraining(screen);
while (canvas.CompletedGeneration < options.Generations && canvas.TrainingActive)
{
    Thread.Sleep(250);
    canvas.PulseTrainingHud(screen);
}

canvas.StopTrainingLoop();
Thread.Sleep(500);
monitor.PrintSummary();

static void AnalyzeOffspring(TrainingOptions options)
{
    var brainBytes = LoadBrainBytes(options);
    var champion = (Brain)new Brain().Load(brainBytes);
    var random = new Random(options.EvaluateSeed);
    var population = new List<(string Kind, double MutationRate, Brain Brain)>();

    for (var i = 0; i < 8; i++)
        population.Add(("Elite", 0.0, (Brain)champion.Clone()));

    var randomCount = Math.Max(1, (int)(80 * options.AnalysisRandomFraction));
    for (var i = 0; i < randomCount; i++)
        population.Add(("Random", 0.0, new Brain()));

    var mutationMultipliers = new[] { 0.25, 0.5, 1.0, 2.0 };
    var childIndex = 0;
    while (population.Count < 80)
    {
        var mutationRate = options.AnalysisMutationRate * mutationMultipliers[childIndex++ % mutationMultipliers.Length];
        var child = (Brain)champion.Clone();
        child.Mutate(mutationRate, random);
        population.Add(("Child", mutationRate, child));
    }

    var baseline = EvaluateSingleBrain((Brain)champion.Clone(), options.EvaluateSeed);
    Console.WriteLine(
        $"Enemy-free offspring analysis|Parent X {baseline.X:F0}|Finished {(baseline.Finished ? 1 : 0)}" +
        $"|Population {population.Count}|Base mutation {options.AnalysisMutationRate:F4}|Random {options.AnalysisRandomFraction:P0}");

    var results = new List<(string Kind, double MutationRate, OffspringOutcome Outcome)>();
    for (var i = 0; i < population.Count; i++)
    {
        var entry = population[i];
        var outcome = EvaluateSingleBrain(entry.Brain, options.EvaluateSeed);
        results.Add((entry.Kind, entry.MutationRate, outcome));
        Console.WriteLine(
            $"#{i:00}|{entry.Kind}|Mut {entry.MutationRate:F5}|X {outcome.X:F0}" +
            $"|Finished {(outcome.Finished ? 1 : 0)}|Ticks {outcome.Ticks}");
    }

    Console.WriteLine("Summary:");
    foreach (var group in results.GroupBy(o => (o.Kind, o.MutationRate)).OrderBy(o => o.Key.Kind).ThenBy(o => o.Key.MutationRate))
    {
        var outcomes = group.Select(o => o.Outcome).ToArray();
        Console.WriteLine(
            $"  {group.Key.Kind,-6} Mut {group.Key.MutationRate:F5}|Count {outcomes.Length}" +
            $"|Finished {outcomes.Count(o => o.Finished)}|X avg {outcomes.Average(o => o.X):F0}" +
            $" min {outcomes.Min(o => o.X):F0} max {outcomes.Max(o => o.X):F0}");
    }
}

static void AnalyzeMutationMatrix(TrainingOptions options)
{
    var champion = (Brain)new Brain().Load(LoadBrainBytes(options));
    var baseline = EvaluateSingleBrain((Brain)champion.Clone(), options.EvaluateSeed);
    var mutationCounts = new[] { 1, 4, 16, 64, 256 };
    var mutationStrengths = new[] { 0.02, 0.10, 0.25, 0.50 };
    var outputMutationCounts = new[] { 1, 2, 4, 8, 16 };
    var outputMutationStrengths = new[] { 0.05, 0.10, 0.25, 0.50 };
    var broadMutationRates = new[] { 0.003, 0.006, 0.012, 0.025, 0.050 };
    const int samplesPerCombination = 16;
    var random = new Random(options.EvaluateSeed);

    Console.WriteLine(
        $"Enemy-free mutation matrix|Parent X {baseline.X:F0}|Finished {(baseline.Finished ? 1 : 0)}" +
        $"|Samples {samplesPerCombination} per combination");
    foreach (var mutationCount in mutationCounts)
    {
        foreach (var mutationStrength in mutationStrengths)
        {
            var outcomes = new OffspringOutcome[samplesPerCombination];
            for (var sample = 0; sample < samplesPerCombination; sample++)
            {
                var child = (Brain)champion.Clone();
                child.Mutate(mutationCount, mutationStrength, random);
                outcomes[sample] = EvaluateSingleBrain(child, options.EvaluateSeed);
            }

            PrintMutationOutcome(
                $"Exact {mutationCount,3} @ {mutationStrength:F2}",
                baseline,
                outcomes);
        }
    }

    foreach (var mutationRate in broadMutationRates)
    {
        var outcomes = new OffspringOutcome[samplesPerCombination];
        for (var sample = 0; sample < samplesPerCombination; sample++)
        {
            var child = (Brain)champion.Clone();
            child.Mutate(mutationRate, random);
            outcomes[sample] = EvaluateSingleBrain(child, options.EvaluateSeed);
        }

        PrintMutationOutcome(
            $"Broad {mutationRate:P1}",
            baseline,
            outcomes);
    }

    foreach (var mutationCount in outputMutationCounts)
    {
        foreach (var mutationStrength in outputMutationStrengths)
        {
            var outcomes = new OffspringOutcome[samplesPerCombination];
            for (var sample = 0; sample < samplesPerCombination; sample++)
            {
                var child = (Brain)champion.Clone();
                child.MutateOutput(2, mutationCount, mutationStrength, random);
                outcomes[sample] = EvaluateSingleBrain(child, options.EvaluateSeed);
            }

            PrintMutationOutcome(
                $"Jump  {mutationCount,3} @ {mutationStrength:F2}",
                baseline,
                outcomes);
        }
    }
}

static void PrintMutationOutcome(string label, OffspringOutcome baseline, OffspringOutcome[] outcomes)
{
    const double progressTolerance = 0.5;
    var improved = outcomes.Count(o => o.Finished && !baseline.Finished ||
                                       o.Finished == baseline.Finished && o.X > baseline.X + progressTolerance);
    var preserved = outcomes.Count(o => o.Finished == baseline.Finished &&
                                        Math.Abs(o.X - baseline.X) <= progressTolerance);
    var regressed = outcomes.Length - improved - preserved;
    Console.WriteLine(
        $"{label}|Better {improved,2}|Same {preserved,2}|Worse {regressed,2}" +
        $"|Finished {outcomes.Count(o => o.Finished),2}/{outcomes.Length}" +
        $"|X avg {outcomes.Average(o => o.X):F0} min {outcomes.Min(o => o.X):F0} max {outcomes.Max(o => o.X):F0}");
}

static OffspringOutcome EvaluateSingleBrain(Brain brain, int seed)
{
    var game = new Game(256, 240, brain, useTrainingTimeouts: true, enableEnemies: false)
    {
        GameRand = new Random(seed)
    };
    game.ResetGame();
    while (!game.IsGameOver)
        game.Tick();

    return new OffspringOutcome(game.BestX, game.HasReachedFlagPole, game.Ticks);
}

static void TraceBrain(TrainingOptions options)
{
    var brain = (Brain)new Brain().Load(LoadBrainBytes(options));
    var game = new Game(options.ScreenWidth, options.ScreenHeight, brain, useTrainingTimeouts: true, enableEnemies: false)
    {
        GameRand = new Random(options.EvaluateSeed)
    };
    var state = new GameState(game);
    var trace = new Queue<string>();
    game.ResetGame();
    while (!game.IsGameOver)
    {
        var move = brain.ChooseMove(state);
        trace.Enqueue(
            $"Tick {game.Ticks,4}|X {game.MarioX,7:F1}|Y {game.MarioY,6:F1}" +
            $"|VX {game.MarioVelocityX,5:F2}|VY {game.MarioVelocityY,5:F2}" +
            $"|Ground {(game.IsGrounded ? 1 : 0)}|Held {(game.IsJumpHeld ? 1 : 0)}" +
            $"|Jump {move.JumpSignal,8:F3}|Right {(move.Right ? 1 : 0)}|Run {(move.Run ? 1 : 0)}");
        while (trace.Count > 30)
            trace.Dequeue();
        game.Tick();
    }

    Console.WriteLine($"Trace complete|X {game.BestX:F0}|Ticks {game.Ticks}|Finished {(game.HasReachedFlagPole ? 1 : 0)}");
    foreach (var line in trace)
        Console.WriteLine(line);
}

static byte[] LoadBrainBytes(TrainingOptions options)
{
    if (!options.UseAppBrain)
    {
        if (!File.Exists(options.BrainPath))
            throw new FileNotFoundException("Brain file not found.", options.BrainPath);
        return File.ReadAllBytes(options.BrainPath);
    }

    var settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "G33kShell",
        "settings.json");
    if (!File.Exists(settingsPath))
        throw new FileNotFoundException("G33kShell settings file not found.", settingsPath);

    using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
    if (!settings.RootElement.TryGetProperty("MarioBrain", out var brainElement))
        throw new InvalidDataException($"MarioBrain was not found in {settingsPath}.");

    return Convert.FromBase64String(brainElement.GetString() ?? string.Empty);
}

static void EvaluateBrain(TrainingOptions options)
{
    var brainBytes = LoadBrainBytes(options);
    var completed = 0;
    var fell = 0;
    var enemyDeaths = 0;
    var totalRating = 0.0;
    var totalX = 0.0;
    for (var gameIndex = 0; gameIndex < options.EvaluateGames; gameIndex++)
    {
        var brain = (Brain)new Brain().Load(brainBytes);
        var game = new Game(options.ScreenWidth, options.ScreenHeight, brain, useTrainingTimeouts: true, enableEnemies: true)
        {
            GameRand = new Random(unchecked(options.EvaluateSeed + gameIndex * 37))
        };
        game.ResetGame();
        while (!game.IsGameOver)
            game.Tick();

        var stats = game.ExtraGameStats().ToDictionary(o => o.Name, o => o.Value, StringComparer.OrdinalIgnoreCase);
        completed += stats.TryGetValue("Finished", out var finished) && finished == "1" ? 1 : 0;
        fell += stats.ContainsKey("Fell") ? 1 : 0;
        enemyDeaths += stats.ContainsKey("EnemyDeath") ? 1 : 0;
        totalRating += game.Rating;
        totalX += game.BestX;
    }

    Console.WriteLine(
        $"Replay {options.EvaluateGames} games|Finished {completed} ({completed * 100.0 / options.EvaluateGames:F1}%)" +
        $"|AverageX {totalX / options.EvaluateGames:F1}|AverageRating {totalRating / options.EvaluateGames:F1}" +
        $"|Fell {fell}|EnemyDeath {enemyDeaths}");
}

internal sealed class HeadlessMarioTrainer : MarioCanvas
{
    private readonly string m_brainPath;
    private readonly byte[] m_seedBrainBytes;
    private volatile int m_completedGeneration;

    public HeadlessMarioTrainer(int screenWidth, int screenHeight, string brainPath, byte[] seedBrainBytes) : base(screenWidth, screenHeight)
    {
        m_brainPath = brainPath;
        m_seedBrainBytes = seedBrainBytes;
        ActivationName = "mario_train";
    }

    public int CompletedGeneration => m_completedGeneration;
    public bool TrainingActive => IsTraining;

    public void StartTraining(ScreenData screen) =>
        TrainAi(screen, SaveBrain);

    public void PulseTrainingHud(ScreenData screen) =>
        TrainAi(screen, SaveBrain);

    public void StopTrainingLoop() =>
        StopTraining();

    protected override byte[] GetSavedBrainBytes() =>
        File.Exists(m_brainPath) ? File.ReadAllBytes(m_brainPath) : null;

    protected override IEnumerable<AiBrainBase> CreateInitialPopulation()
    {
        if (m_seedBrainBytes == null)
            return base.CreateInitialPopulation();

        var champion = (Brain)new Brain().Load(m_seedBrainBytes);
        return Enumerable.Range(0, 80).Select(_ => champion.Clone());
    }

    protected override void OnTrainingGenerationComplete(TrainingGenerationResult result)
    {
        base.OnTrainingGenerationComplete(result);
        m_completedGeneration = result.Generation;
    }

    private void SaveBrain(byte[] brainBytes)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(m_brainPath));
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllBytes(m_brainPath, brainBytes);
    }
}

internal sealed class TrainingMonitor : TextWriter
{
    private static readonly Regex s_generationRegex = new Regex(@"^Gen (?<gen>\d+)\|.*?GOAT (?<goat>[-0-9.]+)\|Current (?<current>[-0-9.]+|skip)", RegexOptions.Compiled);
    private static readonly Regex s_degeneracyRegex = new Regex(@"\|Deg (?<deg>[-0-9.]+|skip)", RegexOptions.Compiled);
    private readonly TextWriter m_inner = Console.Out;
    private readonly int m_targetGenerations;
    private int m_generationCount;
    private int m_savedCount;
    private int m_zeroCurrentCount;
    private double m_lastGoat;
    private double m_lastCurrent;
    private double? m_lastDegeneracy;

    public override Encoding Encoding => m_inner.Encoding;

    public TrainingMonitor(int targetGenerations)
    {
        m_targetGenerations = targetGenerations;
        Console.SetOut(this);
    }

    public override void WriteLine(string value)
    {
        m_inner.WriteLine(value);
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (value.Contains("|Saved", StringComparison.Ordinal))
            m_savedCount++;

        var match = s_generationRegex.Match(value);
        if (!match.Success)
            return;

        m_generationCount++;
        m_lastGoat = ParseDouble(match.Groups["goat"].Value);
        var currentText = match.Groups["current"].Value;
        m_lastCurrent = ParseDouble(currentText);
        var degeneracyMatch = s_degeneracyRegex.Match(value);
        if (degeneracyMatch.Success)
            m_lastDegeneracy = ParseDouble(degeneracyMatch.Groups["deg"].Value);
        if (double.TryParse(currentText, out _) && m_lastCurrent <= 0.0)
            m_zeroCurrentCount++;
    }

    public void PrintSummary()
    {
        m_inner.WriteLine();
        var degeneracyText = m_lastDegeneracy.HasValue ? $", last Deg {m_lastDegeneracy:F2}" : string.Empty;
        m_inner.WriteLine($"Training monitor: {m_generationCount}/{m_targetGenerations} generations observed, saves {m_savedCount}, GOAT {m_lastGoat:F1}, current {m_lastCurrent:F1}{degeneracyText}.");
        if (m_generationCount > 0 && m_zeroCurrentCount == m_generationCount)
            m_inner.WriteLine("Warning: every observed generation had Current 0.0. Validation may still be too hard.");
        else if (m_savedCount == 0)
            m_inner.WriteLine("Warning: no improvements were saved during this run.");
    }

    protected override void Dispose(bool disposing)
    {
        Console.SetOut(m_inner);
        base.Dispose(disposing);
    }

    private static double ParseDouble(string value) =>
        double.TryParse(value, out var result) ? result : 0.0;
}

internal readonly record struct OffspringOutcome(double X, bool Finished, int Ticks);

internal sealed record TrainingOptions(
    int Generations,
    string BrainPath,
    int ScreenWidth,
    int ScreenHeight,
    int EvaluateGames,
    int EvaluateSeed,
    bool AnalyzeOffspring,
    bool AnalyzeMutationMatrix,
    bool TraceBrain,
    bool UseAppBrain,
    bool SeedPopulation,
    double AnalysisMutationRate,
    double AnalysisRandomFraction)
{
    public static TrainingOptions Parse(string[] args)
    {
        var generations = 25;
        var brainPath = Path.Combine(Environment.CurrentDirectory, "artifacts", "mario.brain");
        var width = 256;
        var height = 240;
        var evaluateGames = 0;
        var evaluateSeed = 1_000_000;
        var analyzeOffspring = false;
        var analyzeMutationMatrix = false;
        var traceBrain = false;
        var useAppBrain = false;
        var seedPopulation = false;
        var analysisMutationRate = 0.015;
        var analysisRandomFraction = 0.18;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--generations" when i + 1 < args.Length:
                case "-g" when i + 1 < args.Length:
                    generations = int.Parse(args[++i]);
                    break;
                case "--brain" when i + 1 < args.Length:
                    brainPath = args[++i];
                    break;
                case "--width" when i + 1 < args.Length:
                    width = int.Parse(args[++i]);
                    break;
                case "--height" when i + 1 < args.Length:
                    height = int.Parse(args[++i]);
                    break;
                case "--evaluate" when i + 1 < args.Length:
                    evaluateGames = int.Parse(args[++i]);
                    break;
                case "--seed" when i + 1 < args.Length:
                    evaluateSeed = int.Parse(args[++i]);
                    break;
                case "--analyze-offspring":
                    analyzeOffspring = true;
                    break;
                case "--analyze-mutation-matrix":
                    analyzeMutationMatrix = true;
                    break;
                case "--trace-brain":
                    traceBrain = true;
                    break;
                case "--app-brain":
                    useAppBrain = true;
                    break;
                case "--seed-population":
                    seedPopulation = true;
                    break;
                case "--mutation" when i + 1 < args.Length:
                    analysisMutationRate = double.Parse(args[++i]);
                    break;
                case "--random-fraction" when i + 1 < args.Length:
                    analysisRandomFraction = double.Parse(args[++i]);
                    break;
            }
        }

        return new TrainingOptions(
            Math.Max(1, generations),
            brainPath,
            width,
            height,
            Math.Max(0, evaluateGames),
            evaluateSeed,
            analyzeOffspring,
            analyzeMutationMatrix,
            traceBrain,
            useAppBrain,
            seedPopulation,
            Math.Clamp(analysisMutationRate, 0.0, 1.0),
            Math.Clamp(analysisRandomFraction, 0.0, 1.0));
    }
}
