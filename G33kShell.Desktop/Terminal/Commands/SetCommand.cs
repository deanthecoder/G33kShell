// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
// purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using G33kShell.Desktop.Terminal.Attributes;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription(
    "Lists environment variables, optionally filtered by a case-insensitive prefix.",
    "Assignments affect this session and programs launched from it.",
    "Use -u to also save changes for the current Windows user (future processes).",
    "With -u, listings show saved user variables. Existing applications are unchanged.",
    "An empty value removes the variable. Quote values containing spaces.",
    "Examples:",
    "  set", "  set path", "  set foo=bar", "  set foo=",
    "  set -u foo=bar", "  set -u foo=", "  set foo=\"hello world\"")]
public class SetCommand : CommandBase
{
    [PositionalArgument(ArgumentFlags.Optional | ArgumentFlags.Multiple, Description = "Variable prefix or name=value; an empty value removes the variable.")]
    public string[] Expression { get; [UsedImplicitly] set; } = Array.Empty<string>();

    [NamedArgument(ShortName = "u", Description = "Use saved Windows user variables; changes also apply to this session.")]
    public bool User { get; [UsedImplicitly] set; }

    protected override Task<bool> Run(ITerminalState state)
    {
        try
        {
            if (User && !OperatingSystem.IsWindows())
            {
                WriteLine("Error: Saved user environment variables are only supported on Windows.");
                return Task.FromResult(false);
            }

            var expression = string.Join(" ", Expression);
            var separator = expression.IndexOf('=');
            var target = User ? EnvironmentVariableTarget.User : EnvironmentVariableTarget.Process;
            if (separator < 0)
            {
                var variables = Environment.GetEnvironmentVariables(target).Cast<DictionaryEntry>()
                    .Where(entry => ((string)entry.Key).StartsWith(expression, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(entry => (string)entry.Key, StringComparer.OrdinalIgnoreCase);
                foreach (var entry in variables)
                    WriteLine($"{entry.Key}={entry.Value}");
            }
            else
            {
                var name = expression[..separator];
                if (string.IsNullOrWhiteSpace(name))
                {
                    WriteLine("Error: An environment variable name is required before '='.");
                    return Task.FromResult(false);
                }

                var value = expression[(separator + 1)..];
                Environment.SetEnvironmentVariable(name, value.Length == 0 ? null : value, target);
                if (User)
                    Environment.SetEnvironmentVariable(name, value.Length == 0 ? null : value, EnvironmentVariableTarget.Process);
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            WriteLine($"Error: {ex.Message}");
            return Task.FromResult(false);
        }
    }
}
