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
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NClap.Metadata;
using Newtonsoft.Json;
using TextCopy;

namespace G33kShell.Desktop.Terminal.Commands;

public class ClipCommand : CommandBase
{
    [PositionalArgument(0, Description = "Select a single line (1+).")]
    public int? LineNumber { get; [UsedImplicitly] set; }

    public override Task<bool> Run(ITerminalState state)
    {
        var output = state.CommandHistory.Commands.LastOrDefault()?.Output;
        if (output != null)
        {
            if (LineNumber != null)
            {
                var lines = output.ReplaceLineEndings("\n").Split("\n");
                if (LineNumber >= 1 && LineNumber <= lines.Length)
                    output = lines[LineNumber.Value - 1];
            }
            
            ClipboardService.SetText(output);
        }
        
        return Task.FromResult(true);
    }
}