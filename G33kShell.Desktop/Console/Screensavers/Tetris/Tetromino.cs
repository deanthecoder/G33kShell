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

namespace G33kShell.Desktop.Console.Screensavers.Tetris;

public enum Tetromino
{
    I,
    O,
    T,
    S,
    Z,
    J,
    L
}

public readonly record struct Cell(int X, int Y);

public readonly record struct CandidateMove(Tetromino Piece, int Rotation, int X, int Y, PlacementStats Stats, double NeuralScore, double Score);

public readonly record struct PlacementStats(
    int LinesCleared,
    int Holes,
    int InaccessibleHoles,
    int Overhangs,
    int AggregateHeight,
    int MaxHeight,
    int Bumpiness,
    int WellDepth,
    int LandingHeight,
    int CoveredCells,
    int RightWellDepth,
    int ColumnTransitions,
    int RowTransitions,
    int BlockedWellCells,
    int CellsAboveHoles);
