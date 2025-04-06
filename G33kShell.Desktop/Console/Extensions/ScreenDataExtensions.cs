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
namespace G33kShell.Desktop.Console.Extensions;

public static class ScreenDataExtensions
{
    public static void DrawBox(this ScreenData screen, int left, int top, int right, int bottom, string boxChars = "╔╗╚╝══║║")
    {
        var topLeft = boxChars[0];
        var topRight = boxChars[1];
        var bottomLeft = boxChars[2];
        var bottomRight = boxChars[3];
        var hTop = boxChars[4];
        var hBottom = boxChars[5];
        var vLeft = boxChars[6];
        var vRight = boxChars[7];

        // Top border
        screen.PrintAt(left, top, topLeft);
        screen.PrintAt(left + 1, top, new string(hTop, right - left - 1));
        screen.PrintAt(right, top, topRight);

        // Bottom border
        screen.PrintAt(left, bottom, bottomLeft);
        screen.PrintAt(left + 1, bottom, new string(hBottom, right - left - 1));
        screen.PrintAt(right, bottom, bottomRight);

        // Vertical borders
        for (var i = 1; i < bottom - top; i++)
        {
            screen.PrintAt(left, top + i, vLeft);
            screen.PrintAt(right, top + i, vRight);
        }
    }
}