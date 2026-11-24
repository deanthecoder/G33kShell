// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Collections.Generic;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Finds a short walking route around rock walls and holes in the arcade height map.
/// </summary>
/// <remarks>
/// Works on eight-pixel cells and permits small ledges that an unmounted fighter can jump.
/// </remarks>
internal static class TerrainNavigator
{
    private static readonly (int X, int Z)[] m_directions = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static GameInput Towards(Game game, Fighter actor, double targetX, double targetZ, bool allowExit = false)
    {
        const int cellSize = 8;
        var canJump = !actor.IsMount && actor.Moves.Jump != 0;
        var start = (X: (int)Math.Floor(actor.X / cellSize), Z: (int)Math.Floor(actor.Z / cellSize));
        var queue = new PriorityQueue<(int X, int Z), double>();
        var cost = new Dictionary<(int X, int Z), double> { [start] = 0 };
        var previous = new Dictionary<(int X, int Z), (int X, int Z)>();
        double Distance((int X, int Z) cell) => Math.Abs(cell.X * cellSize + 4 - targetX) + Math.Abs(cell.Z * cellSize + 4 - targetZ) * 2;
        var best = start;
        var bestDistance = Distance(start);
        queue.Enqueue(start, bestDistance);
        while (queue.TryDequeue(out var cell, out _) && cost.Count < 2500)
        {
            var distance = Distance(cell);
            if (distance < bestDistance)
            {
                best = cell;
                bestDistance = distance;
            }
            if (distance < 10)
                break;
            var height = game.Ground(cell.X * cellSize + 4, cell.Z * cellSize + 4);
            foreach (var direction in m_directions)
            {
                var next = (X: cell.X + direction.X, Z: cell.Z + direction.Z);
                var x = next.X * cellSize + 4;
                var z = next.Z * cellSize + 4;
                if (x < game.Progress + (allowExit ? -64 : 12) || x > game.Progress + (allowExit ? 448 : 360) || z < 3976 || z > 4472)
                    continue;
                var floor = game.Ground(x, z);
                if (floor <= 3840 || floor >= 5248 || height - floor > (canJump ? 56 : 12) || allowExit && floor - height > 12)
                    continue;
                // Check the same forward clearance used by the movement collision code.
                var edge = game.Ground(x + direction.X * 8, z);
                if (edge <= 3840 || edge >= 5248 || height - edge > (canJump ? 56 : 12) || allowExit && edge - height > 12)
                    continue;
                var nextCost = cost[cell] + 8 + (height - floor > 12 ? 24 : 0);
                if (cost.TryGetValue(next, out var oldCost) && oldCost <= nextCost)
                    continue;
                cost[next] = nextCost;
                previous[next] = cell;
                queue.Enqueue(next, nextCost + Distance(next));
            }
        }
        if (best == start)
            return default;
        while (previous[best] != start)
            best = previous[best];
        var dx = best.X * cellSize + 4 - actor.X;
        var dz = best.Z * cellSize + 4 - actor.Z;
        var inputX = Math.Abs(dx) > 2 ? Math.Sign(dx) : 0;
        var inputZ = Math.Abs(dz) > 2 ? Math.Sign(dz) : 0;
        var ahead = game.Ground(actor.X + inputX * 12, actor.Z + inputZ * 4);
        return new GameInput(inputX, inputZ, Jump: canJump && ahead < actor.Y - 12 && ahead > 3840);
    }
}
