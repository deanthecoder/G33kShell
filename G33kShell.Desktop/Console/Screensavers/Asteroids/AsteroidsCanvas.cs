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
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Screensavers.AI;
using G33kShell.Desktop.Terminal;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers.Asteroids;

/// <summary>
/// AI-powered Asteroids game.
/// </summary>
[DebuggerDisplay("AsteroidsCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class AsteroidsCanvas : AiGameCanvasBase
{
    private const int TrainingSeedBase = 7331;
    private Game m_game;

    public AsteroidsCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 60)
    {
        Name = "asciiroids";
    }

    public override void UpdateFrame(ScreenData screen)
    {
        screen.ClearChars();
        
        if (ActivationName.Contains("_train"))
            TrainAi(screen, brainBytes => Settings.Instance.AsteroidsBrain = brainBytes);
        else
            PlayGame(screen);
    }

    [UsedImplicitly]
    private void PlayGame(ScreenData screen)
    {
        if (m_game == null)
        {
            var brain = new Brain().Load(Settings.Instance.AsteroidsBrain);
            m_game = (Game)CreateGame(brain);
            m_game.ResetGame();
        }

        m_game.Tick();
        DrawGame(screen, m_game);
        
        if (m_game.IsGameOver)
            m_game.ResetGame();
    }

    private void DrawGame(ScreenData screen, AiGameBase aiGame)
    {
        screen.Clear(Foreground, Background);
        
        var game = (Game)aiGame;
        
        // Ship.
        var highResScreen = new HighResScreen(screen, HighResScreen.DrawMode.LightenOnly);

        // Exhaust.
        foreach (var particle in game.ExhaustParticles)
            highResScreen.Plot((int)particle.Position.X, (int)particle.Position.Y, particle.Brightness.Lerp(Background, Foreground));

        byte[] ship =
        [
            0b11000000,
            0b01100000,
            0b00111100,
            0b00011111,
            0b00111100,
            0b01100000,
            0b11000000
        ];
        var rotation = Matrix3x2.CreateRotation(game.Ship.Theta);
        for (var y = 0.0f; y < 7.0f; y += 0.25f)
        {
            for (var x = 0.0f; x < 8.0f; x += 0.25f)
            {
                var ix = (int)x;
                var iy = (int)y;
                if ((ship[iy] & (0b10000000 >> ix)) == 0)
                    continue; // No pixel.

                var xy = new Vector2(x - 5, y - 3);
                var rotatedXy = Vector2.Transform(xy, rotation);
                highResScreen.Plot((int)(game.Ship.Position.X + rotatedXy.X), (int)(game.Ship.Position.Y + rotatedXy.Y), Foreground);
            }
        }

        // Asteroids.
        foreach (var asteroid in game.Asteroids.OrderBy(o => o.Shade))
            highResScreen.DrawSphere((int)asteroid.Position.X, (int)asteroid.Position.Y, asteroid.Radius, asteroid.Shade.Lerp(Background, Foreground), Background);
        
        // Bullets.
        foreach (var bullet in game.Bullets)
            highResScreen.Plot((int)bullet.Position.X, (int)bullet.Position.Y, Foreground);

        // Score + Shield.
        screen.Clear(0, 0, screen.Width, 1, Foreground, Background);
        screen.PrintAt(2, 0, $"Score: {game.Score.ToString().PadLeft(5, '0')}   Shield: {game.Ship.Shield.ToProgressBar(73)}");
    }

    protected override AiGameBase CreateGame(AiBrainBase brain) => new Game(ArenaWidth, ArenaHeight * 2, (Brain)brain, enableVisualEffects: true);
    protected override AiGameBase CreateTrainingGame(AiBrainBase brain, int generation, bool isValidation, int candidateIndex, int gameIndex) =>
        new Game(
            ArenaWidth,
            ArenaHeight * 2,
            (Brain)brain,
            isValidation ? Game.TrainingProfile.Default : GetCurriculumProfile(generation),
            enableVisualEffects: false);
    protected override int GetGamesPerBrain() => 6;
    protected override string GetTrainingStatusText(int generation)
    {
        var stage = GetCurriculumProfile(Math.Max(1, generation));
        var stageIndex =
            stage.StageLabel == "Bootcamp" ? 1 :
            stage.StageLabel == "Scout" ? 2 :
            stage.StageLabel == "Dogfight" ? 3 :
            stage.StageLabel == "Chaos" ? 4 :
            5;
        return $"Stage: {stage.StageLabel} ({stageIndex}/5)";
    }
    protected override bool UseHarnessStyleEvolution() => true;
    protected override int? GetBreedingRandomSeed() => TrainingSeedBase;
    protected override byte[] GetSavedBrainBytes() => Settings.Instance.AsteroidsBrain;
    protected override int GetTrainingSeed(int generation, int brainIndex, int gameIndex) =>
        unchecked(TrainingSeedBase + generation * 10_000 + brainIndex * 101 + gameIndex * 17);
    protected override int GetValidationSeed(int generation, int candidateIndex, int gameIndex) =>
        unchecked(TrainingSeedBase + 1_000_000 + candidateIndex * 1009 + gameIndex * 37);
    protected override AiBrainBase CreateBrain() => new Brain();

    private static Game.TrainingProfile GetCurriculumProfile(int generation)
    {
        var ramp = Math.Clamp((generation - 1) / 240.0, 0.0, 1.0);
        var asteroidMetric = (int)Math.Round(6 + ramp * 6);
        var speed = (float)(0.07 + ramp * 0.03);
        var aimedSpawnChance = Math.Clamp(ramp - 0.35, 0.0, 0.45);
        var (stageLabel, collisionDamageMultiplier) =
            ramp < 0.25 ? ("Bootcamp", 0.0) :
            ramp < 0.50 ? ("Scout", 0.25) :
            ramp < 0.75 ? ("Dogfight", 0.55) :
            ramp < 1.00 ? ("Chaos", 0.85) :
            ("Full", 1.0);
        return new Game.TrainingProfile(asteroidMetric, speed, aimedSpawnChance, collisionDamageMultiplier, stageLabel);
    }
}
