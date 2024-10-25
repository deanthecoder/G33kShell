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

[CommandDescription("Copy files and directories.")]
public class CopyCommand : CommandBase
{
    [PositionalArgument(ArgumentFlags.Multiple, Description = "One or more pairs of source/destinations.")]
    [UsedImplicitly]
    public string[] Paths { get; set; }

    // [NamedArgument(Description = "Display in bare format.", ShortName = "b")]
    // [UsedImplicitly]
    // public bool BareFormat { get; set; }

    protected override async Task<bool> Run(ITerminalState state)
    {
        if ((Paths.Length & 1) != 0)
        {
            WriteLine("One or more pairs of source/destinations must be specified.");
            return false;
        }
        
        for (var i = 0; i < Paths.Length; i += 2)
        {
            var source = state.CurrentDirectory.Resolve(Paths[i]);
            var dest = state.CurrentDirectory.Resolve(Paths[i + 1]);

            // Copy directory to under another directory.
            var sourceDir = source.Contains('*') ? source.ToFile().Directory : source.ToDir();
            if (sourceDir == null)
                continue;
            
            if (sourceDir.Exists && !source.Contains('*'))
            {
                try
                {
                    await Task.Run(() => sourceDir.CopyTo(dest.ToDir()));
                    continue;
                }
                catch (Exception ex)
                {
                    WriteLine($"An error occurred: {ex.Message}");
                    return false;
                }
            }

            // Copy files to a directory?
            var sourceInfos = source.Contains('*') ? sourceDir.GetFileSystemInfos(source.ToFile().Name) : [source.ToFile()];
            if (sourceInfos.Length == 0)
                continue; // Nothing to do.
            var isTargetADir = dest.ToDir().Exists || dest.EndsWith('/') || dest.EndsWith('\\');
            if (isTargetADir)
            {
                foreach (var sourceFile in sourceInfos)
                    sourceFile.CopyTo(dest.ToDir());
                continue;
            }
            
            // Copy file(s) to a file. (I.e. Copy with new name)
            (sourceInfos.Last() as FileInfo)?.CopyTo(dest.ToFile());
        }
        
        return true;
    }
}