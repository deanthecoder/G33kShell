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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace G33kShell.Desktop.Terminal.Commands;

public class ShutdownCommand : CommandBase
{
    protected override bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    protected override Task<bool> Run(ITerminalState state)
    {
        const int delayInSeconds = 2;

        var success = Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown", Arguments = $"/s /f /t {delayInSeconds}", CreateNoWindow = true, UseShellExecute = false
        }) != null;
        return Task.FromResult(success);
    }
}