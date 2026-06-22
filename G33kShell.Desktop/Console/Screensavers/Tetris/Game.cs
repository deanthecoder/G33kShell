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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Tetris;

/// <summary>
/// Tetris simulation used for both display and neuroevolution training.
/// </summary>
[DebuggerDisplay("Rating = {Rating}, Score = {Score}, Lines = {Lines}")]
public class Game : AiGameBase
{
    public const int BoardWidth = 10;
    public const int BoardHeight = 20;

    private const int MaxPiecesPerTrainingGame = 1500;
    private readonly bool m_useTrainingTimeouts;
    private readonly GameState m_gameState = new GameState();
    private int m_piecesPlaced;
    private int m_tetrises;
    private int m_triples;
    private int m_doubles;
    private int m_lineClearEvents;
    private int m_singleClears;
    private PlacementStats m_lastStats;
    private readonly GameState m_lookaheadState = new GameState();
    private readonly Queue<Tetromino> m_pieceBag = new Queue<Tetromino>();
    private Tetromino m_currentPiece;

    public int[,] Board { get; } = new int[BoardWidth, BoardHeight];
    public double[,] CellAges { get; } = new double[BoardWidth, BoardHeight];
    public Tetromino NextPiece { get; private set; }
    public int Score { get; private set; }
    public int HighScore { get; private set; }
    public int Lines { get; private set; }
    public int Level => Math.Min(28, Lines / 10 + 1);

    public override bool IsGameOver => m_isGameOver;
    private bool m_isGameOver;

    public override (string Name, double Value, string Format)? BestObservedMetric =>
        ("Score", Score, "N0");

    public override double DegeneracyScore
    {
        get
        {
            if (m_piecesPlaced < 20)
                return 0.0;
            if (Lines == 0 && m_lastStats.MaxHeight > 16)
                return 0.7;
            return Math.Min(0.95, Math.Max(0.0, (m_lastStats.Holes - 14) / 18.0));
        }
    }

    public override string DegeneracyReason =>
        DegeneracyScore > 0.6 ? "messy stack" : string.Empty;

    public override double Rating
    {
        get
        {
            var tetrisRate = m_tetrises / Math.Max(1.0, Lines / 4.0);
            var lineEfficiency = Lines / Math.Max(1.0, m_piecesPlaced);
            var scoreEfficiency = Score / Math.Max(1.0, m_piecesPlaced);
            var clearPurity = m_tetrises / Math.Max(1.0, m_lineClearEvents);
            var wastedClearEvents = Math.Max(0, m_lineClearEvents - m_tetrises);
            var capReached = m_useTrainingTimeouts && m_piecesPlaced >= MaxPiecesPerTrainingGame;
            var lineScore = Lines * 105.0 +
                            m_doubles * 40.0 +
                            m_triples * 150.0 +
                            m_tetrises * 9000.0 +
                            tetrisRate * 14_000.0 +
                            clearPurity * 18_000.0 +
                            lineEfficiency * 95_000.0 +
                            scoreEfficiency * 80.0;
            var survivalScore = Math.Min(m_piecesPlaced, MaxPiecesPerTrainingGame) * (capReached ? 2.0 : 4.0);
            var fussyClearPenalty = m_singleClears * (m_lastStats.MaxHeight < 14 && m_lastStats.Holes == 0 ? 470.0 : 115.0);
            var nonTetrisClearPenalty = wastedClearEvents * (capReached ? 260.0 : 90.0);
            var shapePenalty = m_lastStats.Holes * 55.0 +
                               m_lastStats.InaccessibleHoles * 80.0 +
                               m_lastStats.Overhangs * 22.0 +
                               m_lastStats.MaxHeight * 8.0 +
                               m_lastStats.Bumpiness * 3.0 +
                               m_lastStats.BlockedWellCells * 45.0 +
                               m_lastStats.CellsAboveHoles * 38.0 +
                               m_lastStats.CoveredCells * 24.0;
            return Score + lineScore + survivalScore - shapePenalty - fussyClearPenalty - nonTetrisClearPenalty;
        }
    }

    public Game(int arenaWidth, int arenaHeight, Brain brain, bool useTrainingTimeouts = false) : base(arenaWidth, arenaHeight, brain)
    {
        m_useTrainingTimeouts = useTrainingTimeouts;
    }

