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
using G33kShell.Desktop.Skins;
using G33kShell.Desktop.Terminal.Attributes;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription("List or change the current Terminal skin.")]
public class SkinCommand : CommandBase
{
    [NamedArgument(Description = "List the available options.", LongName = "list", ShortName = "l")]
    public bool List { get; [UsedImplicitly] set; }

    [PositionalArgument(ArgumentFlags.Optional, Description = "The name of the skin to apply.")]
    public string Name { get; [UsedImplicitly] set; }

    protected override Task<bool> Run(ITerminalState state)
    {
        var skinObjects = CreateSkins();

        if (List)
        {
            WriteLine("Available options:");
            foreach (var skinObject in skinObjects.OrderBy(o => o.Name))
                WriteLine($"  {skinObject.Name}");
        }

        if (!string.IsNullOrEmpty(Name))
        {
            var selectedSkin = skinObjects.FirstOrDefault(o => o.Name.Equals(Name, StringComparison.OrdinalIgnoreCase));
            if (selectedSkin == null)
            {
                WriteLine($"Option '{Name}' not found.");
                return Task.FromResult(false);
            }

            state.LoadSkin(selectedSkin);
            Settings.Instance.SkinName = selectedSkin.Name;
        }

        return Task.FromResult(true);
    }

    public static SkinBase[] CreateSkins()
    {
        var skinTypes = typeof(RetroPlasma).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(SkinBase).IsAssignableFrom(t));
        return skinTypes.Select(o => (SkinBase)Activator.CreateInstance(o)).ToArray();
    }
}