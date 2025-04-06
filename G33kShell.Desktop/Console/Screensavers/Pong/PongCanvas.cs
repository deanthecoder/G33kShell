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
using System.Reflection;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using G33kShell.Desktop.Terminal;
using JetBrains.Annotations;
using WenceyWang.FIGlet;

namespace G33kShell.Desktop.Console.Screensavers.Pong;

/// <summary>
/// AI-powered snake game.
/// </summary>
[DebuggerDisplay("PongCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class PongCanvas : ScreensaverBase
{
    private const int PopulationSize = 200;
    private readonly Random m_rand = new Random();
    private int m_trainingGenerationCompleted;
    private int ArenaWidth { get; }
    private int ArenaHeight { get; }
    private List<Game> m_games;
    private double m_savedRating;
    private readonly FIGletFont m_font;

    public PongCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 60)
    {
        Name = "pong";

        ArenaWidth = screenWidth;
        ArenaHeight = screenHeight;

        m_font = LoadFont();
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
            m_games[0].LoadBrainData(Settings.Instance.PongBrain);
        }

        DrawGame(screen, m_games[0]);

        m_games[0].Tick();
        if (m_games[0].IsGameOver)
            m_games[0].Resurrect();
    }

    private void DrawGame(ScreenData screen, Game game)
    {
        var dimRgb = 0.2.Lerp(Background, Foreground);

        // Half-way separator.
        for (var y = 0; y < screen.Height; y++)
            screen.PrintAt(screen.Width / 2, y, "▄", dimRgb);
        
        // Scores.
        for (var scoreIndex = 0; scoreIndex < 2; scoreIndex++)
        {
            var ox = screen.Width / 2 + (scoreIndex == 0 ? -18 : 8);
            var oy = 2;
            for (var y = 0; y < m_font.Height; y++)
            {
                var score = game.Scores[scoreIndex].ToString();
                foreach (var ch in score)
                {
                    var text = m_font.GetCharacter(ch, y);
                    for (var x = 0; x < text.Length; x++)
                        screen.PrintAt(ox + x, oy + y, text[x], dimRgb);
                }
            }
        }

        // Bats.
        for (var i = 0; i < Game.BatHeight; i++)
        {
            foreach (var batPosition in game.BatPositions)
                screen.PrintAt((int)batPosition.X, (int)(batPosition.Y - Game.BatHeight / 2.0f + i), '█');
        }

        // Ball.
        screen.PrintAt((int)game.BallPosition.X, (int)game.BallPosition.Y, game.BallPosition.Y - (int)game.BallPosition.Y < 0.5f ? '▀' : '▄', Foreground);
    }

    [UsedImplicitly]
    private void TrainAi(ScreenData screen)
    {
        m_games ??= Enumerable.Range(0, PopulationSize).Select(_ => new Game(ArenaWidth, ArenaHeight)).ToList();

        m_games.AsParallel().ForAll(o =>
        {
            while (!o.IsGameOver)
                o.Tick();
        });

        DrawGame(screen, m_games[0]);
        
        var isAllGamesEnded = m_games.All(o => o.IsGameOver);
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
        System.Console.WriteLine($"Gen {m_trainingGenerationCompleted}, Rating: {veryBest.Rating:F2}, Range: {bestGames.Min(o => o.Rating):F1} -> {bestGames.Max(o => o.Rating):F1}, Rallies: {veryBest.Rallies}, Scores: [{veryBest.Scores[0]}, {veryBest.Scores[1]}]");

        if (veryBest.Rating > m_savedRating && veryBest.Rating > 100)
        {
            m_savedRating = veryBest.Rating * 1.05;
            System.Console.WriteLine("Saved.");
            Settings.Instance.PongBrain = veryBest.Brain.Save();
        }

        // Build the games for the next generation.
        m_games.Clear();
        
        // Best brains get a free pass.
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

    private static FIGletFont LoadFont()
    {
        // Enumerate all Avalonia embedded resources.
        var fontFolder = Assembly.GetExecutingAssembly().GetDirectory();
        var fontFile = fontFolder.GetFiles("Assets/Fonts/Figlet/*.flf").Single();

        // Load the font.
        using var fontStream = fontFile.OpenRead();
        return new FIGletFont(fontStream);
    }
}