    public override IEnumerable<(string Name, string Value)> ExtraGameStats()
    {
        yield return ("Score", Score.ToString("N0"));
        yield return ("Lines", Lines.ToString("N0"));
        yield return ("Tetrises", m_tetrises.ToString("N0"));
        yield return ("TetrisRate", (m_tetrises / Math.Max(1.0, Lines / 4.0)).ToString("P0"));
        yield return ("ClearPurity", (m_tetrises / Math.Max(1.0, m_lineClearEvents)).ToString("P0"));
        yield return ("ScorePiece", (Score / Math.Max(1.0, m_piecesPlaced)).ToString("N1"));
        yield return ("LinesPiece", (Lines / Math.Max(1.0, m_piecesPlaced)).ToString("N3"));
        yield return ("Singles", m_singleClears.ToString("N0"));
        yield return ("Doubles", m_doubles.ToString("N0"));
        yield return ("Triples", m_triples.ToString("N0"));
        yield return ("Pieces", m_piecesPlaced.ToString("N0"));
        yield return ("Holes", m_lastStats.Holes.ToString("N0"));
        yield return ("Buried", m_lastStats.CellsAboveHoles.ToString("N0"));
        yield return ("Overhangs", m_lastStats.Overhangs.ToString("N0"));
        yield return ("Height", m_lastStats.MaxHeight.ToString("N0"));
    }

    public override Game ResetGame()
    {
        Brain.ResetTemporalState();
        Array.Clear(Board, 0, Board.Length);
        Array.Clear(CellAges, 0, CellAges.Length);
        m_isGameOver = false;
        Score = 0;
        Lines = 0;
        m_piecesPlaced = 0;
        m_tetrises = 0;
        m_triples = 0;
        m_doubles = 0;
        m_lineClearEvents = 0;
        m_singleClears = 0;
        m_lastStats = default;
        m_pieceBag.Clear();
        m_currentPiece = RandomPiece();
        NextPiece = RandomPiece();
        return this;
    }

    public override void Tick()
    {
        if (IsGameOver)
            return;

        var move = ChooseBestMove();
        if (!move.HasValue)
        {
            m_isGameOver = true;
            return;
        }

        LockMove(move.Value);
    }

    public CandidateMove? ChooseBestMove()
    {
        CandidateMove? bestMove = null;
        foreach (var candidate in EnumerateCandidateMoves(m_currentPiece, Board))
        {
            var neuralScore = ScorePlacement(candidate.Stats, m_currentPiece, NextPiece, m_gameState);
            var score = neuralScore + GetTrainingBias(candidate.Stats) + GetNextPieceLookaheadScore(candidate);
            var scored = candidate with { NeuralScore = neuralScore, Score = score };
            if (!bestMove.HasValue || scored.Score > bestMove.Value.Score)
                bestMove = scored;
        }

        return bestMove;
    }

    private double GetNextPieceLookaheadScore(CandidateMove candidate)
    {
        var board = GetBoardAfterMove(Board, candidate);
        var viableMoves = 0;
        var bestScore = double.NegativeInfinity;
        foreach (var nextCandidate in EnumerateCandidateMoves(NextPiece, board))
        {
            viableMoves++;
            var neuralScore = ScorePlacement(nextCandidate.Stats, NextPiece, NextPiece, m_lookaheadState);
            var score = neuralScore + GetTrainingBias(nextCandidate.Stats);
            if (score > bestScore)
                bestScore = score;
        }

        if (viableMoves == 0)
            return -80.0;

        var flexibilityBonus = Math.Min(18, viableMoves) * 0.08;
        return bestScore * 0.42 + flexibilityBonus;
    }

    private double ScorePlacement(PlacementStats stats, Tetromino piece, Tetromino nextPiece, GameState state)
    {
        state.Reset(stats, piece, nextPiece);
        return ((Brain)Brain).ScorePlacement(state);
    }

