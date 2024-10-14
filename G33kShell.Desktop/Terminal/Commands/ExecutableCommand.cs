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
using CliWrap;
using CliWrap.Buffered;
using JetBrains.Annotations;
using PeNet;

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
        try
        {
            // Set up the command we want to run.
            var command = Cli.Wrap(m_args.First())
                .WithArguments(m_args.Skip(1))
                .WithValidation(CommandResultValidation.None)
                .WithWorkingDirectory(state.CurrentDirectory.FullName);

            if (IsGuiApp(m_args.First()))
            {
                // Don't capture output on GUI apps - Launch and forget.
                command.ExecuteAsync();
                return true;
            }
            
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

    private static bool IsGuiApp(string filePath)
    {
        // Check file extension to ensure it's an executable
        var extension = Path.GetExtension(filePath).ToLower();
        if (extension != ".exe" && extension != ".dll")
        {
            // Non-Windows executables
            return false;
        }

        // Use PeNet to read the PE headers and check if the subsystem is Windows GUI.
        var peFile = new PeFile(filePath);
        return peFile.ImageNtHeaders?.OptionalHeader.Subsystem == PeNet.Header.Pe.SubsystemType.WindowsGui;
    }
}