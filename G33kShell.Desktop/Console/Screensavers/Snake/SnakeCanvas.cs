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
using G33kShell.Desktop.Console.Controls;
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
    [UsedImplicitly] private int m_exploratoryDeaths;
    private Snake m_snake;
    private int m_foodX;
    private int m_foodY;
    private int m_gamesPlayed = 1;
    private int m_highScore;
    private int m_score;
    private LearningConfig m_learningConfig;

    private const int TrainingWidth = 32;
    private const int TrainingHeight = 24;
    private int ArenaWidth { get; }
    private int ArenaHeight { get; }

    private enum DeathReason
    {
        CollisionWall,
        CollisionSelf,
        NoValidMoves,
        Unknown
    }

    [UsedImplicitly]
    public bool DebugMode { get; set; }

    public SnakeCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 45)
    {
        Name = "snake";

        ResetDeathReasons();

        DebugMode = false;
        ArenaWidth = DebugMode ? TrainingWidth : screenWidth;
        ArenaHeight = DebugMode ? TrainingHeight : screenHeight;
        m_qTable = new QTable(m_rand);
    }

    private void ResetDeathReasons()
    {
        m_exploratoryDeaths = 0;
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
        //if (DebugMode)
            PreLearn(TrainingWidth, TrainingHeight);
    }

    [UsedImplicitly]
    private void PreLearn(int arenaWidth, int arenaHeight)
    {
        // Iterate through all combinations.
        foreach (var learningConfig in LearningConfig.AllCombinations(m_learningConfig))
        {
            // Prepare to learn.
            m_learningConfig = learningConfig.Clone();
            ResetGame(arenaWidth, arenaHeight, resetBrain: true);

            // Train until stable exploration.
            while (m_learningConfig.ExplorationRate > m_learningConfig.MinExplorationRate)
                Learn(arenaWidth, arenaHeight);

            m_highScore = 0;
            m_gamesPlayed = 1;
#if false
            // Now start tracking score...
            ResetDeathReasons();
            const int gamesToPlay = 100;
            var totalScore = 0.0;
            while (m_gamesPlayed < gamesToPlay)
            {
                var oldScore = m_score;
                Learn(arenaWidth, arenaHeight);

                if (m_score < oldScore)
                {
                    // We just died.
                    totalScore += oldScore;
                }
            }

            // Write the high score for these params.
            System.Console.WriteLine($"AvgScore:{totalScore / gamesToPlay:F1},HiScore:{m_highScore},{learningConfig}");
            
            // Write reason for deaths (on single line).
            var reasons = string.Join(",", m_deathReasons.Select(p => $"{p.Key}:{p.Value}"));
            reasons += $",ExploratoryDeaths:{m_exploratoryDeaths}";
            System.Console.WriteLine(reasons);
            System.Console.WriteLine();
#endif
        }
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

        Learn(ArenaWidth, ArenaHeight);
    }

    private void Learn(int arenaWidth, int arenaHeight)
    {
        var oldState = GetCurrentState(arenaWidth, arenaHeight);
        var oldDistance = ManhattanDistance(m_snake.X, m_snake.Y, m_foodX, m_foodY);

        // Move the snake's head.
        var newDirection = m_qTable.ChooseMove(oldState, m_learningConfig, m_snake.Direction, out var noValidMove, out var wasExploratory);
        m_snake.Move(newDirection);

        // Check if the snake is about to eat food.
        var isFood = m_snake.X == m_foodX && m_snake.Y == m_foodY;
        var isDead = IsCollision(m_snake.X, m_snake.Y, arenaWidth, arenaHeight);
        var newState = isDead ? null : GetCurrentState(arenaWidth, arenaHeight);
        
        var reward = CalculateReward(oldDistance, isDead);
        m_qTable.UpdateQValue(oldState, newState, m_learningConfig, newDirection, reward);

        // Decay exploration rate if using dynamic exploration.
        m_learningConfig.DecayExplorationRate();

        if (isDead)
        {
            // Find out why we died.
            if (wasExploratory)
                m_exploratoryDeaths++;
            if (noValidMove)
                m_deathReasons[DeathReason.NoValidMoves]++;
            else if (m_snake.X < 0 || m_snake.X >= arenaWidth || m_snake.Y < 0 || m_snake.Y >= arenaHeight)
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
    }

    private static (int, int) GetNextPosition(int x, int y, Direction dir)
    {
        return dir switch
        {
            Direction.Left => (x - 1, y),
            Direction.Right => (x + 1, y),
            Direction.Up => (x, y - 1),
            Direction.Down => (x, y + 1),
            _ => (x, y)
        };
    }

    private bool IsCollision(int x, int y, int arenaWidth, int arenaHeight) =>
        x < 0 || x >= arenaWidth || y < 0 || y >= arenaHeight || m_snake.IsTailHit(x, y);

    private double CalculateReward(int oldDistance, bool isDead)
    {
        // Slight penalty for taking a long time.
        var reward = m_learningConfig.TimePenaltyPerStep;
            
        // Death is a pretty poor choice.
        if (isDead)
            reward += m_learningConfig.Death;

        // Boosted reward for eating food.
        if (m_snake.X == m_foodX && m_snake.Y == m_foodY)
            reward += m_learningConfig.EatFood;

        // Bonus for moving toward the food.
        var newDistance = ManhattanDistance(m_snake.X, m_snake.Y, m_foodX, m_foodY);
        reward += newDistance > oldDistance ? m_learningConfig.AwayFood : 0.0;

        return reward;
    }
    
    private GameState GetCurrentState(int screenWidth, int screenHeight)
    {
        var (straightX, straightY) = GetNextPosition(m_snake.X, m_snake.Y, m_snake.Direction);
        var (leftX, leftY) = GetNextPosition(m_snake.X, m_snake.Y, m_snake.Direction.TurnLeft());
        var (rightX, rightY) = GetNextPosition(m_snake.X, m_snake.Y, m_snake.Direction.TurnRight());

        // Compute relative food direction based on current movement.
        bool foodStraight = false, foodLeftRelative = false, foodRightRelative = false;
        switch (m_snake.Direction)
        {
            case Direction.Left:
                foodStraight = m_foodX < m_snake.X;
                foodLeftRelative = m_foodY > m_snake.Y;  // left turn (downwards)
                foodRightRelative = m_foodY < m_snake.Y; // right turn (upwards)
                break;
            case Direction.Right:
                foodStraight = m_foodX > m_snake.X;
                foodLeftRelative = m_foodY < m_snake.Y;  // left turn (upwards)
                foodRightRelative = m_foodY > m_snake.Y; // right turn (downwards)
                break;
            case Direction.Up:
                foodStraight = m_foodY < m_snake.Y;
                foodLeftRelative = m_foodX < m_snake.X;  // left turn (leftwards)
                foodRightRelative = m_foodX > m_snake.X; // right turn (rightwards)
                break;
            case Direction.Down:
                foodStraight = m_foodY > m_snake.Y;
                foodLeftRelative = m_foodX > m_snake.X;  // left turn (rightwards)
                foodRightRelative = m_foodX < m_snake.X; // right turn (leftwards)
                break;
        }

        var dangerStraight = IsCollision(straightX, straightY, screenWidth, screenHeight);
        var dangerLeft = IsCollision(leftX, leftY, screenWidth, screenHeight);
        var dangerRight = IsCollision(rightX, rightY, screenWidth, screenHeight);

        var flags = GameState.Flags.None;
        if (dangerStraight) flags |= GameState.Flags.DangerStraight;
        if (dangerLeft) flags |= GameState.Flags.DangerLeft;
        if (dangerRight) flags |= GameState.Flags.DangerRight;

        if (m_snake.Direction == Direction.Left) flags |= GameState.Flags.MovingLeft;
        if (m_snake.Direction == Direction.Right) flags |= GameState.Flags.MovingRight;
        if (m_snake.Direction == Direction.Up) flags |= GameState.Flags.MovingUp;
        if (m_snake.Direction == Direction.Down) flags |= GameState.Flags.MovingDown;

        if (foodStraight) flags |= GameState.Flags.FoodStraight;
        if (foodLeftRelative) flags |= GameState.Flags.FoodLeftRelative;
        if (foodRightRelative) flags |= GameState.Flags.FoodRightRelative;

        return new GameState(flags);
    }
}