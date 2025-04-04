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
using G33kShell.Desktop.Console.Controls;
using G33kShell.Desktop.Terminal;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

/// <summary>
/// AI-powered snake game.
/// </summary>
[DebuggerDisplay("SnakeCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class SnakeCanvas : ScreensaverBase
{
    private const int PopulationSize = 200;
    private readonly Random m_rand = new Random();
    private int m_trainingGenerationCompleted;
    private int ArenaWidth { get; }
    private int ArenaHeight { get; }
    private List<Game> m_games;
    private double m_savedRating;

    public SnakeCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 60)
    {
        Name = "snake";

        ArenaWidth = screenWidth;
        ArenaHeight = screenHeight;
    }

    public override void UpdateFrame(ScreenData screen)
    {
        screen.ClearChars();

        //TrainAi(screen);
        PlayGame(screen);
    }

    [UsedImplicitly]
    private void PlayGame(ScreenData screen)
    {
        if (m_games == null)
        {
            m_games = [ new Game(ArenaWidth, ArenaHeight) ];
            m_games[0].LoadBrainData(Settings.Instance.SnakeBrain);
        }

        DrawGame(screen, m_games[0]);

        m_games[0].Tick();
    }

    private static void DrawGame(ScreenData screen, Game game)
    {
        screen.PrintAt(game.FoodPosition.X, game.FoodPosition.Y, '\u2665');
        foreach (var segment in game.Snake.Segments)
            screen.PrintAt(segment.X, segment.Y, '■');
        screen.PrintAt(game.Snake.HeadPosition.X, game.Snake.HeadPosition.Y, '☻');
        screen.PrintAt(0, 0, $"Score: {game.Score}, High Score: {game.HighScore}");
    }

    [UsedImplicitly]
    private void TrainAi(ScreenData screen)
    {
        m_games ??= Enumerable.Range(0, PopulationSize).Select(_ => new Game(ArenaWidth, ArenaHeight)).ToList();

        m_games.AsParallel().ForAll(o =>
        {
            while (o.Lives > 0 || !o.Snake.IsDead)
                o.Tick();
        });

        DrawGame(screen, m_games[0]);
        
        var isAllGamesEnded = m_games.All(o => o.Snake.IsDead && o.Lives == 0);
        if (!isAllGamesEnded)
            return;
        
        // Select the breeders.
        var orderedGames = m_games.OrderByDescending(o => o.Rating).ToArray();
        var gameCount = orderedGames.Length;
        var bestGames = orderedGames.Take((int)(gameCount * 0.1)).ToArray();
        var losers = orderedGames.Except(bestGames);
        var luckyLosers = losers.OrderBy(_ => m_rand.Next()).Take((int)(gameCount * 0.05)).ToArray();
        
        // Report summary of results.
        m_trainingGenerationCompleted++;
        var veryBest = bestGames[0];
        System.Console.WriteLine($"Gen {m_trainingGenerationCompleted}, Rating: {veryBest.Rating:F2}, HighScore: {veryBest.HighScore}, Deaths: [{GetDeathStats(bestGames)}]");

        if (veryBest.Rating > m_savedRating && veryBest.HighScore > 100)
        {
            m_savedRating = veryBest.Rating * 1.05;
            System.Console.WriteLine("Saved.");
            Settings.Instance.SnakeBrain = veryBest.Brain.Save();
        }

        // Build the games for the next generation.
        m_games.Clear();
        
        // Best snakes get a free pass.
        m_games.AddRange(bestGames);

        // Lucky losers get to survive too.
        m_games.AddRange(luckyLosers);
            
        // Spawn some randoms.
        m_games.AddRange(Enumerable.Range(0, (int)(gameCount * 0.2)).Select(_ => new Game(ArenaWidth, ArenaHeight)));
            
        // Best games get to be parents.
        while (m_games.Count < PopulationSize)
        {
            var mum = bestGames[m_rand.Next(bestGames.Length)];
            var dad = bestGames[m_rand.Next(bestGames.Length)];
            m_games.Add(mum.MergeWith(dad));
        }
        
        // ...and go again...
        m_games = m_games.Select(o => o.Resurrect()).ToList();
    }

    private static string GetDeathStats(Game[] games)
    {
        var reasons = Enum.GetValues<Snake.DeathType>().ToDictionary(o => o, _ => 0);
        foreach (var game in games)
        {
            foreach (var reason in game.DeathReasons)
                reasons[reason.Key] += reason.Value;
        }

        var s = string.Empty;
        foreach (var reason in reasons.Where(o => o.Key != Snake.DeathType.None))
            s += $"{reason.Key}:{reason.Value}, ";
        return s.TrimEnd(',', ' ');
    }
}