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
using System.Linq;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

public class QTable
{
    private readonly Random m_rand;
    private readonly Dictionary<GameState, Dictionary<Direction, double>> m_qTable = new();

    public QTable([NotNull] Random rand)
    {
        m_rand = rand ?? throw new ArgumentNullException(nameof(rand));
    }

    public Direction ChooseMove(GameState state, LearningConfig learningConfig, Direction snakeDirection, out bool noValidMove, out bool wasExploratory)
    {
        EnsureQTableEntryExists(state);

        // Filter out reverse direction.
        var possibleActions = m_qTable[state].Keys
            .Where(a => !a.IsReverse(snakeDirection))
            .ToList();

        // If no valid move remains (corner case), fallback to all actions.
        noValidMove = possibleActions.Count == 0;
        if (noValidMove)
            possibleActions = m_qTable[state].Keys.ToList();

        // Exploration vs exploitation.
        if (m_rand.NextDouble() < learningConfig.ExplorationRate)
        {
            wasExploratory = true;
            return possibleActions[m_rand.Next(possibleActions.Count)];
        }

        // Exploitation: choose best move among allowed actions.
        wasExploratory = false;
        return FindBestDirection(state, snakeDirection, possibleActions);
    }

    public void UpdateQValue(GameState oldState, GameState newState, LearningConfig learningConfig, Direction direction, double reward)
    {
        EnsureQTableEntryExists(oldState);
        var value = m_qTable[oldState];

        // Get the current Q-value
        var oldQ = value[direction];

        // Find the maximum future Q-value from the new state
        var maxFutureQ = 0.0;
        if (newState != null && m_qTable.ContainsKey(newState))
            maxFutureQ = m_qTable[newState].Values.Max();

        // Q-learning formula: Q(s, a) = Q(s, a) + α * (reward + γ * max(Q(s', a')) - Q(s, a))
        var newQ = oldQ + learningConfig.LearningRate * (reward + learningConfig.DiscountFactor * maxFutureQ - oldQ);
        value[direction] = newQ;
    }

    public void Clear() => m_qTable.Clear();

    private void EnsureQTableEntryExists(GameState state)
    {
        // Ensure state exists in Q-table.
        if (!m_qTable.ContainsKey(state))
        {
            m_qTable[state] = new Dictionary<Direction, double>
            {
                {
                    Direction.Left, 0.0
                },
                {
                    Direction.Right, 0.0
                },
                {
                    Direction.Up, 0.0
                },
                {
                    Direction.Down, 0.0
                }
            };
        }
    }

    private Direction FindBestDirection(GameState state, Direction direction, List<Direction> possibleActions)
    {
        var stats = m_qTable[state];
        var bestMoveIndex = 0;
        var bestMoveScore = double.MinValue;
        for (var i = 0; i < possibleActions.Count; i++)
        {
            var score = stats[possibleActions[i]];

            if (possibleActions[i] == direction)
                score += 0.1; // Prefer current direction.

            if (score <= bestMoveScore)
                continue;
            bestMoveScore = score;
            bestMoveIndex = i;
        }

        return possibleActions[bestMoveIndex];
    }
}