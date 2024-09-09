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
using Avalonia.Media;

namespace G33kShell.Desktop.Console;

[DebuggerDisplay("Ch: {Ch}")]
public class Attr
{
    public char Ch { get; private set; }
    public IBrush Foreground { get; set; }
    public IBrush Background { get; set; }

    public Attr()
    {
        Clear();
    }
    
    public void Clear() => Ch = ' ';

    public void Set(char ch) => Ch = ch;
    public void Set(Attr attr) => Set(attr.Ch);
}