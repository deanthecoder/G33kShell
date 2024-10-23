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
using Avalonia;
using Avalonia.Controls;
using CSharp.Core.Extensions;

namespace G33kShell.Desktop.Terminal.Commands;

public class MaxCommand : CommandBase
{
    protected override Task<bool> Run(ITerminalState state)
    {
        var window = Application.Current.GetMainWindow();

        if (window.WindowState != WindowState.FullScreen)
        {
            window.WindowState = WindowState.FullScreen;
            window.SystemDecorations = SystemDecorations.None;
        }
        else
        {
            window.WindowState = WindowState.Normal;
            window.SystemDecorations = SystemDecorations.Full;
        }
        
        return Task.FromResult(true);
    }
}