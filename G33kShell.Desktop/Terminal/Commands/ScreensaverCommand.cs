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
using System.Linq;
using System.Threading.Tasks;
using G33kShell.Desktop.Terminal.Attributes;
using G33kShell.Desktop.Terminal.Controls;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription("Select and activate the screensaver.")]
public class ScreensaverCommand : CommandBase
{
    [NamedArgument(Description = "List the available options.", LongName = "list", ShortName = "l")]
    public bool List { get; [UsedImplicitly] set; }

    [PositionalArgument(ArgumentFlags.Optional, Description = "The name of the screensaver to apply.")]
    public string Name { get; [UsedImplicitly] set; }

    protected override Task<bool> Run(ITerminalState state)
    {
        var availableNames = ScreensaverControl.GetAvailableNames();

        if (List)
        {
            WriteLine("Available options:");
            foreach (var name in availableNames)
                WriteLine($"  {name}");
        }

        if (!string.IsNullOrEmpty(Name))
        {
            var selectedScreensaver = availableNames.FirstOrDefault(o => o.Equals(Name, StringComparison.OrdinalIgnoreCase));
            if (selectedScreensaver == null)
            {
                WriteLine($"Option '{Name}' not found.");
                return Task.FromResult(false);
            }
            
            state.LoadScreensaver(selectedScreensaver);
        }

        return Task.FromResult(true);
    }
}