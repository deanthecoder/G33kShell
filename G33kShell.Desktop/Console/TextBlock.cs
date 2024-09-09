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

using System.Diagnostics;
using System.Linq;
using CSharp.Core.Extensions;

namespace G33kShell.Desktop.Console;

[DebuggerDisplay("TextBlock:{X},{Y} {Width}x{Height} T:{Text}")]
public class TextBlock : Canvas
{
    public string[] Text { get; private set; }
    
    public TextBlock Init(string s) =>
        Init(s.ReadAllLines().ToArray());

    public TextBlock Init(params string[] lines)
    {
        Text = lines;
        base.Init(Text.Max(o => o.Length), Text.Length);
        return this;
    }

    public override void Render()
    {
        base.Render();

        for (var i = 0; i < Text.Length; i++)
            Screen.PrintAt(0, i, Text[i]);
    }
}