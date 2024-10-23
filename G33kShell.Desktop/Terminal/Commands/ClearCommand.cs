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
using G33kShell.Desktop.Terminal.Attributes;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription("Clears the terminal screen.")]
public class ClearCommand : CommandBase
{
    protected override Task<bool> Run(ITerminalState state)
    {
        state.CliPrompt.Parent.ScrollChildren(state.CliPrompt.Parent.Y - state.CliPrompt.Y - 2);
        return Task.FromResult(true);
    }
}