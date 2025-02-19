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

namespace G33kShell.Desktop.Terminal.Attributes;

/// <summary>
/// Attribute used to categorize a command type.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class CommandCategoryAttribute : Attribute
{
    public CommandType Category { get; }

    public CommandCategoryAttribute(CommandType category)
    {
        Category = category;
    }
}