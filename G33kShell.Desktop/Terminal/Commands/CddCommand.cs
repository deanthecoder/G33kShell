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
using DTC.Core.Extensions;
using FuzzySharp;
using G33kShell.Desktop.Terminal.Attributes;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription(
    "Change directory using an intelligent look-up.",
    "",
    "Search all visited directories for a the most suitable location matching",
    "the command's argument.",
    "",
    "Example: cdd ProjectName")]
public class CddCommand : LocationCommand
{
    protected override Task<bool> Run(ITerminalState state)
    {
        var history = Settings.Instance.PathHistory;
        var candidates =
            history.Where(Directory.Exists)
                .Select(o => (o, GetScore(o, Path)))
                .OrderByDescending(o => o.Item2)
                .ToArray();
        
        // Check for a perfect match.
        var result = candidates.FirstOrDefault(o => System.IO.Path.GetFileName(o.o)?.Equals(Path, StringComparison.OrdinalIgnoreCase) == true);
        
        // ...failing that, find the best match.
        if (result.Item2 == 0.0)
            result = candidates.FirstOrDefault();
        
        if (result.Item2 > 0.7)
        {
            // Good enough to be a likely match - Use it.
            var newDir = result.o.ToDir();
            state.CurrentDirectory = newDir;
            Settings.Instance.AppendPathToHistory(newDir.FullName);
            return Task.FromResult(true);
        }
        
        // Not enough info - Report what matches _might_ exist.
        if (candidates.Any())
        {
            WriteLine("Candidates:");
            foreach ((string path, double score) tuple in candidates.Take(5))
                WriteLine($"  [{$"{tuple.score:P01}",5}] {tuple.path}");
            if (candidates.Length > 5)
                WriteLine("...");
        }
        
        return Task.FromResult(false);
    }

    private static double GetScore(string path, string subPath)
    {
        var lastSegment = path.ToDir().Name;
        return string.IsNullOrEmpty(lastSegment) ? 0.0 : Fuzz.PartialRatio(lastSegment.ToLower(), subPath.ToLower()) / 100.0;
    }
}