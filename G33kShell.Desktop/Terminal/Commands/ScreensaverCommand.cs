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

[CommandDescription(
    "Select and activate the screensaver.",
    "The screensaver can be activated by name, or by using the -l option to list available options.",
    "The name can have an optional suffix, which will be passed into the screensaver.",
    "",
    "Examples:",
    "  screensaver snake",
    "  screensaver snake_train")]
public class ScreensaverCommand : CommandBase
{
    [NamedArgument(Description = "List the available options.", ShortName = "l")]
    public bool List { get; [UsedImplicitly] set; }

    [PositionalArgument(ArgumentFlags.Optional, Description = "The name of the screensaver to apply.")]
    public string Name { get; [UsedImplicitly] set; }

    protected override Task<bool> Run(ITerminalState state)
    {
        var availableNames = ScreensaverControl.GetAvailableNames();
        var availableScreensavers = ScreensaverControl.GetAvailableScreensaverInfo();

        if (List)
        {
            WriteLine("Available options:");
            WriteLine(string.Empty);
            WriteTwoColumnTable(
                availableScreensavers.Where(o => !o.IsAi).Select(o => o.Name).ToArray(),
                availableScreensavers.Where(o => o.IsAi).Select(o => o.Name).ToArray());
        }

        if (!string.IsNullOrEmpty(Name))
        {
            var nameParts = Name.Split('_');
            var selectedScreensaver = availableNames.FirstOrDefault(o => o.Equals(nameParts[0], StringComparison.OrdinalIgnoreCase));
            if (selectedScreensaver == null)
            {
                WriteLine($"Option '{Name}' not found.");
                return Task.FromResult(false);
            }
            
            state.LoadScreensaver(selectedScreensaver, Name);
            return Task.FromResult(true);
        }

        if (!List)
            WriteLine($"Last seen: {state.LastRunScreensaverName ?? "None yet"}");

        return Task.FromResult(true);
    }

    private void WriteTwoColumnTable(string[] classicNames, string[] aiNames)
    {
        var leftHeader = "Classic";
        var rightHeader = "AI";
        var leftWidth = Math.Max(leftHeader.Length, classicNames.DefaultIfEmpty(string.Empty).Max(o => o.Length));
        var rightWidth = Math.Max(rightHeader.Length, aiNames.DefaultIfEmpty(string.Empty).Max(o => o.Length));
        var rowCount = Math.Max(classicNames.Length, aiNames.Length);

        WriteLine($"  {leftHeader.PadRight(leftWidth)}   {rightHeader.PadRight(rightWidth)}");
        WriteLine($"  {new string('─', leftWidth)}   {new string('─', rightWidth)}");

        for (var i = 0; i < rowCount; i++)
        {
            var left = i < classicNames.Length ? classicNames[i] : string.Empty;
            var right = i < aiNames.Length ? aiNames[i] : string.Empty;
            WriteLine($"  {left.PadRight(leftWidth)}   {right.PadRight(rightWidth)}");
        }
    }
}
