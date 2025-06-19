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
using System.Threading.Tasks;
using DTC.Core.Extensions;
using G33kShell.Desktop.Terminal.Attributes;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription("Creates a directory, including parent directories if needed.", "Example: mkdir folder")]
public class MkdirCommand : CommandBase
{
    [PositionalArgument(ArgumentFlags.Required, Description = "Directory to create.")]
    public string Directory { get; [UsedImplicitly] set; } = string.Empty;

    protected override Task<bool> Run(ITerminalState state)
    {
        try
        {
            state.CurrentDirectory.Resolve(Directory, out var directoryInfo, out _, out _);
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
                Settings.Instance.AppendPathToHistory(directoryInfo.FullName);
            }
            else
            {
                WriteLine("Error: Directory already exists.");
            }
        }
        catch (Exception ex)
        {
            WriteLine($"Error: Unable to create directory ({ex.Message})");
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
}