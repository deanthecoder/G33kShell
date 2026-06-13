// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
// purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Tetris;

/// <summary>
/// Encodes the board resulting from one candidate Tetris placement.
/// </summary>
public class GameState : IAiGameState
{
    public const int InputCount = 18;

    private PlacementStats m_stats;
    private Tetromino m_piece;
    private Tetromino m_nextPiece;

    public void Reset(PlacementStats stats, Tetromino piece, Tetromino nextPiece)
    {
        m_stats = stats;
        m_piece = piece;
        m_nextPiece = nextPiece;
    }

    public void FillInputVector(double[] inputVector)
    {
        inputVector[0] = 1.0;
        inputVector[1] = (m_stats.LinesCleared / 4.0 * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[2] = (m_stats.Holes / 18.0 * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[3] = (m_stats.InaccessibleHoles / 12.0 * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[4] = (m_stats.Overhangs / 18.0 * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[5] = (m_stats.AggregateHeight / 120.0 * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[6] = (m_stats.MaxHeight / (double)Game.BoardHeight * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[7] = (m_stats.Bumpiness / 50.0 * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[8] = (m_stats.WellDepth / 20.0 * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[9] = (m_stats.LandingHeight / (double)Game.BoardHeight * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[10] = (m_stats.CoveredCells / 30.0 * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[11] = (m_stats.RightWellDepth / 10.0 * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[12] = (m_stats.ColumnTransitions / 80.0 * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[13] = (m_stats.RowTransitions / 180.0 * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[14] = (m_stats.BlockedWellCells / 8.0 * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[15] = (m_stats.CellsAboveHoles / 40.0 * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[16] = ((int)m_piece / 6.0 * 2.0 - 1.0).Clamp(-1.0, 1.0);
        inputVector[17] = ((int)m_nextPiece / 6.0 * 2.0 - 1.0).Clamp(-1.0, 1.0);
    }
}
