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

namespace G33kShell.Desktop.Console.Screensavers.RoadFighter;

/// <summary>
/// Endless Road Fighter-style AI driving screensaver.
/// </summary>
[DebuggerDisplay("RoadFighterCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class RoadFighterCanvas : AiGameCanvasBase
{
    private const int PreferredFps = 15;
    private const int TrainingSeedBase = 4242;
    private const int ValidationSeedBase = 1_004_242;
    private static readonly string[] CarSprite =
    [
        " ▄▄ ",
        "{██}",
        "{██}"
    ];
    private Game m_game;

    public RoadFighterCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, PreferredFps)
    {
        Name = "roadfighter";
    }

    public override void UpdateFrame(ScreenData screen)
    {
        screen.ClearChars();

        if (ActivationName.Contains("_train"))
            TrainAi(screen, brainBytes => Settings.Instance.RoadFighterBrain = brainBytes);
        else
            PlayGame(screen);
    }

    private void PlayGame(ScreenData screen)
    {
        if (m_game == null)
        {
            var brain = CreateBrain().Load(Settings.Instance.RoadFighterBrain);
            m_game = (Game)CreateGame(brain).ResetGame();
        }

        m_game.Tick();
        DrawGame(screen, m_game);

        if (m_game.IsGameOver)
            m_game.ResetGame();
    }

    private void DrawGame(ScreenData screen, Game game)
    {
        var grass = 0.12.Lerp(Background, Foreground);
        var road = 0.28.Lerp(Background, Foreground);
        var lane = 0.68.Lerp(Background, Foreground);
        var shoulder = 0.48.Lerp(Background, Foreground);
        var building = 0.36.Lerp(Background, Foreground);
        var window = 0.82.Lerp(Background, Foreground);
        var water = 0.52.Lerp(Background, Foreground.WithBrightness(0.9));
        var treeTrunk = 0.58.Lerp(Background, Foreground);
        var treeLeaves = 0.74.Lerp(Background, Foreground);
        var enemyBody = 0.86.Lerp(Background, Foreground);
        var enemyTrim = 0.66.Lerp(Background, Foreground);

        screen.Clear(Foreground, grass);
        screen.Clear(0, 0, screen.Width, 1, Foreground, 0.18.Lerp(Background, Foreground));
        screen.PrintAt(2, 0, $"Road Fighter   Score {game.Score,5}   Distance {game.Distance,5}   Overtakes {game.Overtakes,3}");

        for (var y = 1; y < screen.Height; y++)
        {
            var (left, right) = game.GetRoadBounds(y);
            left = left.Clamp(0, screen.Width - 1);
            right = right.Clamp(0, screen.Width - 1);
            if (right <= left)
                continue;

            DrawRoadsideScenery(screen, game, y, left, right, building, window, water, treeTrunk, treeLeaves);
            screen.Clear(left, y, right - left + 1, 1, Foreground, road);
            screen.PrintAt(left, y, '▌', shoulder);
            screen.PrintAt(right, y, '▐', shoulder);

            var laneX = (int)Math.Round(game.GetRoadCenter(y));
            var laneWorldY = game.Distance + (screen.Height - y);
            if (laneWorldY % 4 < 2)
                screen.PrintAt(laneX.Clamp(left + 1, right - 1), y, '╎', lane);
        }

        foreach (var car in game.TrafficCars)
        {
            var x = (int)Math.Round(car.X) - CarSprite[0].Length / 2;
            var y = (int)Math.Round(car.Y) - 1;
            if (y < 1 || y >= screen.Height - CarSprite.Length)
                continue;

            var drawX = x.Clamp(1, screen.Width - 1 - CarSprite[0].Length);
            DrawCar(screen, drawX, y, enemyBody, enemyTrim);
        }

        var playerX = (int)Math.Round(game.PlayerX) - CarSprite[0].Length / 2;
        var playerRgb = game.Crashed ? 0.78.Lerp(Background, new Rgb(255, 96, 96)) : Foreground.WithBrightness(1.15);
        var playerDrawX = playerX.Clamp(1, screen.Width - 1 - CarSprite[0].Length);
        DrawCar(screen, playerDrawX, game.PlayerY - 2, playerRgb, shoulder.WithBrightness(1.05));
    }

    private static void DrawRoadsideScenery(ScreenData screen, Game game, int y, int roadLeft, int roadRight, Rgb building, Rgb window, Rgb water, Rgb treeTrunk, Rgb treeLeaves)
    {
        var worldY = game.Distance + (screen.Height - y);
        var segment = worldY / 10;

        DrawSceneryForSide(screen, y, roadLeft, roadRight, worldY, segment, isLeft: true, building, window, water, treeTrunk, treeLeaves);
        DrawSceneryForSide(screen, y, roadLeft, roadRight, worldY, segment, isLeft: false, building, window, water, treeTrunk, treeLeaves);
    }

    private static void DrawSceneryForSide(ScreenData screen, int y, int roadLeft, int roadRight, int worldY, int segment, bool isLeft, Rgb building, Rgb window, Rgb water, Rgb treeTrunk, Rgb treeLeaves)
    {
        var seed = Hash(segment * 2 + (isLeft ? 0 : 1));
        if (seed % 10 > 2)
            return;

        var sceneryType = seed % 4;
        switch (sceneryType)
        {
            case 0:
                DrawBuilding(screen, y, roadLeft, roadRight, worldY, seed, isLeft, building, window);
                break;
            case 1:
                DrawLake(screen, y, roadLeft, roadRight, worldY, seed, isLeft, water);
                break;
            case 2:
            case 3:
                DrawPalmTree(screen, y, roadLeft, roadRight, worldY, seed, isLeft, treeTrunk, treeLeaves);
                break;
        }
    }

    private static void DrawBuilding(ScreenData screen, int y, int roadLeft, int roadRight, int worldY, int seed, bool isLeft, Rgb building, Rgb window)
    {
        var width = 5 + seed % 5;
        var localY = ((worldY % 8) + 8) % 8;
        var facadeChar = localY == 7 ? '▀' : '█';
        var x = GetSceneryX(screen.Width, roadLeft, roadRight, width, seed, isLeft);
        var limit = isLeft ? roadLeft : screen.Width;
        var drawableWidth = Math.Min(width, Math.Max(0, limit - x));
        if (drawableWidth <= 0)
            return;

        screen.PrintAt(x, y, new string(facadeChar, drawableWidth), building);
        if (localY is > 0 and < 7 && drawableWidth >= 6)
        {
            var windowPattern = drawableWidth >= 8 ? "∙ ∙ ∙" : "∙ ∙";
            screen.PrintAt(x + 1, y, windowPattern[..Math.Min(windowPattern.Length, drawableWidth - 2)], window);
        }
    }

    private static void DrawLake(ScreenData screen, int y, int roadLeft, int roadRight, int worldY, int seed, bool isLeft, Rgb water)
    {
        var width = 5 + seed % 6;
        var localY = ((worldY % 6) + 6) % 6;
        var x = GetSceneryX(screen.Width, roadLeft, roadRight, width, seed, isLeft);
        var fill = localY switch
        {
            0 or 5 => " ▄▄▄ ",
            1 or 4 => "▄████▄",
            _ => "▀████▀"
        };
        var text = FitScenery(fill, width);
        if (text.Length > 0)
            screen.PrintAt(x, y, text, water);
    }

    private static void DrawPalmTree(ScreenData screen, int y, int roadLeft, int roadRight, int worldY, int seed, bool isLeft, Rgb trunk, Rgb leaves)
    {
        var localY = ((worldY % 8) + 8) % 8;
        var x = GetSceneryX(screen.Width, roadLeft, roadRight, 3, seed, isLeft) + 1;
        switch (localY)
        {
            case 0:
                screen.PrintAt(x - 1, y, "╲│╱", leaves);
                break;
            case 1:
                screen.PrintAt(x - 1, y, "─┼─", leaves);
                break;
            case 2:
            case 3:
            case 4:
            case 5:
            case 6:
                screen.PrintAt(x, y, "│", trunk);
                break;
            default:
                screen.PrintAt(x, y, "╿", trunk);
                break;
        }
    }

    private static void DrawCar(ScreenData screen, int x, int y, Rgb bodyColor, Rgb wheelColor)
    {
        screen.PrintAt(x, y, CarSprite[0], bodyColor);
        screen.PrintAt(x, y + 1, CarSprite[1], wheelColor);
        screen.PrintAt(x, y + 2, CarSprite[2], wheelColor);
    }

    private static int Hash(int value)
    {
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        return Math.Abs(value);
    }

    private static int GetSceneryX(int screenWidth, int roadLeft, int roadRight, int width, int seed, bool isLeft)
    {
        if (isLeft)
        {
            var maxX = Math.Max(0, roadLeft - width - 2);
            return maxX <= 0 ? 0 : seed % (maxX + 1);
        }

        var minX = Math.Min(screenWidth - width, roadRight + 2);
        var maxExtra = Math.Max(0, screenWidth - width - minX);
        return minX + (maxExtra == 0 ? 0 : seed % (maxExtra + 1));
    }

    private static string FitScenery(string text, int width)
    {
        if (width <= 0)
            return string.Empty;
        if (text.Length == width)
            return text;
        if (text.Length > width)
            return text[..width];

        return text.PadRight(width);
    }

    protected override AiGameBase CreateGame(AiBrainBase brain) => new Game(ArenaWidth, ArenaHeight, (Brain)brain, useTrainingTimeouts: false);
    protected override AiGameBase CreateTrainingGame(AiBrainBase brain, int generation, bool isValidation, int candidateIndex, int gameIndex) =>
        new Game(ArenaWidth, ArenaHeight, (Brain)brain, useTrainingTimeouts: true, trainingProfile: isValidation ? Game.TrainingProfile.Default : Game.GetCurriculumProfile(generation));
    protected override int GetGamesPerBrain() => 8;
    protected override int GetValidationGamesPerBrain() => 12;
    protected override string GetTrainingStatusText(int generation)
    {
        var stage = Game.GetCurriculumProfile(Math.Max(1, generation));
        var stageIndex =
            stage.StageLabel == "Straight" ? 1 :
            stage.StageLabel == "Dodge" ? 2 :
            stage.StageLabel == "Curve" ? 3 :
            4;
        return $"Stage: {stage.StageLabel} ({stageIndex}/4)";
    }
    protected override bool UseHarnessStyleEvolution() => true;
    protected override int? GetBreedingRandomSeed() => TrainingSeedBase;
    protected override byte[] GetSavedBrainBytes() => Settings.Instance.RoadFighterBrain;
    protected override int GetTrainingSeed(int generation, int brainIndex, int gameIndex) =>
        unchecked(TrainingSeedBase + generation * 10_000 + brainIndex * 101 + gameIndex * 17);
    protected override int GetValidationSeed(int generation, int candidateIndex, int gameIndex)
    {
        var validationSet = ((generation - 1) / 10) % 3;
        var setOffset = validationSet * 100_000;
        return unchecked(ValidationSeedBase + setOffset + candidateIndex * 1009 + gameIndex * 37);
    }
    protected override AiBrainBase CreateBrain() => new Brain();
}
