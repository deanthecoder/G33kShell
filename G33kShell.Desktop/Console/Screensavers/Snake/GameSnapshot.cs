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
namespace G33kShell.Desktop.Console.Screensavers.Snake;

public class GameSnapshot
{
    private readonly char[,] m_arena;

    public GameSnapshot(int arenaWidth, int arenaHeight, Snake snake, int foodX, int foodY)
    {
        m_arena = new char[arenaWidth, arenaHeight];
        for (var y = 0; y < arenaHeight; y++)
        {
            for (var x = 0; x < arenaWidth; x++)
                m_arena[x, y] = '.';
        }

        foreach (var segment in snake.Segments)
            m_arena[segment.X, segment.Y] = '*';
            
        m_arena[snake.X, snake.Y] = snake.Direction switch
        {
            Direction.Left => 'L',
            Direction.Right => 'R',
            Direction.Up => 'U',
            _ => 'D'
        };
            
        m_arena[foodX, foodY] = 'F';
    }

    public void WriteToConsole()
    {
        for (var y = 0; y < m_arena.GetLength(1); y++)
        {
            for (var x = 0; x < m_arena.GetLength(0); x++)
                System.Console.Write(m_arena[x, y]);
            System.Console.WriteLine();
        }
            
        System.Console.WriteLine();
    }
}