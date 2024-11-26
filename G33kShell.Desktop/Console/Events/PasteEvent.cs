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
using System;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Events;

/// <summary>
/// Represents an event that is triggered when a paste operation occurs in the console view.
/// </summary>
public class PasteEvent : ConsoleEvent
{
    public string[] Items { get; }

    public PasteEvent([NotNull] string[] items)
    {
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }
}