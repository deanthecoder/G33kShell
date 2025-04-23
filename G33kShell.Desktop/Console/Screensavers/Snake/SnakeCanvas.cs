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
using G33kShell.Desktop.Console.Screensavers.AI;
using G33kShell.Desktop.Terminal;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

/// <summary>
/// AI-powered snake game.
/// </summary>
[DebuggerDisplay("SnakeCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class SnakeCanvas : AiGameCanvasBase
{
    private Game m_game;

    public SnakeCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 60)
    {
        Name = "snake";
    }

    public override void UpdateFrame(ScreenData screen)
    {
        screen.ClearChars();

        if (ActivationName.Contains("_train"))
            TrainAi(screen, brainBytes => Settings.Instance.SnakeBrain = brainBytes);
        else
            PlayGame(screen);
    }

    [UsedImplicitly]
    private void PlayGame(ScreenData screen)
    {
        if (m_game == null)
        {
            m_game = (Game)new Game(ArenaWidth, ArenaHeight).ResetGame();
            m_game.LoadBrainData(Settings.Instance.SnakeBrain);
        }

        DrawGame(screen, m_game);

        m_game.Tick();
    }

    private static void DrawGame(ScreenData screen, AiGameBase aiGame)
    {
        var game = (Game)aiGame;
        
        screen.PrintAt(game.FoodPosition.X, game.FoodPosition.Y, '\u2665');
        foreach (var segment in game.Snake.Segments)
            screen.PrintAt(segment.X, segment.Y, '■');
        screen.PrintAt(game.Snake.HeadPosition.X, game.Snake.HeadPosition.Y, '☻');
        screen.PrintAt(0, 0, $"Score: {game.Score}, High Score: {game.HighScore}");
    }

    protected override AiGameBase CreateGame() =>
        new Game(ArenaWidth, ArenaHeight);
}