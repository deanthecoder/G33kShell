using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using G33kShell.Desktop.Console.Screensavers.AI;
using G33kShell.Desktop.Console.Screensavers.Asteroids;

namespace G33kShell.TrainingHarness;

internal static class Program
{
    private const int ArenaWidth = 120;
    private const int ArenaHeight = 80;
    private const int TrainingSeedBase = 7331;
    private const int ValidationSeedBase = TrainingSeedBase + 1_000_000;
    private const int FinalEvalSeedBase = TrainingSeedBase + 2_000_000;

    private sealed class Options
    {
        public int Generations { get; init; } = 200;
        public int Population { get; init; } = 160;
        public int GamesPerBrain { get; init; } = 6;
        public int ValidationGames { get; init; } = 6;
        public int FinalEvalGames { get; init; } = 24;
        public int ValidationCandidates { get; init; } = 3;
        public int EliteCount { get; init; } = 5;
        public double RandomFraction { get; init; } = 0.05;
        public double CrossoverRate { get; init; } = 0.5;
        public double MutationRate { get; init; } = 0.05;
        public int LogEvery { get; init; } = 1;
        public int BreedingSeed { get; init; } = TrainingSeedBase;
        public string SavePath { get; init; }
        public bool Fresh { get; init; } = true;
    }

    private sealed class State
    {
        public double BestFitness { get; set; }
        public int GenerationsSinceImprovement { get; set; }
        public int ExplorationBoostGenerationsRemaining { get; set; }
        public string LastExplorationReason { get; set; } = string.Empty;
        public AiBrainBase BestBrain { get; set; }
    }

    private readonly record struct EvalResult(
        double AverageRating,
        double AverageDegeneracy,
        string DegeneracyReason,
        double BestRating,
        int BestSeed,
        AiBrainBase Brain,
        string GameStats);

    private readonly record struct EvalSummary(
        double AverageRating,
        double AverageDegeneracy,
        double AverageSelectionFitness,
        string BestStats,
        int BestSeed);

