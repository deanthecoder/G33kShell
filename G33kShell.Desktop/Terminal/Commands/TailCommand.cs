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
using CSharp.Core.Extensions;
using G33kShell.Desktop.Terminal.Attributes;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription("Output the last few lines of a file.", "Example: tail readme.md")]
public class TailCommand : LocationCommand
{
    [NamedArgument(Description = "Keep watching the file for changes.", ShortName = "w")]
    [UsedImplicitly]
    public bool Watch { get; set; }

    public TailCommand()
    {
        WarnIfCancelled = false;
    }

    protected override async Task<bool> Run(ITerminalState state)
    {
        try
        {
            var targetPath = GetTargetPath(state);
            var fileInfo = new FileInfo(targetPath);
            if (!fileInfo.Exists)
            {
                WriteLine($"File not found: {targetPath}");
                return false;
            }

            if (Watch)
            {
                await WatchFile(fileInfo, state);
            }
            else
            {
                await OutputLastLines(fileInfo, state.CliPrompt.Width, 10);
            }

            return true;
        }
        catch (Exception ex)
        {
            WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
    }

    private async Task WatchFile(FileInfo fileInfo, ITerminalState state)
    {
        var previousSize = long.MaxValue;

        while (!IsCancelRequested)
        {
            fileInfo.Refresh();
            if (fileInfo.Length != previousSize)
            {
                previousSize = fileInfo.Length;

                state.CliPrompt.ClearText();
                WriteLine();
                await OutputLastLines(fileInfo, state.CliPrompt.Width, state.CliPrompt.Root.Height - 2);
                WriteLine();
                state.CliPrompt.ScrollIntoView();
                state.CliPrompt.Backspace();
            }

            await Task.Delay(200); // Delay before refreshing again.
        }
    }

    private async Task OutputLastLines(FileInfo fileInfo, int width, int count)
    {
        var lastLines = await Task.Run(() => fileInfo.ReadAllLines()
            .TakeLast(count)
            .Select(o => o.Length > width ? o.Substring(0, width) : o)
            .ToArray());

        if (lastLines.Length == count)
            lastLines[0] = "...";

        foreach (var line in lastLines)
            WriteLine(line);
    }
}