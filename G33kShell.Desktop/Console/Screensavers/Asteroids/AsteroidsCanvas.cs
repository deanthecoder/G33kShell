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
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using CSharp.Core.Extensions;
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
    public AsteroidsCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 60)
    {
        Name = "asciiroids";
    }

    public override void UpdateFrame(ScreenData screen)
    {
        screen.ClearChars();
        
        if (ActivationName.Contains("_train"))
            TrainAi(screen, brainBytes => Settings.Instance.AsteroidsBrain = brainBytes, () => new Brain());
        else
            PlayGame(screen);
    }

    [UsedImplicitly]
    private void PlayGame(ScreenData screen)
    {
        if (m_games == null)
        {
            m_games = [CreateGame().ResetGame()];
            m_games[0].LoadBrainData(Settings.Instance.AsteroidsBrain);
        }

        m_games[0].Tick();
        DrawGame(screen, m_games[0]);
        
        if (((Game)m_games[0]).IsGameOver)
            m_games[0].ResetGame();
    }

    protected override void DrawGame(ScreenData screen, AiGameBase aiGame)
    {
        screen.Clear(Foreground, Background);
        
        var game = (Game)aiGame;
        
        // Ship.
        var highResScreen = new HighResScreen(screen, HighResScreen.DrawMode.LightenOnly);
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

    protected override AiGameBase CreateGame() =>
        new Game(ArenaWidth, ArenaHeight * 2);
}