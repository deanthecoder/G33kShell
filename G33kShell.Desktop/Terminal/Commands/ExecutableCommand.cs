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
using CliWrap;
using CliWrap.Buffered;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Terminal.Commands;


public class ExecutableCommand : CommandBase
{
    private readonly string[] m_args;

    public ExecutableCommand([NotNull] string[] args)
    {
        m_args = args ?? throw new ArgumentNullException(nameof(args));
    }

    public bool CanExecute()
    {
        var command = m_args.FirstOrDefault();
        return !string.IsNullOrWhiteSpace(command) && WhereIsCommand.FindExecutables(command).Any();
    }

    public override async Task<bool> Run(ITerminalState state)
    {
        // todo - 'git --version' doesn't work multiple times.
        
        try
        {
            // Set up the command we want to run.
            var command = Cli.Wrap(m_args.First())
                .WithArguments(m_args.Skip(1))
                .WithValidation(CommandResultValidation.None)
                .WithWorkingDirectory(state.CurrentDirectory.FullName);

            // Execute the command and capture the output.
            var result = await command.ExecuteBufferedAsync();

            // Output the result to the console.
            foreach (var s in result.StandardOutput.Split('\n'))
                WriteLine(s);

            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                foreach (var s in result.StandardError.Split('\n'))
                    WriteLine(s);
            }

            return true;
        }
        catch (Exception ex)
        {
            WriteLine($"Failed to run command: {ex.Message}");
            return false;
        }
    }
}