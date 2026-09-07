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
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using G33kShell.Desktop.Console;
using G33kShell.Desktop.Skins;
using G33kShell.Desktop.Terminal.Controls;
using NUnit.Framework;

namespace G33kShell.Desktop.Terminal.Commands;

/// <summary>Checks manual lookup results for known and unknown commands.</summary>
/// <remarks>Prevents an unknown name from silently selecting the first command.</remarks>
[TestFixture]
public class ManCommandTests
{
    [TestCase("sleep")]
    [TestCase("nonexistent-command")]
    public async Task UnknownCommandReportsAnError(string name)
    {
        var state = CreateState();
        var command = new TestManCommand { CommandName = name };
        command.SetState(state);

        Assert.That(await command.RunCommand(state), Is.False);
        Assert.That(state.CliPrompt.TextWithoutPrefix.Trim(), Is.EqualTo("Error: Command not found."));
    }

    [TestCase("cat")]
    [TestCase("type")]
    [TestCase("CAT")]
    public async Task KnownCommandDisplaysItsManual(string name)
    {
        var state = CreateState();
        var command = new TestManCommand { CommandName = name };
        command.SetState(state);

        Assert.That(await command.RunCommand(state), Is.True);
        Assert.That(state.CliPrompt.TextWithoutPrefix, Does.Contain("cat"));
        Assert.That(state.CliPrompt.TextWithoutPrefix, Does.Not.Contain("Error:"));
    }

    private static TestState CreateState()
    {
        var state = new TestState();
        var manager = new WindowManager(80, 25, new RetroMonoDos());
        manager.Root.AddChild(state.CliPrompt);
        return state;
    }

    private sealed class TestManCommand : ManCommand
    {
        public Task<bool> RunCommand(ITerminalState state) => Run(state);
    }

    private sealed class TestState : ITerminalState
    {
        public DirectoryInfo CurrentDirectory { get; set; }
        public CommandHistory CommandHistory { get; } = new CommandHistory();
        public CliPrompt CliPrompt { get; } = new CliPrompt(80);
        public string LastRunScreensaverName { get; set; }
        public Stack<DirectoryInfo> DirStack { get; } = new Stack<DirectoryInfo>();
        public void LoadSkin(SkinBase skin) { }
        public void LoadScreensaver(string screensaverName, string extendedName) { }
    }
}