    private static double GetTrainingBias(PlacementStats stats)
    {
        var wellSweetSpot = stats.RightWellDepth is >= 3 and <= 7 && stats.MaxHeight < 15 ? 1.2 : 0.0;
        var unsafeWell = stats.RightWellDepth > 10 || stats.MaxHeight >= 18 ? -1.4 : 0.0;
        var singleBurnsOpenWell = stats.LinesCleared == 1 && stats.RightWellDepth >= 3 && stats.Holes == 0 ? 1.35 : 0.0;
        var lineClearScore = stats.LinesCleared switch
        {
            4 => 22.0,
            3 => stats.RightWellDepth >= 2 ? -2.2 : -0.1,
            2 => stats.RightWellDepth >= 2 ? -2.8 : -0.45,
            1 => stats.MaxHeight < 15 && stats.Holes == 0 ? -3.25 : -0.75,
            _ => 0.0
        };

        return lineClearScore -
               singleBurnsOpenWell -
               stats.Holes * 0.45 -
               stats.InaccessibleHoles * 0.65 -
               stats.Overhangs * 0.18 -
               stats.MaxHeight * 0.025 -
               stats.BlockedWellCells * 0.75 -
               stats.CellsAboveHoles * 0.34 -
               stats.CoveredCells * 0.22 -
               stats.Bumpiness * 0.015 +
               Math.Min(10, stats.RightWellDepth) * 0.62 +
               wellSweetSpot +
               unsafeWell;
    }

    public void LockMove(CandidateMove move)
    {
        foreach (var cell in GetCells(move.Piece, move.Rotation, move.X, move.Y))
        {
            if (cell.Y < 0)
            {
                m_isGameOver = true;
                return;
            }

            Board[cell.X, cell.Y] = (int)move.Piece + 1;
            CellAges[cell.X, cell.Y] = 0.0;
        }

        var cleared = ClearCompletedLines(Board, CellAges);
        Lines += cleared;
        Score += GetLineScore(cleared) * Level;
        if (cleared > 0)
            m_lineClearEvents++;
        if (cleared == 1)
            m_singleClears++;
        if (cleared == 2)
            m_doubles++;
        if (cleared == 3)
            m_triples++;
        if (cleared == 4)
            m_tetrises++;

        m_lastStats = AnalyzeBoard(Board, cleared, GetLandingHeight(move));
        if (IsCollision(NextPiece, 0, 3, -1, Board))
            m_isGameOver = true;

        HighScore = Math.Max(HighScore, Score);
        m_piecesPlaced++;

        m_currentPiece = NextPiece;
        NextPiece = RandomPiece();

        if (m_useTrainingTimeouts && m_piecesPlaced >= MaxPiecesPerTrainingGame)
            m_isGameOver = true;
    }

    public void AgeSettledCells(double frames = 1.0)
    {
        for (var y = 0; y < BoardHeight; y++)
        {
            for (var x = 0; x < BoardWidth; x++)
            {
                if (Board[x, y] != 0)
                    CellAges[x, y] += frames;
            }
        }
    }

    private Tetromino RandomPiece()
    {
        if (m_pieceBag.Count == 0)
            RefillPieceBag();

        return m_pieceBag.Dequeue();
    }

    private void RefillPieceBag()
    {
        var pieces = Enum.GetValues<Tetromino>();
        for (var i = pieces.Length - 1; i > 0; i--)
        {
            var j = GameRand.Next(i + 1);
            (pieces[i], pieces[j]) = (pieces[j], pieces[i]);
        }

        foreach (var piece in pieces)
            m_pieceBag.Enqueue(piece);
    }

    private static int GetLineScore(int lines) =>
        lines switch
        {
            1 => 100,
            2 => 300,
            3 => 500,
            4 => 900,
            _ => 0
        };

    private static int ClearCompletedLines(int[,] board, double[,] cellAges = null)
    {
        var cleared = 0;
        for (var y = BoardHeight - 1; y >= 0; y--)
        {
            var full = true;
            for (var x = 0; x < BoardWidth; x++)
            {
                if (board[x, y] != 0)
                {
                    continue;
                }

                full = false;
                break;
            }

            if (!full)
                continue;

            cleared++;
            for (var yy = y; yy > 0; yy--)
            {
                for (var x = 0; x < BoardWidth; x++)
                {
                    board[x, yy] = board[x, yy - 1];
                    if (cellAges != null)
                        cellAges[x, yy] = cellAges[x, yy - 1];
                }
            }

            for (var x = 0; x < BoardWidth; x++)
            {
                board[x, 0] = 0;
                if (cellAges != null)
                    cellAges[x, 0] = 0.0;
            }
            y++;
        }

        return cleared;
    }

