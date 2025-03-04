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
using CSharp.Core;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Terminal.Attributes;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription(
    "Copy files and directories.",
    "If the source and target are directories the 'sync' option can be used to ensure the contents match.")]
public class CopyCommand : CommandBase
{
    [PositionalArgument(ArgumentFlags.Multiple, Description = "One or more pairs of source/destinations.")]
    [UsedImplicitly]
    public string[] Paths { get; set; }

    [NamedArgument(Description = "Sync the content of source and target directories.", ShortName = "s")]
    [UsedImplicitly]
    public bool Sync { get; set; }

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
            try
            {
                // Syncing two directories?
                if (Sync && source.ToDir().Exists)
                {
                    await Task.Run(() => RunSync(source, dest));
                    continue;
                }

                // Copy directory to under another directory.
                var sourceDir = source.Contains('*') ? source.ToFile().Directory : source.ToDir();
                if (sourceDir == null)
                    continue;

                if (sourceDir.Exists && !source.Contains('*'))
                {
                    await Task.Run(() => sourceDir.CopyTo(dest.ToDir()));
                    continue;
                }

                // Copy files to a directory?
                var sourceInfos = source.Contains('*') ? sourceDir.GetFileSystemInfos(source.ToFile().Name) : [source.ToFile()];
                if (sourceInfos.Length == 0)
                    continue; // Nothing to do.
                var isTargetADir = dest.ToDir().Exists || dest.EndsWith('/') || dest.EndsWith('\\');
                if (isTargetADir)
                {
                    foreach (var sourceFile in sourceInfos)
                        sourceFile.CopyTo(dest.ToDir(), true);
                    continue;
                }

                // Copy file(s) to a file. (I.e. Copy with new name)
                (sourceInfos.Last() as FileInfo)?.CopyTo(dest.ToFile(), true);
            }
            catch (Exception ex)
            {
                WriteLine($"An error occurred copying {Paths[i]} to {Paths[i + 1]}: {ex.Message}");
                Logger.Instance.Exception($"Failed to copy {Paths[i]} to {Paths[i + 1]}", ex);
                return false;
            }
        }

        return true;
    }

    private void RunSync(string source, string dest)
    {
        var sourceDir = source.ToDir();
        var destDir = dest.ToDir();

        if (!destDir.Exists)
            destDir.Create();

        // Normalizing the paths by making them relative.
        var allSources = sourceDir
            .EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
            .Select(s => new
            {
                RelativePath = Path.GetRelativePath(source, s.FullName), Info = s
            })
            .ToArray();

        var allTargets = destDir
            .EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
            .Select(t => new
            {
                RelativePath = Path.GetRelativePath(dest, t.FullName), Info = t
            })
            .ToArray();

        // Finding excess items in target by comparing relative paths
        var excessItems = allTargets
            .Where(target => !allSources.Any(src => src.RelativePath.Equals(target.RelativePath, StringComparison.OrdinalIgnoreCase)))
            .Select(t => t.Info)
            .ToArray();

        foreach (var excessItem in excessItems)
            excessItem.TryDelete();

        // Copy the content that does exist.
        var filesCopied = sourceDir
            .GetFileSystemInfos()
            .Sum(o => o.CopyTo(destDir, true));

        WriteLine($"Operation complete. {excessItems.Length} item(s) removed, {filesCopied} copied.");
    }
}