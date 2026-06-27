using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using G33kShell.Desktop.Console;
using G33kShell.Desktop.Console.Screensavers.Mario;

var options = TrainingOptions.Parse(args);
if (options.EvaluateGames > 0)
{
    EvaluateBrain(options);
    return;
}

var screen = new ScreenData(options.ScreenWidth, options.ScreenHeight);
var canvas = new HeadlessMarioTrainer(options.ScreenWidth, options.ScreenHeight, options.BrainPath);
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

static void EvaluateBrain(TrainingOptions options)
{
    if (!File.Exists(options.BrainPath))
        throw new FileNotFoundException("Brain file not found.", options.BrainPath);

    var brainBytes = File.ReadAllBytes(options.BrainPath);
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
    private volatile int m_completedGeneration;

    public HeadlessMarioTrainer(int screenWidth, int screenHeight, string brainPath) : base(screenWidth, screenHeight)
    {
        m_brainPath = brainPath;
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

internal sealed record TrainingOptions(int Generations, string BrainPath, int ScreenWidth, int ScreenHeight, int EvaluateGames, int EvaluateSeed)
{
    public static TrainingOptions Parse(string[] args)
    {
        var generations = 25;
        var brainPath = Path.Combine(Environment.CurrentDirectory, "artifacts", "mario.brain");
        var width = 256;
        var height = 240;
        var evaluateGames = 0;
        var evaluateSeed = 1_000_000;

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
            }
        }

        return new TrainingOptions(Math.Max(1, generations), brainPath, width, height, Math.Max(0, evaluateGames), evaluateSeed);
    }
}
