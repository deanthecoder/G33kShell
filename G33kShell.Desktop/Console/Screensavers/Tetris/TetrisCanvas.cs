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

namespace G33kShell.Desktop.Console.Screensavers.Tetris;

/// <summary>
/// AI-powered Tetris screensaver.
/// </summary>
[DebuggerDisplay("TetrisCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class TetrisCanvas : AiGameCanvasBase
{
    private const int PreferredFps = 30;
    private const int TrainingSeedBase = 1984;
    private const int ActiveStartY = -4;
    private const double SettledFadeFrames = 95.0;
    private CandidateMove? m_activeMove;
    private double m_activeX;
    private double m_activeY;
    private int m_activeRotation;
    private int m_activeFrames;
    private int m_gameOverFrames;
    private Game m_game;

    public TetrisCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
    {
        Name = "tetris";
    }

    public override void UpdateFrame(ScreenData screen)
    {
        screen.ClearChars();

        if (ActivationName.Contains("_train", StringComparison.OrdinalIgnoreCase))
            TrainAi(screen, brainBytes => Settings.Instance.TetrisBrain = brainBytes);
        else
            PlayGame(screen);
    }

    private void PlayGame(ScreenData screen)
    {
        if (m_game == null)
        {
            var brain = CreateBrain().Load(Settings.Instance.TetrisBrain);
            m_game = (Game)CreateGame(brain).ResetGame();
        }

        if (m_gameOverFrames > 0)
        {
            m_gameOverFrames--;
            DrawGame(screen, m_game, showGameOver: true);
            if (m_gameOverFrames == 0)
            {
                m_activeMove = null;
                m_game.ResetGame();
            }
            return;
        }

        AdvanceVisibleGame();
        m_game.AgeSettledCells();

        DrawGame(screen, m_game);

        if (m_game.IsGameOver)
        {
            m_activeMove = null;
            m_gameOverFrames = PreferredFps * 5;
        }
    }

    private void AdvanceVisibleGame()
    {
        if (m_activeMove == null)
        {
            m_activeMove = m_game.ChooseBestMove();
            m_activeX = 3.0;
            m_activeY = ActiveStartY;
            m_activeRotation = 0;
            m_activeFrames = 0;
            if (m_activeMove == null)
                m_game.Tick();
            return;
        }

        var move = m_activeMove.Value;
        var fallSpeed = Math.Clamp(0.42 + m_game.Level * 0.045, 0.42, 1.2) * 1.15;
        m_activeFrames++;
        if (m_activeFrames <= 1)
        {
            m_activeY = Math.Min(move.Y, m_activeY + fallSpeed);
            return;
        }

        var horizontalStep = 0.55;
        if (Math.Abs(move.X - m_activeX) <= horizontalStep)
            m_activeX = move.X;
        else
            m_activeX += Math.Sign(move.X - m_activeX) * horizontalStep;

        if (m_activeRotation != move.Rotation)
            m_activeRotation = (m_activeRotation + 1) % 4;

        m_activeY = Math.Min(move.Y, m_activeY + fallSpeed);
        if (m_activeY < move.Y || Math.Abs(m_activeX - move.X) > 0.001 || m_activeRotation != move.Rotation)
            return;

        m_game.LockMove(move);
        m_activeMove = null;
    }

    private void DrawGame(ScreenData screen, Game game, bool showGameOver = false)
    {
        screen.Clear(Foreground, Background);

        const int hudWidth = 20;
        var blockSize = Math.Max(1, Math.Min((screen.Width - hudWidth - 4) / Game.BoardWidth, (screen.Height * 2 - 4) / Game.BoardHeight));
        var boardPixelWidth = Game.BoardWidth * blockSize;
        var boardPixelHeight = Game.BoardHeight * blockSize;
        var left = Math.Max(1, (screen.Width - boardPixelWidth - hudWidth) / 2);
        var top = Math.Max(2, (screen.Height * 2 - boardPixelHeight) / 2);

        var highResScreen = new HighResScreen(screen);
        DrawBoard(highResScreen, game, left, top, blockSize);

        var hudX = Math.Min(Math.Max(0, screen.Width - hudWidth), left + boardPixelWidth + 3);
        screen.PrintAt(hudX, 1, "TETRIS", Foreground.WithBrightness(1.15));
        screen.PrintAt(hudX, 3, $"Score {game.Score:N0}");
        screen.PrintAt(hudX, 4, $"High  {game.HighScore:N0}");
        screen.PrintAt(hudX, 5, $"Lines {game.Lines:N0}");
        screen.PrintAt(hudX, 6, $"Level {game.Level:N0}");
        screen.PrintAt(hudX, 8, "Next");
        DrawNextPiece(highResScreen, game.NextPiece, hudX, 18, Math.Max(1, Math.Min(3, blockSize)));

        if (showGameOver)
        {
            var message = "GAME OVER";
            var x = left + Math.Max(0, (boardPixelWidth - message.Length) / 2);
            var y = Math.Max(1, top / 2);
            screen.PrintAt(x, y, message, Foreground.WithBrightness(1.25));
        }
    }

    private void DrawBoard(HighResScreen highResScreen, Game game, int left, int top, int blockSize)
    {
        var frameColor = 0.45.Lerp(Background, Foreground);
        highResScreen.DrawFilledBox(left - 1, top - 1, Game.BoardWidth * blockSize + 2, 1, frameColor);
        highResScreen.DrawFilledBox(left - 1, top + Game.BoardHeight * blockSize, Game.BoardWidth * blockSize + 2, 1, frameColor);
        highResScreen.DrawFilledBox(left - 1, top, 1, Game.BoardHeight * blockSize, frameColor);
        highResScreen.DrawFilledBox(left + Game.BoardWidth * blockSize, top, 1, Game.BoardHeight * blockSize, frameColor);

        for (var y = 0; y < Game.BoardHeight; y++)
        {
            for (var x = 0; x < Game.BoardWidth; x++)
            {
                var value = game.Board[x, y];
                if (value == 0)
                    continue;
                var piece = (Tetromino)(value - 1);
                DrawBlock(highResScreen, left + x * blockSize, top + y * blockSize, blockSize, GetSettledColor(game, x, y, piece), x, y);
            }
        }

        if (!m_activeMove.HasValue)
            return;

        var activeMove = m_activeMove.Value;
        foreach (var cell in Game.GetCells(activeMove.Piece, m_activeRotation, (int)Math.Round(m_activeX), (int)Math.Round(m_activeY)))
        {
            if (cell.Y < 0 || cell.Y >= Game.BoardHeight)
                continue;
            DrawBlock(highResScreen, left + cell.X * blockSize, top + cell.Y * blockSize, blockSize, GetActivePieceColor(activeMove.Piece), cell.X, cell.Y);
        }
    }

    private void DrawNextPiece(HighResScreen highResScreen, Tetromino piece, int charX, int pixelY, int blockSize)
    {
        foreach (var cell in Game.GetCells(piece, 0, 0, 0))
            DrawBlock(highResScreen, charX + cell.X * blockSize, pixelY + cell.Y * blockSize, blockSize, GetActivePieceColor(piece), cell.X, cell.Y);
    }

    private static void DrawBlock(HighResScreen screen, int x, int y, int size, Rgb color, int boardX, int boardY)
    {
        for (var yy = 0; yy < size; yy++)
        {
            for (var xx = 0; xx < size; xx++)
            {
                var edge = xx == 0 || yy == 0 ? 1.14 : xx == size - 1 || yy == size - 1 ? 0.86 : 1.0;
                var texture = ((boardX * 17 + boardY * 31 + xx * 7 + yy * 11) % 9 - 4) * 0.018;
                screen.Plot(x + xx, y + yy, color.WithBrightness(edge + texture));
            }
        }

        if (size <= 1)
            return;
        screen.DrawFilledBox(x, y, size, 1, color.WithBrightness(1.18));
        screen.DrawFilledBox(x, y, 1, size, color.WithBrightness(1.1));
    }

    private Rgb GetPieceColor(Tetromino piece)
    {
        var shades = new[] { 0.86, 0.72, 0.92, 0.58, 0.78, 0.66, 0.98 };
        return shades[(int)piece].Lerp(Background, Foreground);
    }

    private Rgb GetActivePieceColor(Tetromino piece) => GetPieceColor(piece).WithBrightness(1.24);

    private Rgb GetSettledColor(Game game, int x, int y, Tetromino piece)
    {
        var fade = (game.CellAges[x, y] / SettledFadeFrames).Clamp(0.0, 1.0);
        fade = Math.Round(fade * 14.0) / 14.0;
        var uniform = 0.7.Lerp(Background, Foreground);
        var fresh = GetPieceColor(piece);
        return fade.Lerp(fresh, uniform);
    }

    protected override AiGameBase CreateGame(AiBrainBase brain) => new Game(ArenaWidth, ArenaHeight, (Brain)brain);
    protected override AiGameBase CreateTrainingGame(AiBrainBase brain) => new Game(ArenaWidth, ArenaHeight, (Brain)brain, useTrainingTimeouts: true);
    protected override int GetGamesPerBrain() => 6;
    protected override int GetValidationGamesPerBrain() => 6;
    protected override int GetInitialPopulationSize() => 180;
    protected override int GetMinPopulationSize() => 100;
    protected override double GetMutationRate() => 0.06;
    protected override bool UseHarnessStyleEvolution() => true;
    protected override int? GetBreedingRandomSeed() => TrainingSeedBase;
    protected override byte[] GetSavedBrainBytes() => Settings.Instance.TetrisBrain;
    protected override int GetTrainingSeed(int generation, int brainIndex, int gameIndex) =>
        unchecked(TrainingSeedBase + generation * 10_000 + brainIndex * 101 + gameIndex * 17);
    protected override AiBrainBase CreateBrain() => new Brain();
}
