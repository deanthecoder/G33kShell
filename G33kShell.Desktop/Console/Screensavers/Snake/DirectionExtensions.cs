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

public static class DirectionExtensions
{
    public static Direction TurnLeft(this Direction dir) =>
        dir switch
        {
            Direction.Left => Direction.Down,
            Direction.Right => Direction.Up,
            Direction.Up => Direction.Left,
            Direction.Down => Direction.Right,
            _ => dir
        };

    public static Direction TurnRight(this Direction dir) =>
        dir switch
        {
            Direction.Left => Direction.Up,
            Direction.Right => Direction.Down,
            Direction.Up => Direction.Right,
            Direction.Down => Direction.Left,
            _ => dir
        };

    public static bool IsReverse(this Direction action, Direction current)
    {
        return (current == Direction.Left && action == Direction.Right) ||
               (current == Direction.Right && action == Direction.Left) ||
               (current == Direction.Up && action == Direction.Down) ||
               (current == Direction.Down && action == Direction.Up);
    }
}