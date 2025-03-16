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

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// AI-powered snake game.
/// </summary>
[DebuggerDisplay("SnakeCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class SnakeCanvas : ScreensaverBase
{
    private enum Direction
    {
        Left,
        Right,
        Up,
        Down
    }

    private readonly Random m_rand = new Random();
    private readonly LinkedList<(int X, int Y)> m_snakeSegments = [];
    private int m_snakeX;
    private int m_snakeY;
    private Direction m_snakeDirection;
    private int m_snakeLength;
    private int m_foodX;
    private int m_foodY;
    private int m_iteration = 1;
    private int m_highScore;
    private int m_score;

    private readonly Dictionary<GameState, Dictionary<Direction, double>> m_qTable = new();
    private double m_learningRate = 0.01;
    private double m_discountFactor = 0.95;
    private double m_explorationRate = 1.0;              // Start with 100% exploration.
    private readonly double m_minExplorationRate = 0.01; // Lower bound for exploration.
    private double m_explorationDecayRate = 0.995;       // Decay factor per move.
    private double m_hitPenalty = -20.0;
    private double m_eatFoodBonus = 10.0;
    private double m_approachFoodBonus = 5.0;

    public SnakeCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 45)
    {
        Name = "snake";
    }

    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);
        ResetGame(screen.Width, screen.Height); 
        // PreLearn(16, 16);
    }

    // ReSharper disable once UnusedMember.Local
    private void PreLearn(int screenWidth, int screenHeight)
    {
        // Iterate through all combinations.
        var results = new List<(double learningRate, double discountFactor, double explorationDecayRate, int score, int moves, double hitPenalty, double eatFoodBonus, double approachFoodBonus)>();
        foreach (var approachFoodBonus in new[] { m_approachFoodBonus })
        foreach (var eatFoodBonus in new[] { m_eatFoodBonus })
        foreach (var learningRate in new[] { m_learningRate })
        foreach (var discountFactor in new[] { m_discountFactor })
        foreach (var explorationDecayRate in new[] { m_explorationDecayRate })
        foreach (var hitPenalty in new[] { m_hitPenalty })
        {
            // Learn.
            var highSum = 0;
            var movesSum = 0;
            var attempts = 6;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                m_learningRate = learningRate;
                m_discountFactor = discountFactor;
                m_explorationDecayRate = explorationDecayRate;
                m_hitPenalty = hitPenalty;
                m_eatFoodBonus = eatFoodBonus;
                m_approachFoodBonus = approachFoodBonus;
                
                ResetGame(screenWidth, screenHeight);
                m_highScore = 0;
                m_iteration = 1;
                m_qTable.Clear();

                var high = int.MinValue;
                var completed = 0.0;
                var iterations = 1000.0;
                while (m_highScore > high)
                {
                    high = m_highScore;
                    while (m_iteration - completed < iterations)
                        Learn(screenWidth, screenHeight);
                    completed += iterations;
                    iterations *= 1.5;
                }
                highSum += m_highScore;
                movesSum += (int)iterations;

                // Write the high score for these params.
                System.Console.WriteLine(
                    $"Score:{m_highScore},LearningRate:{learningRate},DiscountFactor:{discountFactor},ExplorationDecayRate:{explorationDecayRate},HitPenalty:{hitPenalty},EatFoodBonus:{eatFoodBonus},ApproachFoodBonus:{approachFoodBonus}");

            }
            
            results.Add((learningRate, m_discountFactor, m_explorationDecayRate, highSum / attempts, movesSum / attempts, m_hitPenalty, m_eatFoodBonus, m_approachFoodBonus));
        }
        
        System.Console.WriteLine("\nFinal results:");
        results = results.OrderBy(x => x.score).TakeLast(10).ToList();
        foreach (var result in results)
        {
            // Write out results (one per line) starting with score (E.g. Score:12,LearningRate:0.5,...).
            System.Console.WriteLine($"Score:{result.score},MovesPerScore:{result.moves / (double)result.score:F1},LearningRate:{result.learningRate},DiscountFactor:{result.discountFactor},ExplorationDecayRate:{result.explorationDecayRate},HitPenalty:{result.hitPenalty},EatFoodBonus:{result.eatFoodBonus},ApproachFoodBonus:{result.approachFoodBonus}");
        }
    }

    private void ResetGame(int screenWidth, int screenHeight)
    {
        m_snakeX = screenWidth / 2;
        m_snakeY = screenHeight / 2;
        m_snakeDirection = Direction.Left;
        m_snakeLength = 5;
        m_score = 0;
        SpawnFood(screenWidth, screenHeight);

        m_snakeSegments.Clear();
    }

    private static int ManhattanDistance(int x1, int y1, int x2, int y2) =>
        Math.Abs(x1 - x2) + Math.Abs(y1 - y2);

    public override void UpdateFrame(ScreenData screen)
    {
        screen.ClearChars();
        DrawGame(screen);

        Learn(screen.Width, screen.Height);
    }

    private void Learn(int screenWidth, int screenHeight)
    {
        var oldState = GetCurrentState(screenWidth, screenHeight);
        var oldDistance = ManhattanDistance(m_snakeX, m_snakeY, m_foodX, m_foodY);

        m_snakeDirection = ChooseMove(oldState);

        // Move the snake's head.
        switch (m_snakeDirection)
        {
            case Direction.Left:
                m_snakeX--;
                break;
            case Direction.Right:
                m_snakeX++;
                break;
            case Direction.Up:
                m_snakeY--;
                break;
            case Direction.Down:
                m_snakeY++;
                break;
        }

        // Check if the snake is about to eat food.
        var isFood = m_snakeX == m_foodX && m_snakeY == m_foodY;
        var isDead = IsCollision(m_snakeX, m_snakeY, screenWidth, screenHeight);

        var newState = GetCurrentState(screenWidth, screenHeight);
        var reward = CalculateReward(newState, oldDistance);
        UpdateQValue(oldState, newState, m_snakeDirection, reward);

        // Decay exploration rate if using dynamic exploration.
        m_explorationRate = Math.Max(m_minExplorationRate, m_explorationRate * m_explorationDecayRate);

        if (isDead)
        {
            m_iteration++;
            ResetGame(screenWidth, screenHeight);
            return;
        }

        // Update snake segments: add new head.
        m_snakeSegments.AddFirst((m_snakeX, m_snakeY));

        // If not eating food, remove the tail segment.
        if (!isFood && m_snakeSegments.Count > m_snakeLength)
            m_snakeSegments.RemoveLast();

        // If eating food, increase score and snake length, then spawn new food.
        if (isFood)
        {
            m_score++;
            m_highScore = Math.Max(m_highScore, m_score);
            m_snakeLength += 5;
            SpawnFood(screenWidth, screenHeight);
        }
    }

    private void DrawGame(ScreenData screen)
    {
        // Draw the snake body.
        foreach (var (x, y) in m_snakeSegments)
            screen.PrintAt(x, y, '■');
        
        // Draw snake head.
        screen.PrintAt(m_snakeX, m_snakeY, '☻');
        
        // Draw the food.
        screen.PrintAt(m_foodX, m_foodY, '\u2665');
        
        // Draw iteration.
        screen.PrintAt(0, 0, $"Iteration: {m_iteration}, Score: {m_score}, High Score: {m_highScore}");
    }

    private void SpawnFood(int screenWidth, int screenHeight)
    {
        do
        {
            // Spawn new food at empty location.
            m_foodX = m_rand.Next(0, screenWidth);
            m_foodY = m_rand.Next(0, screenHeight);
        }
        while (m_snakeSegments.Any(o => o.X == m_foodX && o.Y == m_foodY));
    }

    private Direction ChooseMove(GameState state)
    {
        EnsureQTableEntryExists(state);

        // Filter out reverse direction.
        var possibleActions = m_qTable[state].Keys
            .Where(a => !IsReverse(a, m_snakeDirection))
            .ToList();
        
        // If no valid move remains (corner case), fallback to all actions.
        if (!possibleActions.Any())
            possibleActions = m_qTable[state].Keys.ToList();

        // Exploration vs exploitation.
        if (m_rand.NextDouble() < m_explorationRate)
            return possibleActions[m_rand.Next(possibleActions.Count)];

        // Exploitation: choose best move among allowed actions.
        return FindBestDirection(state, possibleActions);
    }

    private void EnsureQTableEntryExists(GameState state)
    {
        // Ensure state exists in Q-table.
        if (!m_qTable.ContainsKey(state))
        {
            m_qTable[state] = new Dictionary<Direction, double>
            {
                { Direction.Left, 0.0 },
                { Direction.Right, 0.0 },
                { Direction.Up, 0.0 },
                { Direction.Down, 0.0 }
            };
        }
    }

    private Direction FindBestDirection(GameState state, List<Direction> possibleActions)
    {
        var stats = m_qTable[state];
        var bestMoveIndex = 0;
        var bestMoveScore = double.MinValue;
        for (var i = 0; i < possibleActions.Count; i++)
        {
            var score = stats[possibleActions[i]];
            
            if (possibleActions[i] == m_snakeDirection)
                score += 0.1; // Prefer current direction.
            
            if (score <= bestMoveScore)
                continue;
            bestMoveScore = score;
            bestMoveIndex = i;
        }
        
        return possibleActions[bestMoveIndex];
    }

    private void UpdateQValue(GameState oldState, GameState newState, Direction direction, double reward)
    {
        EnsureQTableEntryExists(oldState);
        var value = m_qTable[oldState];

        // Get the current Q-value
        var oldQ = value[direction];

        // Find the maximum future Q-value from the new state
        var maxFutureQ = 0.0;
        if (m_qTable.ContainsKey(newState))
            maxFutureQ = m_qTable[newState].Values.Max();

        // Q-learning formula: Q(s, a) = Q(s, a) + α * (reward + γ * max(Q(s', a')) - Q(s, a))
        var newQ = oldQ + m_learningRate * (reward + m_discountFactor * maxFutureQ - oldQ);
        value[direction] = newQ;
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

    private static Direction TurnLeft(Direction dir) =>
        dir switch
        {
            Direction.Left => Direction.Down,
            Direction.Right => Direction.Up,
            Direction.Up => Direction.Left,
            Direction.Down => Direction.Right,
            _ => dir
        };

    private static Direction TurnRight(Direction dir) =>
        dir switch
        {
            Direction.Left => Direction.Up,
            Direction.Right => Direction.Down,
            Direction.Up => Direction.Right,
            Direction.Down => Direction.Left,
            _ => dir
        };

    private bool IsCollision(int x, int y, int screenWidth, int screenHeight) =>
        x < 0 || x >= screenWidth || y < 0 || y >= screenHeight || m_snakeSegments.Contains((x, y));

    private double CalculateReward(GameState state, int oldDistance)
    {
        var reward = 0.0;

        // Heavy penalty if moving into immediate danger.
        if (state.DangerStraight)
        {
            reward += m_hitPenalty;
            return reward; // No point adding more points. You be dead.
        }
        
        // Boosted reward for eating food.
        if (m_snakeX == m_foodX && m_snakeY == m_foodY)
            reward += m_eatFoodBonus;

        // Bonus for moving toward the food.
        var newDistance = ManhattanDistance(m_snakeX, m_snakeY, m_foodX, m_foodY);
        if (newDistance < oldDistance)
            reward += m_approachFoodBonus;

        return reward;
    }

    private static bool IsReverse(Direction action, Direction current)
    {
        return (current == Direction.Left && action == Direction.Right) ||
               (current == Direction.Right && action == Direction.Left) ||
               (current == Direction.Up && action == Direction.Down) ||
               (current == Direction.Down && action == Direction.Up);
    }
    
    private GameState GetCurrentState(int screenWidth, int screenHeight)
    {
        var (straightX, straightY) = GetNextPosition(m_snakeX, m_snakeY, m_snakeDirection);
        var (leftX, leftY) = GetNextPosition(m_snakeX, m_snakeY, TurnLeft(m_snakeDirection));
        var (rightX, rightY) = GetNextPosition(m_snakeX, m_snakeY, TurnRight(m_snakeDirection));

        // Compute relative food direction based on current movement.
        bool foodStraight = false, foodLeftRelative = false, foodRightRelative = false;
        switch (m_snakeDirection)
        {
            case Direction.Left:
                foodStraight = m_foodX < m_snakeX;
                foodLeftRelative = m_foodY > m_snakeY;  // left turn (downwards)
                foodRightRelative = m_foodY < m_snakeY; // right turn (upwards)
                break;
            case Direction.Right:
                foodStraight = m_foodX > m_snakeX;
                foodLeftRelative = m_foodY < m_snakeY;  // left turn (upwards)
                foodRightRelative = m_foodY > m_snakeY; // right turn (downwards)
                break;
            case Direction.Up:
                foodStraight = m_foodY < m_snakeY;
                foodLeftRelative = m_foodX < m_snakeX;  // left turn (leftwards)
                foodRightRelative = m_foodX > m_snakeX; // right turn (rightwards)
                break;
            case Direction.Down:
                foodStraight = m_foodY > m_snakeY;
                foodLeftRelative = m_foodX > m_snakeX;  // left turn (rightwards)
                foodRightRelative = m_foodX < m_snakeX; // right turn (leftwards)
                break;
        }

        return new GameState
        {
            DangerStraight = IsCollision(straightX, straightY, screenWidth, screenHeight),
            DangerLeft = IsCollision(leftX, leftY, screenWidth, screenHeight),
            DangerRight = IsCollision(rightX, rightY, screenWidth, screenHeight),
            MovingLeft = m_snakeDirection == Direction.Left,
            MovingRight = m_snakeDirection == Direction.Right,
            MovingUp = m_snakeDirection == Direction.Up,
            MovingDown = m_snakeDirection == Direction.Down,
            FoodStraight = foodStraight,
            FoodLeftRelative = foodLeftRelative,
            FoodRightRelative = foodRightRelative
        };
    }

    private class GameState
    {
        public bool DangerStraight { get; init; }
        public bool DangerLeft { get; init; }
        public bool DangerRight { get; init; }
        public bool MovingLeft { get; init; }
        public bool MovingRight { get; init; }
        public bool MovingUp { get; init; }
        public bool MovingDown { get; init; }
        public bool FoodStraight { get; init; }
        public bool FoodLeftRelative { get; init; }
        public bool FoodRightRelative { get; init; }

        public override bool Equals(object obj)
        {
            if (obj is not GameState other) return false;
            return DangerStraight == other.DangerStraight &&
                   DangerLeft == other.DangerLeft &&
                   DangerRight == other.DangerRight &&
                   MovingLeft == other.MovingLeft &&
                   MovingRight == other.MovingRight &&
                   MovingUp == other.MovingUp &&
                   MovingDown == other.MovingDown &&
                   FoodStraight == other.FoodStraight &&
                   FoodLeftRelative == other.FoodLeftRelative &&
                   FoodRightRelative == other.FoodRightRelative;
        }

        public override int GetHashCode()
        {
            var hash = 0;
            hash |= (DangerStraight ? 1 : 0) << 0;
            hash |= (DangerLeft ? 1 : 0) << 1;
            hash |= (DangerRight ? 1 : 0) << 2;
            hash |= (MovingLeft ? 1 : 0) << 3;
            hash |= (MovingRight ? 1 : 0) << 4;
            hash |= (MovingUp ? 1 : 0) << 5;
            hash |= (MovingDown ? 1 : 0) << 6;
            hash |= (FoodStraight ? 1 : 0) << 7;
            hash |= (FoodLeftRelative ? 1 : 0) << 8;
            hash |= (FoodRightRelative ? 1 : 0) << 9;
            return hash;
        }
    }
}