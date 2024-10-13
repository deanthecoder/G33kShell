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
using System.Threading.Tasks;

namespace G33kShell.Desktop.Terminal.Commands;

public class ClearCommand : CommandBase
{
    public override Task<bool> Run(ITerminalState state)
    {
        state.CliPrompt.Parent.ScrollChildren(state.CliPrompt.Parent.Y - state.CliPrompt.Y - 2);
        return Task.FromResult(true);
    }
}