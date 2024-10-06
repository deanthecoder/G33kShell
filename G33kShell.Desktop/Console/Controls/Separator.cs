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
using System.Diagnostics;

namespace G33kShell.Desktop.Console.Controls;

[DebuggerDisplay("Separator: {Width}")]
public class Separator : Visual
{
    private const char SeparatorCharacter = '─';

    public Separator(int width) : base(width, 1)
    {
    }

    public override void Render(ScreenData screen) =>
        screen.PrintAt(0, 0, new string(SeparatorCharacter, Width));
}