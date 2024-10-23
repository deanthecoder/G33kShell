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
using System.Threading.Tasks;
using CSharp.Core.Extensions;

namespace G33kShell.Desktop.Terminal.Commands;

public class PopDCommand : CommandBase
{
    protected override Task<bool> Run(ITerminalState state)
    {
        try
        {
            if (state.DirStack.Count > 0)
            {
                var prevDir = state.DirStack.Pop(); // pop last stored directory from stack
                if (prevDir.Exists())
                {
                    state.CurrentDirectory = prevDir;
                    return Task.FromResult(true);
                }
            }
        }
        catch (Exception)
        {
            // Fall through.
        }

        WriteLine("No directories in the history.");
        return Task.FromResult(false);
    }
}