    private IEnumerable<CandidateMove> EnumerateCandidateMoves(Tetromino piece, int[,] sourceBoard)
    {
        var seen = new HashSet<(int Rotation, int X, int Y)>();
        for (var rotation = 0; rotation < GetRotationCount(piece); rotation++)
        {
            for (var x = -4; x < BoardWidth + 4; x++)
            {
                if (IsCollision(piece, rotation, x, -4, sourceBoard))
                    continue;

                var y = -4;
                while (!IsCollision(piece, rotation, x, y + 1, sourceBoard))
                    y++;

                if (y < -3 || !seen.Add((rotation, x, y)))
                    continue;

                var board = CloneBoard(sourceBoard);
                foreach (var cell in GetCells(piece, rotation, x, y))
                {
                    if (cell.Y >= 0)
                        board[cell.X, cell.Y] = (int)piece + 1;
                }

                var lines = ClearCompletedLines(board);
                var stats = AnalyzeBoard(board, lines, GetLandingHeight(piece, rotation, y));
                yield return new CandidateMove(piece, rotation, x, y, stats, 0.0, 0.0);
            }
        }
    }

    private static int[,] CloneBoard(int[,] source)
    {
        var clone = new int[BoardWidth, BoardHeight];
        Array.Copy(source, clone, source.Length);
        return clone;
    }

    private static int[,] GetBoardAfterMove(int[,] sourceBoard, CandidateMove move)
    {
        var board = CloneBoard(sourceBoard);
        foreach (var cell in GetCells(move.Piece, move.Rotation, move.X, move.Y))
        {
            if (cell.Y >= 0)
                board[cell.X, cell.Y] = (int)move.Piece + 1;
        }

        ClearCompletedLines(board);
        return board;
    }

    private static PlacementStats AnalyzeBoard(int[,] board, int linesCleared, int landingHeight)
    {
        var heights = new int[BoardWidth];
        var holes = 0;
        var inaccessibleHoles = 0;
        var coveredCells = 0;
        var cellsAboveHoles = 0;

        for (var x = 0; x < BoardWidth; x++)
        {
            var blockSeen = false;
            var columnHoles = 0;
            var blocksAboveFirstHole = 0;
            for (var y = 0; y < BoardHeight; y++)
            {
                if (board[x, y] != 0)
                {
                    if (!blockSeen)
                        heights[x] = BoardHeight - y;
                    blockSeen = true;
                    if (columnHoles > 0)
                        blocksAboveFirstHole++;
                    continue;
                }

                if (!blockSeen)
                    continue;

                holes++;
                columnHoles++;
                var leftBlocked = x == 0 || board[x - 1, y] != 0;
                var rightBlocked = x == BoardWidth - 1 || board[x + 1, y] != 0;
                if (leftBlocked && rightBlocked)
                    inaccessibleHoles++;
            }

            if (columnHoles > 0)
            {
                coveredCells += columnHoles;
                cellsAboveHoles += blocksAboveFirstHole;
            }
        }

        var aggregateHeight = heights.Sum();
        var maxHeight = heights.Max();
        var bumpiness = 0;
        for (var x = 0; x < BoardWidth - 1; x++)
            bumpiness += Math.Abs(heights[x] - heights[x + 1]);

        var wellDepth = 0;
        var rightWellDepth = 0;
        var blockedWellCells = 0;
        for (var x = 0; x < BoardWidth; x++)
        {
            var leftHeight = x == 0 ? BoardHeight : heights[x - 1];
            var rightHeight = x == BoardWidth - 1 ? BoardHeight : heights[x + 1];
            var depth = Math.Max(0, Math.Min(leftHeight, rightHeight) - heights[x]);
            wellDepth += depth;
            if (x == BoardWidth - 1)
                rightWellDepth = depth;
            if (depth > 1 && heights[x] > 0)
                blockedWellCells += depth;
        }

        var overhangs = 0;
        for (var y = 1; y < BoardHeight; y++)
        {
            for (var x = 0; x < BoardWidth; x++)
            {
                if (board[x, y] == 0 && board[x, y - 1] != 0)
                    overhangs++;
            }
        }

        var rowTransitions = 0;
        for (var y = 0; y < BoardHeight; y++)
        {
            var previousFilled = true;
            for (var x = 0; x < BoardWidth; x++)
            {
                var filled = board[x, y] != 0;
                if (filled != previousFilled)
                    rowTransitions++;
                previousFilled = filled;
            }
            if (!previousFilled)
                rowTransitions++;
        }

        var columnTransitions = 0;
        for (var x = 0; x < BoardWidth; x++)
        {
            var previousFilled = true;
            for (var y = 0; y < BoardHeight; y++)
            {
                var filled = board[x, y] != 0;
                if (filled != previousFilled)
                    columnTransitions++;
                previousFilled = filled;
            }
            if (!previousFilled)
                columnTransitions++;
        }

        return new PlacementStats(linesCleared, holes, inaccessibleHoles, overhangs, aggregateHeight, maxHeight, bumpiness,
            wellDepth, landingHeight, coveredCells, rightWellDepth, columnTransitions, rowTransitions, blockedWellCells, cellsAboveHoles);
    }

