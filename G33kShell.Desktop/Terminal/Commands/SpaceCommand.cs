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

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DTC.Core.Extensions;
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
            var usagePercent = (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize;
            var s = $"[{drive.Name}] _ {usagePercent:P0} │ {drive.TotalSize.ToSize()}";
            s = s.Replace("_", usagePercent.ToProgressBar(state.CliPrompt.Width - s.Length));
            WriteLine(s);
        }
        return Task.FromResult(true);
    }
}