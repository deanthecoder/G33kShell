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
using System.Reflection;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Screensavers.AI;
using G33kShell.Desktop.Terminal;
using JetBrains.Annotations;
using WenceyWang.FIGlet;

namespace G33kShell.Desktop.Console.Screensavers.Pong;

/// <summary>
/// AI-powered Pong game.
/// </summary>
[DebuggerDisplay("AsteroidsCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class PongCanvas : AiGameCanvasBase
{
    private readonly FIGletFont m_font;
    private Game m_game;

    public PongCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 60)
    {
        Name = "pong";

        m_font = LoadFont();
    }

    public override void UpdateFrame(ScreenData screen)
    {
        screen.ClearChars();

        if (ActivationName.Contains("_train"))
            TrainAi(screen, brainBytes => Settings.Instance.PongBrain = brainBytes);
        else
            PlayGame(screen);

    }

    [UsedImplicitly]
    private void PlayGame(ScreenData screen)
    {
        if (m_game == null)
        {
            var brain = new Brain().Load(Settings.Instance.PongBrain);
            m_game = (Game)CreateGame(brain).ResetGame();
        }

        DrawGame(screen, m_game);

        m_game.Tick();
        if (m_game.IsGameOver)
            m_game.ResetGame();
    }

    private void DrawGame(ScreenData screen, AiGameBase aiGame)
    {
        var game = (Game)aiGame;
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
    
    private static FIGletFont LoadFont()
    {
        // Enumerate all Avalonia embedded resources.
        var fontFolder = Assembly.GetExecutingAssembly().GetDirectory();
        var fontFile = fontFolder.GetFiles("Assets/Fonts/Figlet/*.flf").Single();

        // Load the font.
        using var fontStream = fontFile.OpenRead();
        return new FIGletFont(fontStream);
    }

    protected override AiGameBase CreateGame(AiBrainBase brain) => new Game(ArenaWidth, ArenaHeight, (Brain)brain);
    protected override AiBrainBase CreateBrain() => new Brain();
}