    private static int GetLandingHeight(CandidateMove move) => GetLandingHeight(move.Piece, move.Rotation, move.Y);

    private static int GetLandingHeight(Tetromino piece, int rotation, int y)
    {
        var maxY = GetShape(piece, rotation).Max(o => o.Y);
        return BoardHeight - (y + maxY);
    }

    private static bool IsCollision(Tetromino piece, int rotation, int x, int y, int[,] board)
    {
        foreach (var cell in GetCells(piece, rotation, x, y))
        {
            if (cell.X < 0 || cell.X >= BoardWidth || cell.Y >= BoardHeight)
                return true;
            if (cell.Y >= 0 && board[cell.X, cell.Y] != 0)
                return true;
        }

        return false;
    }

    public static IEnumerable<Cell> GetCells(Tetromino piece, int rotation, int x, int y)
    {
        foreach (var cell in GetShape(piece, rotation))
            yield return new Cell(x + cell.X, y + cell.Y);
    }

    private static int GetRotationCount(Tetromino piece) =>
        piece switch
        {
            Tetromino.O => 1,
            Tetromino.I or Tetromino.S or Tetromino.Z => 2,
            _ => 4
        };

    private static Cell[] GetShape(Tetromino piece, int rotation)
    {
        rotation %= GetRotationCount(piece);
        return piece switch
        {
            Tetromino.I => rotation == 0
                ? [new Cell(0, 1), new Cell(1, 1), new Cell(2, 1), new Cell(3, 1)]
                : [new Cell(2, 0), new Cell(2, 1), new Cell(2, 2), new Cell(2, 3)],
            Tetromino.O => [new Cell(1, 0), new Cell(2, 0), new Cell(1, 1), new Cell(2, 1)],
            Tetromino.T => rotation switch
            {
                0 => [new Cell(1, 0), new Cell(0, 1), new Cell(1, 1), new Cell(2, 1)],
                1 => [new Cell(1, 0), new Cell(1, 1), new Cell(2, 1), new Cell(1, 2)],
                2 => [new Cell(0, 1), new Cell(1, 1), new Cell(2, 1), new Cell(1, 2)],
                _ => [new Cell(1, 0), new Cell(0, 1), new Cell(1, 1), new Cell(1, 2)]
            },
            Tetromino.S => rotation == 0
                ? [new Cell(1, 0), new Cell(2, 0), new Cell(0, 1), new Cell(1, 1)]
                : [new Cell(1, 0), new Cell(1, 1), new Cell(2, 1), new Cell(2, 2)],
            Tetromino.Z => rotation == 0
                ? [new Cell(0, 0), new Cell(1, 0), new Cell(1, 1), new Cell(2, 1)]
                : [new Cell(2, 0), new Cell(1, 1), new Cell(2, 1), new Cell(1, 2)],
            Tetromino.J => rotation switch
            {
                0 => [new Cell(0, 0), new Cell(0, 1), new Cell(1, 1), new Cell(2, 1)],
                1 => [new Cell(1, 0), new Cell(2, 0), new Cell(1, 1), new Cell(1, 2)],
                2 => [new Cell(0, 1), new Cell(1, 1), new Cell(2, 1), new Cell(2, 2)],
                _ => [new Cell(1, 0), new Cell(1, 1), new Cell(0, 2), new Cell(1, 2)]
            },
            _ => rotation switch
            {
                0 => [new Cell(2, 0), new Cell(0, 1), new Cell(1, 1), new Cell(2, 1)],
                1 => [new Cell(1, 0), new Cell(1, 1), new Cell(1, 2), new Cell(2, 2)],
                2 => [new Cell(0, 1), new Cell(1, 1), new Cell(2, 1), new Cell(0, 2)],
                _ => [new Cell(0, 0), new Cell(1, 0), new Cell(1, 1), new Cell(1, 2)]
            }
        };
    }
}
