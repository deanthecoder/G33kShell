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
using CSharp.Core;
using CSharp.Core.Extensions;
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
    private readonly Random m_rand = new Random();
    private readonly QTable m_qTable;
    private Dictionary<DeathReason, int> m_deathReasons;
    private Snake m_snake;
    private int m_foodX;
    private int m_foodY;
    private int m_gamesPlayed = 1;
    private int m_highScore;
    private int m_score;
    [UsedImplicitly] private LearningConfig m_learningConfig;
    private int m_stepsSinceFood;
    private bool m_brainInitialized;

    private const int TrainingWidth = 32;
    private const int TrainingHeight = 24;
    private int ArenaWidth { get; }
    private int ArenaHeight { get; }

    private enum DeathReason
    {
        CollisionWall,
        CollisionSelf,
        Unknown,
        [UsedImplicitly] StuckInLoop
    }

    public SnakeCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 45)
    {
        Name = "snake";

        ResetDeathReasons();

        ArenaWidth = screenWidth;
        ArenaHeight = screenHeight;
        m_qTable = new QTable();
    }

    public override void StopScreensaver()
    {
        base.StopScreensaver();
        
        if (m_brainInitialized)
            Settings.Instance.SnakeBrain = m_qTable.Save();
    }

    private void ResetDeathReasons()
    {
        m_deathReasons =
            Enum.GetValues(typeof(DeathReason))
                .Cast<DeathReason>()
                .ToDictionary(k => k, _ => 0);
    }

    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);

        m_learningConfig = new LearningConfig();
        ResetGame(ArenaWidth, ArenaHeight, resetBrain: true);
        if (Settings.Instance.SnakeBrain == null || Settings.Instance.SnakeBrain.Length == 0)
        {
            PreLearn(TrainingWidth, TrainingHeight);
            Settings.Instance.SnakeBrain = m_qTable.Save();
        }
        else
        {
            m_qTable.Load(Settings.Instance.SnakeBrain);
        }

        m_brainInitialized = true;
    }

    private void PreLearn(int arenaWidth, int arenaHeight)
    {
        // Prepare to learn.
        ResetGame(arenaWidth, arenaHeight, resetBrain: true);

        // Train until stable exploration.
        var scorePerGame = new List<int>();
        var gamesPlayed = 0;
        var score = 0;
        while (m_gamesPlayed < LearningConfig.LearningGameCount)
        {
            Learn(arenaWidth, arenaHeight, true);
            if (m_score > score)
                score = m_score;

            if (m_gamesPlayed > gamesPlayed)
            {
                gamesPlayed = m_gamesPlayed;
                scorePerGame.Add(score);
                score = 0;
            }
        }
        
        // Remember the brains.
        Settings.Instance.SnakeBrain = m_qTable.Save();

        // Dump scores to the console.
        System.Console.WriteLine($"TrainingScores:{scorePerGame.ToCsv()}");

#if DEBUG
        m_highScore = 0;
        var trainingGameCount = m_gamesPlayed;
        m_gamesPlayed = 1;
        
        // Now start tracking score...
        ResetDeathReasons();
        const int gamesToPlay = 100;
        var totalScore = 0.0;
        var moves = 0;
        while (m_gamesPlayed < gamesToPlay)
        {
            var oldScore = m_score;
            Learn(arenaWidth, arenaHeight, false);

            moves++;
            if (moves > 5000)
            {
                // Guess we're stuck in a loop - Restart.
                m_gamesPlayed++;
                ResetGame(arenaWidth, arenaHeight, resetBrain: false);
                m_deathReasons[DeathReason.StuckInLoop]++;
                moves = 0;
            }
                
            if (m_score < oldScore)
            {
                // We just died.
                totalScore += oldScore;
                moves = 0;
            }
        }

        // Write the high score for these params.
        System.Console.WriteLine($"MlLayers:{m_qTable.Layers.ToCsv(':')},TrainingGames:{trainingGameCount},AvgScore:{totalScore / gamesToPlay:F1},HiScore:{m_highScore},{m_learningConfig}");
            
        // Write reason for deaths (on single line).
        var reasons = string.Join(",", m_deathReasons.Select(p => $"{p.Key}:{p.Value}"));
        System.Console.WriteLine(reasons);
        System.Console.WriteLine();
#endif
    }

    private void ResetGame(int arenaWidth, int arenaHeight, bool resetBrain)
    {
        m_snake = new Snake(arenaWidth, arenaHeight);
        m_score = 0;
        SpawnFood(arenaWidth, arenaHeight);
        
        if (resetBrain)
            m_qTable.Clear();
    }

    private static int ManhattanDistance(int x1, int y1, int x2, int y2) =>
        Math.Abs(x1 - x2) + Math.Abs(y1 - y2);

    public override void UpdateFrame(ScreenData screen)
    {
        screen.ClearChars();
        DrawGame(screen);

        Learn(ArenaWidth, ArenaHeight, false);
    }

    private void Learn(int arenaWidth, int arenaHeight, bool isTraining)
    {
        var oldState = GetCurrentState(arenaWidth, arenaHeight);
        var oldDistance = ManhattanDistance(m_snake.X, m_snake.Y, m_foodX, m_foodY);

        // Move the snake's head.
        var newDirection = m_qTable.ChooseMove(oldState, m_gamesPlayed, allowExploring: isTraining);
        m_snake.Move(newDirection);

        // Check if the snake is about to eat food.
        var isFood = m_snake.X == m_foodX && m_snake.Y == m_foodY;
        if (!isFood)
            m_stepsSinceFood++;
        
        var isDead = IsCollision(m_snake.X, m_snake.Y, arenaWidth, arenaHeight);
        var newState = isDead ? null : GetCurrentState(arenaWidth, arenaHeight);
        
        var reward = CalculateReward(oldDistance, isDead);
        m_qTable.UpdateQValue(oldState, newState, newDirection, reward);

        if (isDead)
        {
            // Find out why we died.
            if (m_snake.X < 0 || m_snake.X >= arenaWidth || m_snake.Y < 0 || m_snake.Y >= arenaHeight)
                m_deathReasons[DeathReason.CollisionWall]++;
            else if (m_snake.IsTailHit(m_snake.X, m_snake.Y))
                m_deathReasons[DeathReason.CollisionSelf]++;
            else
                m_deathReasons[DeathReason.Unknown]++;
            
            // Restart the game.
            m_gamesPlayed++;
            ResetGame(arenaWidth, arenaHeight, resetBrain: false);
            return;
        }

        // If eating food, increase score and snake length, then spawn new food.
        if (!isFood)
            return;
        m_score++;
        m_highScore = Math.Max(m_highScore, m_score);
        m_snake.Grow();
        SpawnFood(arenaWidth, arenaHeight);
    }

    private void DrawGame(ScreenData screen)
    {
        // Draw the snake.
        m_snake.DrawOn(screen);
        
        // Draw the food.
        screen.PrintAt(m_foodX, m_foodY, '\u2665');
        
        // Draw iteration.
        screen.PrintAt(0, 0, $"Game: {m_gamesPlayed}, Score: {m_score}, High Score: {m_highScore}");
    }

    private void SpawnFood(int screenWidth, int screenHeight)
    {
        do
        {
            // Spawn new food at empty location.
            m_foodX = m_rand.Next(0, screenWidth);
            m_foodY = m_rand.Next(0, screenHeight);
        }
        while (m_snake.X == m_foodX && m_snake.Y == m_foodY);

        m_stepsSinceFood = 0;
    }
    
    private bool IsCollision(int x, int y, int arenaWidth, int arenaHeight) =>
        x < 0 || x >= arenaWidth || y < 0 || y >= arenaHeight || m_snake.IsTailHit(x, y);

    private double CalculateReward(int oldDistance, bool isDead)
    {
        // Slight penalty for taking a long time.
        var reward = LearningConfig.TimePenaltyPerStep;
            
        // Death is a pretty poor choice.
        if (isDead)
            reward += LearningConfig.Death;

        // Boosted reward for eating food.
        if (m_snake.X == m_foodX && m_snake.Y == m_foodY)
            reward += LearningConfig.EatFood;

        // Bonus for moving toward the food.
        var newDistance = ManhattanDistance(m_snake.X, m_snake.Y, m_foodX, m_foodY);
        if (newDistance > oldDistance)
            reward += LearningConfig.AwayFood;
        else if (newDistance < oldDistance)
            reward -= LearningConfig.AwayFood;
        
        return reward;
    }
    
    private GameState GetCurrentState(int arenaWidth, int arenaHeight)
    {
        GameState.Flags flags;
        switch (m_snake.Direction)
        {
            case Direction.Left:
                flags = GameState.Flags.MovingLeft;
                break;
            case Direction.Right:
                flags = GameState.Flags.MovingRight;
                break;
            case Direction.Up:
                flags = GameState.Flags.MovingUp;
                break;
            case Direction.Down:
                flags = GameState.Flags.MovingDown;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (m_foodX < m_snake.X) flags |= GameState.Flags.FoodLeft;
        if (m_foodX > m_snake.X) flags |= GameState.Flags.FoodRight;
        if (m_foodY < m_snake.Y) flags |= GameState.Flags.FoodUp;
        if (m_foodY > m_snake.Y) flags |= GameState.Flags.FoodDown;

        var headPt = new IntPoint(m_snake.X, m_snake.Y);
        var foodPt = new IntPoint(m_foodX, m_foodY);
        var state = new GameState(flags, m_snake.Length, headPt, foodPt, m_stepsSinceFood);

        // Examine local view of cells.
        var localViewGrid = GetLocalViewGrid(
            headPt,
            pt => pt.X < 0 || pt.X >= arenaWidth || pt.Y < 0 || pt.Y >= arenaHeight,
            pt => m_snake.IsTailHit(pt.X, pt.Y),
            viewSize: GameState.LocalViewDimension);
        Array.Copy(localViewGrid, state.SurroundingCells, localViewGrid.Length);

        var localSpaceAwareness = GetLocalSpaceAwareness(
            headPt,
            pt => IsCollision(pt.X, pt.Y, arenaWidth, arenaHeight),
            GameState.SpaceAwarenessLength);
        Array.Copy(localSpaceAwareness, state.LocalSpaceAwareness, localSpaceAwareness.Length);

        return state;
    }

    private static double[] GetLocalSpaceAwareness(
        IntPoint head,
        Func<IntPoint, bool> isBlocked,
        int maxDepth)
    {
        var results = new double[4]; // Left, Forward, Right

        var directions = new[]
        {
            Direction.Up,
            Direction.Down,
            Direction.Left,
            Direction.Right
        };

        for (var i = 0; i < directions.Length; i++)
        {
            var dir = directions[i];
            var current = head;
            var free = 0;

            for (var step = 1; step <= maxDepth; step++)
            {
                current = new IntPoint(current.X + dir.ToVector().X, current.Y + dir.ToVector().Y);
                if (isBlocked(current))
                    break;

                free++;
            }

            results[i] = free / (double)maxDepth; // Scaled 0.0 to 1.0
        }

        return results;
    }

    // todo - move to snake class?
    private static double[] GetLocalViewGrid(
        IntPoint head,
        Func<IntPoint, bool> isWall,
        Func<IntPoint, bool> isSnake,
        int viewSize = 5)
    {
        var half = viewSize / 2;
        var grid = new double[viewSize * viewSize];

        // Local coordinates: (0,0) is top-left of local grid
        for (var dy = -half; dy <= half; dy++)
        {
            for (var dx = -half; dx <= half; dx++)
            {
                // Relative point in local space
                var local = new IntPoint(dx, dy);

                // Map to actual world space
                var world = new IntPoint(head.X + local.X, head.Y + local.Y);

                // Get content at this world cell
                var index = (dy + half) * viewSize + dx + half;
                grid[index] = isWall(world) ? -1.0
                    : isSnake(world) ? -0.5
                    : 0.0;
            }
        }

        return grid;
    }
}