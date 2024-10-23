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
using System.Threading.Tasks;
using CSharp.Core.Extensions;

namespace G33kShell.Desktop.Terminal.Commands;

public class CdCommand : LocationCommand
{
    protected override Task<bool> Run(ITerminalState state)
    {
        var newDir = state.CurrentDirectory;
        try
        {
            newDir = state.CurrentDirectory.Resolve(Path).ToDir();
            if (newDir.Exists)
            {
                state.CurrentDirectory = newDir;
                return Task.FromResult(true);
            }
        }
        catch (Exception)
        {
            // Fall through.
        }
        
        WriteLine($"Directory not found: {newDir}");
        return Task.FromResult(false);
    }
}