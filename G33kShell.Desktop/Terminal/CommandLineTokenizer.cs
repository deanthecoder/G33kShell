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

using System.Collections.Generic;
using System.Text;

namespace G33kShell.Desktop.Terminal;

public static class CommandLineTokenizer
{
    /// <summary>
    /// Splits arguments on unquoted whitespace, allowing quoted text anywhere in an argument.
    /// Backslashes remain literal so Windows paths do not require escaping.
    /// </summary>
    public static string[] Tokenize(string commandLine)
    {
        var arguments = new List<string>();
        var argument = new StringBuilder();
        var inQuotes = false;
        var hasArgument = false;
        foreach (var character in commandLine)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                hasArgument = true;
            }
            else if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (!hasArgument)
                    continue;
                arguments.Add(argument.ToString());
                argument.Clear();
                hasArgument = false;
            }
            else
            {
                argument.Append(character);
                hasArgument = true;
            }
        }

        if (hasArgument)
            arguments.Add(argument.ToString());
        return arguments.ToArray();
    }
}
