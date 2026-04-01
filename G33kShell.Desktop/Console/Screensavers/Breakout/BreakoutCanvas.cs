// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
// purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.
using System;
using System.Diagnostics;
using DTC.Core;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Screensavers.AI;
using G33kShell.Desktop.Terminal;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers.Breakout;

/// <summary>
/// AI-powered Breakout game.
/// </summary>
[DebuggerDisplay("BreakoutCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class BreakoutCanvas : AiGameCanvasBase
{
    private const char BallChar = '☻';
    private const int PreferredFps = 45;
    private const int TrainingSeedBase = 1729;
    private static readonly char[] BrickChars = ['█', '▓', '▒'];

    private Game m_game;

    public BreakoutCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, PreferredFps)
    {
        Name = "breakout";
    }

    public override void UpdateFrame(ScreenData screen)
    {
        screen.ClearChars();

        if (ActivationName.Contains("_train"))
            TrainAi(screen, brainBytes => Settings.Instance.BreakoutBrain = brainBytes);
        else
            PlayGame(screen);
    }

    private void PlayGame(ScreenData screen)
    {
        if (m_game == null)
        {
            var brain = CreateBrain().Load(Settings.Instance.BreakoutBrain);
            m_game = (Game)CreateGame(brain).ResetGame();
        }

        m_game.Tick();
        DrawGame(screen, m_game);

        if (m_game.IsGameOver)
            m_game.ResetGame();
    }

    private void DrawGame(ScreenData screen, Game game)
    {
        screen.Clear(Foreground, Background);

        var headerBackground = 0.18.Lerp(Background, Foreground);
        screen.Clear(0, 0, screen.Width, 1, Foreground, headerBackground);
        var title = $"Score {game.Score,5}  Lives {new string(BallChar, game.Lives)}  Level {game.Level}";
        screen.PrintAt(Math.Max(0, (screen.Width - title.Length) / 2), 0, title, Foreground);

        screen.PrintAt(0, 1, '┌', Foreground);
        screen.PrintAt(screen.Width - 1, 1, '┐', Foreground);
        for (var x = 1; x < screen.Width - 1; x++)
            screen.PrintAt(x, 1, '─', Foreground);

        for (var y = 2; y < screen.Height - 1; y++)
        {
            screen.PrintAt(0, y, '│', Foreground);
            screen.PrintAt(screen.Width - 1, y, '│', Foreground);
        }

        for (var row = 0; row < game.BrickRows; row++)
        {
            for (var col = 0; col < game.BrickCols; col++)
            {
                if (!game.Bricks[col, row])
                    continue;

                var x = game.BrickOffsetX + col * 5;
                var y = game.BrickOffsetY + row;
                var color = GetBrickColor(game.BrickRows, game.Level, col, row);
                var bg = 0.18.Lerp(Background, color);
                var ch = BrickChars[(row + game.Level) % BrickChars.Length];
                for (var dx = 0; dx < 5; dx++)
                    screen.PrintAt(x + dx, y, new Attr(ch, color, bg));
            }
        }

        var paddle = "─" + new string('▀', Math.Max(1, game.PaddleWidth - 2)) + "─";
        var paddleLeft = ((int)Math.Round(game.PaddleX - game.PaddleWidth / 2.0)).Clamp(1, screen.Width - 1 - game.PaddleWidth);
        screen.PrintAt(paddleLeft, game.PaddleY, paddle, Foreground.WithBrightness(1.1));

        var ballColor = game.BallY < game.PaddleY / 2.0
            ? Foreground.WithBrightness(1.15)
            : 0.75.Lerp(Foreground, Rgb.White);
        screen.PrintAt((int)Math.Round(game.BallX), (int)Math.Round(game.BallY), BallChar, ballColor);

        if (!string.IsNullOrWhiteSpace(game.Message) && game.MessageFrames > 0)
        {
            var x = Math.Max(2, (screen.Width - game.Message.Length) / 2);
            screen.PrintAt(x, screen.Height / 2, game.Message, Foreground.WithBrightness(1.15));
        }
    }

    private Rgb GetBrickColor(int brickRows, int level, int col, int row)
    {
        var rowBlend = ((double)(brickRows - row) / Math.Max(1, brickRows)).Lerp(0.38, 0.92);
        var pulse = ((col + level) % 3) * 0.04;
        return Math.Min(1.0, rowBlend + pulse).Lerp(Background, Foreground);
    }

    protected override AiGameBase CreateGame(AiBrainBase brain) => new Game(ArenaWidth, ArenaHeight, (Brain)brain, useTrainingTimeouts: false);
    protected override AiGameBase CreateTrainingGame(AiBrainBase brain) => new Game(ArenaWidth, ArenaHeight, (Brain)brain, useTrainingTimeouts: true);
    protected override int GetGamesPerBrain() => 8;
    protected override bool UseHarnessStyleEvolution() => true;
    protected override int? GetBreedingRandomSeed() => TrainingSeedBase;
    protected override byte[] GetSavedBrainBytes() => Settings.Instance.BreakoutBrain;
    protected override int GetTrainingSeed(int generation, int brainIndex, int gameIndex) =>
        unchecked(TrainingSeedBase + generation * 10_000 + brainIndex * 101 + gameIndex * 17);
    protected override AiBrainBase CreateBrain() => new Brain();
}
