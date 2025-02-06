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

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription("Display disk space usage for each attached drive.")]
public class SpaceCommand : CommandBase
{
    protected override Task<bool> Run(ITerminalState state)
    {
        var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);
        foreach (var drive in drives)
        {
            var totalSpace = drive.TotalSize;
            var freeSpace = drive.AvailableFreeSpace;
            var usagePercentage = (double)(totalSpace - freeSpace) / totalSpace;

            // Build the size string and calculate its length.
            var infoText = drive.TotalSize.ToSize();

            // Progress bar width calculation.
            var totalWidth = state.CliPrompt.Width;
            var fixedTextWidth = drive.Name.Length + infoText.Length + 5;
            var progressWidth = totalWidth - fixedTextWidth;
            var filledWidth = (int)Math.Round(progressWidth * usagePercentage);

            // Display drive info.
            WriteLine($"{drive.Name} [{new string('=', filledWidth).PadRight(progressWidth, '.')}] {infoText}");
        }
        return Task.FromResult(true);
    }
}