    private static int Main(string[] args)
    {
        var options = ParseArgs(args);
        if (options == null)
            return 1;

        var brains = CreateInitialPopulation(options).ToList();
        var state = new State();
        var breedingRandom = new Random(options.BreedingSeed);

        for (var generation = 1; generation <= options.Generations; generation++)
        {
            var trainResults = EvaluatePopulation(options, brains, generation, isValidation: false);
            var ordered = trainResults.OrderByDescending(GetSelectionFitness).ToArray();

            var validationCount = Math.Min(options.ValidationCandidates, ordered.Length);
            var validationResults = EvaluateCandidates(options, ordered, generation, validationCount);
            var bestValidation = validationResults.OrderByDescending(GetSelectionFitness).First();

            UpdateExplorationBoost(state, bestValidation);
            var mutationRate = GetEffectiveMutationRate(options, state, generation);
            var randomFraction = GetEffectiveRandomFraction(options, state);
            var bestValidationFitness = GetSelectionFitness(bestValidation);

            if (bestValidationFitness > state.BestFitness)
            {
                state.BestFitness = bestValidationFitness;
                state.BestBrain = bestValidation.Brain.Clone();
                state.GenerationsSinceImprovement = 0;
            }
            else
            {
                state.GenerationsSinceImprovement++;
            }

            if (generation == 1 || generation % Math.Max(1, options.LogEvery) == 0 || generation == options.Generations)
            {
                var line = $"Gen {generation}|Pop {brains.Count}|GOAT {state.BestFitness:F1}|Train {ordered[0].AverageRating:F1}|Eval {bestValidation.AverageRating:F1}|Fit {bestValidationFitness:F1}|Mut {mutationRate:F3}|Rnd {randomFraction:F2}|Deg {bestValidation.AverageDegeneracy:F2}|Seed {ordered[0].BestSeed}";
                if (!string.IsNullOrEmpty(ordered[0].GameStats))
                    line += $"|{ordered[0].GameStats}";
                if (state.ExplorationBoostGenerationsRemaining > 0)
                    line += $"|Boost {state.LastExplorationReason}";
                Console.WriteLine(line);
            }

            brains = BuildNextGeneration(options, ordered, mutationRate, randomFraction, breedingRandom);
            if (state.ExplorationBoostGenerationsRemaining > 0)
                state.ExplorationBoostGenerationsRemaining--;
        }

        if (state.BestBrain == null)
        {
            Console.WriteLine("No best brain was produced.");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(options.SavePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.SavePath)) ?? ".");
            File.WriteAllBytes(options.SavePath, state.BestBrain.Save());
            Console.WriteLine($"Saved best brain to {Path.GetFullPath(options.SavePath)}");
        }

        var finalSummary = EvaluateBestBrain(options, state.BestBrain);
        Console.WriteLine($"Final|Eval {finalSummary.AverageRating:F1}|Fit {finalSummary.AverageSelectionFitness:F1}|Deg {finalSummary.AverageDegeneracy:F2}|Seed {finalSummary.BestSeed}|{finalSummary.BestStats}");
        return 0;
    }

    private static Options ParseArgs(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
                continue;

            var key = arg[2..];
            string value;
            if (key.Contains('='))
            {
                var parts = key.Split('=', 2);
                key = parts[0];
                value = parts[1];
            }
            else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++i];
            }
            else
            {
                value = "true";
            }

            values[key] = value;
        }

        if (values.ContainsKey("help") || values.ContainsKey("h"))
        {
            Console.WriteLine("Usage: dotnet run --project G33kShell.TrainingHarness -- [--generations N] [--population N] [--games-per-brain N] [--validation-games N] [--final-eval-games N] [--validation-candidates N] [--log-every N] [--breeding-seed N] [--save path]");
            return null;
        }

        return new Options
        {
            Generations = GetInt(values, "generations", 200),
            Population = GetInt(values, "population", 160),
            GamesPerBrain = GetInt(values, "games-per-brain", 6),
            ValidationGames = GetInt(values, "validation-games", 6),
            FinalEvalGames = GetInt(values, "final-eval-games", 24),
            ValidationCandidates = GetInt(values, "validation-candidates", 3),
            EliteCount = GetInt(values, "elite-count", 5),
            LogEvery = GetInt(values, "log-every", 1),
            BreedingSeed = GetInt(values, "breeding-seed", TrainingSeedBase),
            RandomFraction = GetDouble(values, "random-fraction", 0.05),
            CrossoverRate = GetDouble(values, "crossover-rate", 0.5),
            MutationRate = GetDouble(values, "mutation-rate", 0.05),
            SavePath = GetString(values, "save"),
            Fresh = GetBool(values, "fresh", true)
        };
    }

    private static IEnumerable<AiBrainBase> CreateInitialPopulation(Options options)
    {
        for (var i = 0; i < options.Population; i++)
            yield return new Brain();
    }

    private static EvalResult[] EvaluatePopulation(Options options, IReadOnlyList<AiBrainBase> brains, int generation, bool isValidation)
    {
        var results = new EvalResult[brains.Count];
        Parallel.For(0, brains.Count, brainIndex =>
        {
            results[brainIndex] = EvaluateBrain(
                brains[brainIndex],
                options.GamesPerBrain,
                gameIndex => GetTrainingSeed(generation, brainIndex, gameIndex),
                gameIndex => CreateGame(brains[brainIndex], generation, isValidation, brainIndex, gameIndex));
        });
        return results;
    }

    private static EvalResult[] EvaluateCandidates(Options options, EvalResult[] ordered, int generation, int validationCount)
    {
        var results = new EvalResult[validationCount];
        Parallel.For(0, validationCount, candidateIndex =>
        {
            results[candidateIndex] = EvaluateBrain(
                ordered[candidateIndex].Brain,
                options.ValidationGames,
                gameIndex => GetValidationSeed(candidateIndex, gameIndex),
                gameIndex => CreateGame(ordered[candidateIndex].Brain, generation, isValidation: true, candidateIndex, gameIndex));
        });
        return results;
    }

    private static EvalSummary EvaluateBestBrain(Options options, AiBrainBase bestBrain)
    {
        var result = EvaluateBrain(
            bestBrain,
            options.FinalEvalGames,
            gameIndex => FinalEvalSeedBase + gameIndex * 53,
            gameIndex => CreateFinalEvalGame(bestBrain, gameIndex));

        return new EvalSummary(
            result.AverageRating,
            result.AverageDegeneracy,
            GetSelectionFitness(result),
            result.GameStats,
            result.BestSeed);
    }

    private static EvalResult EvaluateBrain(AiBrainBase brain, int games, Func<int, int> seedProvider, Func<int, Game> gameFactory)
    {
        var totalRating = 0.0;
        var totalDegeneracy = 0.0;
        var bestRating = double.MinValue;
        var bestSeed = 0;
        var bestStats = string.Empty;
        var degeneracyReason = string.Empty;

        for (var gameIndex = 0; gameIndex < games; gameIndex++)
        {
            var seed = seedProvider(gameIndex);
            var game = gameFactory(gameIndex);
            game.GameRand = new Random(seed);
            game.ResetGame();
            while (!game.IsGameOver)
                game.Tick();

            totalRating += game.Rating;
            totalDegeneracy += game.DegeneracyScore;
            if (string.IsNullOrEmpty(degeneracyReason) && !string.IsNullOrEmpty(game.DegeneracyReason))
                degeneracyReason = game.DegeneracyReason;

            if (game.Rating > bestRating)
            {
                bestRating = game.Rating;
                bestSeed = seed;
                bestStats = string.Join('|', game.ExtraGameStats().Select(o => $"{o.Name} {o.Value}"));
            }
        }

        return new EvalResult(totalRating / games, totalDegeneracy / games, degeneracyReason, bestRating, bestSeed, brain, bestStats);
    }

    private static List<AiBrainBase> BuildNextGeneration(Options options, EvalResult[] ordered, double mutationRate, double randomFraction, Random breedingRandom)
    {
        var nextBrains = new List<AiBrainBase>(options.Population);
        var eliteCount = Math.Min(options.EliteCount, ordered.Length);
        for (var i = 0; i < eliteCount; i++)
            nextBrains.Add(ordered[i].Brain.Clone());

        var randomCount = Math.Max(1, (int)(options.Population * randomFraction));
        for (var i = 0; i < randomCount && nextBrains.Count < options.Population; i++)
            nextBrains.Add(new Brain());

        while (nextBrains.Count < options.Population)
        {
            var mumBrain = SelectParent(ordered, breedingRandom);
            var dadBrain = SelectParent(ordered, breedingRandom);
            var child = mumBrain.Clone().CrossWith(dadBrain, options.CrossoverRate, breedingRandom).Mutate(mutationRate, breedingRandom);
            nextBrains.Add(child);
        }

        return nextBrains;
    }

    private static AiBrainBase SelectParent(IReadOnlyList<EvalResult> ordered, Random random)
    {
        var totalFitness = 0.0;
        for (var i = 0; i < ordered.Count; i++)
            totalFitness += Math.Max(1.0, GetSelectionFitness(ordered[i]));

        var target = random.NextDouble() * totalFitness;
        var cumulative = 0.0;
        for (var i = 0; i < ordered.Count; i++)
        {
            cumulative += Math.Max(1.0, GetSelectionFitness(ordered[i]));
            if (target <= cumulative)
                return ordered[i].Brain;
        }

        return ordered[^1].Brain;
    }

    private static Game CreateGame(AiBrainBase brain, int generation, bool isValidation, int candidateIndex, int gameIndex)
    {
        var profile = isValidation ? Game.TrainingProfile.Default : GetCurriculumProfile(generation);
        return new Game(ArenaWidth, ArenaHeight, (Brain)brain, profile);
    }

    private static Game CreateFinalEvalGame(AiBrainBase brain, int gameIndex) =>
        new Game(ArenaWidth, ArenaHeight, (Brain)brain, Game.TrainingProfile.Default);

    private static int GetTrainingSeed(int generation, int brainIndex, int gameIndex) =>
        unchecked(TrainingSeedBase + generation * 10_000 + brainIndex * 101 + gameIndex * 17);

    private static int GetValidationSeed(int candidateIndex, int gameIndex) =>
        unchecked(ValidationSeedBase + candidateIndex * 1009 + gameIndex * 37);

    private static Game.TrainingProfile GetCurriculumProfile(int generation)
    {
        var ramp = Math.Clamp((generation - 1) / 240.0, 0.0, 1.0);
        var asteroidMetric = (int)Math.Round(6 + ramp * 6);
        var speed = (float)(0.07 + ramp * 0.03);
        var aimedSpawnChance = Math.Clamp(ramp - 0.35, 0.0, 0.45);
        return new Game.TrainingProfile(asteroidMetric, speed, aimedSpawnChance);
    }

    private static double GetSelectionFitness(EvalResult result)
    {
        var degeneracy = Math.Clamp(result.AverageDegeneracy, 0.0, 1.0);
        if (degeneracy >= 0.95)
            return 0.0;

        var penaltyFactor = 1.0 - degeneracy * degeneracy * 0.9;
        return result.AverageRating * penaltyFactor;
    }

    private static double GetEffectiveMutationRate(Options options, State state, int generation)
    {
        var baseRate = options.MutationRate;
        var floorRate = Math.Max(0.01, baseRate * 0.35);
        var decay = Math.Min(1.0, Math.Max(0, generation - 1) / 400.0);
        var rate = baseRate + (floorRate - baseRate) * decay;
        if (state.GenerationsSinceImprovement >= 40)
            rate = Math.Min(baseRate, rate * 1.35);
        if (state.ExplorationBoostGenerationsRemaining > 0)
            rate = Math.Min(0.18, Math.Max(rate, baseRate * 2.5));

        return rate;
    }

    private static double GetEffectiveRandomFraction(Options options, State state)
    {
        var randomFraction = options.RandomFraction;
        if (state.ExplorationBoostGenerationsRemaining > 0)
            randomFraction = Math.Max(randomFraction, 0.18);

        return randomFraction;
    }

    private static void UpdateExplorationBoost(State state, EvalResult bestValidation)
    {
        if (state.ExplorationBoostGenerationsRemaining > 0)
            return;

        var severeDegeneracy = bestValidation.AverageDegeneracy >= 0.85;
        var stubbornDegeneracy = bestValidation.AverageDegeneracy >= 0.60 && state.GenerationsSinceImprovement >= 12;
        var hardStagnation = state.GenerationsSinceImprovement >= 40;
        if (!severeDegeneracy && !stubbornDegeneracy && !hardStagnation)
            return;

        state.ExplorationBoostGenerationsRemaining = severeDegeneracy ? 20 : 12;
        state.LastExplorationReason = !string.IsNullOrWhiteSpace(bestValidation.DegeneracyReason)
            ? bestValidation.DegeneracyReason
            : hardStagnation
                ? "stagnation"
                : "degenerate";
    }

    private static int GetInt(Dictionary<string, string> values, string key, int defaultValue) =>
        values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;

    private static double GetDouble(Dictionary<string, string> values, string key, double defaultValue) =>
        values.TryGetValue(key, out var value) && double.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;

    private static string GetString(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static bool GetBool(Dictionary<string, string> values, string key, bool defaultValue) =>
        values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;
}
