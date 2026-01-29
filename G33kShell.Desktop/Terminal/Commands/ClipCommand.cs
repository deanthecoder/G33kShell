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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DTC.Core;
using DTC.Core.Extensions;
using G33kShell.Desktop.Terminal.Attributes;
using JetBrains.Annotations;
using NClap.Metadata;
using TextCopy;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription("Copies the output from the previous command into the system clipboard.",
    "All output is copied, unless a specific line number is requested.",
    "Tip: Use $<n> in any command to reuse line n of the previous output (e.g. cat $3).")]
public class ClipCommand : CommandBase
{
    [NamedArgument(Description = "Capture the command, as well as the output.", ShortName = "a")]
    public bool CaptureAll { get; [UsedImplicitly] set; }

    [NamedArgument(Description = "Show line numbers.", ShortName = "n")]
    public bool ShowLineNumbers { get; [UsedImplicitly] set; }

    [PositionalArgument(0, Description = "Select a single line (1+).")]
    public int? LineNumber { get; [UsedImplicitly] set; }

    [NamedArgument(Description = "Open in the default text editor.", ShortName = "o")]
    public bool AutoOpen { get; [UsedImplicitly] set; }

    protected override Task<bool> Run(ITerminalState state)
    {
        var lastCommand = state.CommandHistory.Commands.LastOrDefault();
        var output = lastCommand?.Output;
        if (output == null)
            return Task.FromResult(true);

        var lines = output.ReplaceLineEndings("\n").Split("\n");
        if (ShowLineNumbers)
        {
            var i = 0;
            foreach (var line in lines)
                WriteLine($"{++i,2}: {line}");
            return Task.FromResult(true);
        }
        
        if (LineNumber != null)
        {
            if (LineNumber >= 1 && LineNumber <= lines.Length)
                output = lines[LineNumber.Value - 1];
        }
        
        // Need the command too?
        if (CaptureAll)
            output = $">{lastCommand.Command}\n{output}";
            
        ClipboardService.SetText(output);

        if (AutoOpen)
        {
            var tempFile = new TempFile(".txt");
            tempFile.WriteAllText(output);
            
            // Open tempFile in the OS's default .txt viewer.
            ((FileInfo)tempFile).OpenWithDefaultViewer();
            
            // Delete the file after 20 seconds.
            _ = Task.Delay(TimeSpan.FromSeconds(20)).ContinueWith(_ => tempFile.Dispose());
        }

        return Task.FromResult(true);
    }
}
