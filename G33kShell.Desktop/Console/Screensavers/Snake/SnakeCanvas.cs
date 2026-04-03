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
/// <remarks>
/// Interactive play uses the current terminal size, while training uses a fixed arena and seed
/// schedule so runs are more repeatable and comparable over time.
/// </remarks>
[DebuggerDisplay("SnakeCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class SnakeCanvas : AiGameCanvasBase
{
    private const int TrainingArenaWidth = 48;
    private const int TrainingArenaHeight = 28;
    private const int TrainingSeedBase = 1337;

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
            var brain = CreateBrain().Load(Settings.Instance.SnakeBrain);
            m_game = (Game)CreateGame(brain).ResetGame();
        }

        DrawGame(screen, m_game);

        m_game.Tick();

        if (m_game.IsGameOver)
            m_game.ResetGame();
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

    protected override AiGameBase CreateGame(AiBrainBase brain) => new Game(ArenaWidth, ArenaHeight, brain, limitLives: false);
    protected override AiGameBase CreateTrainingGame(AiBrainBase brain) => new Game(TrainingArenaWidth, TrainingArenaHeight, brain);
    protected override bool UseHarnessStyleEvolution() => true;
    protected override int? GetBreedingRandomSeed() => TrainingSeedBase;
    protected override byte[] GetSavedBrainBytes() => Settings.Instance.SnakeBrain;
    protected override int GetTrainingSeed(int generation, int brainIndex, int gameIndex) =>
        unchecked(TrainingSeedBase + generation * 10_000 + brainIndex * 101 + gameIndex * 17);
    protected override AiBrainBase CreateBrain() => new Brain();
}
