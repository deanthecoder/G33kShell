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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using G33kShell.Desktop.Console;
using G33kShell.Desktop.Skins;
using G33kShell.Desktop.Terminal.Controls;
using NUnit.Framework;

namespace G33kShell.Desktop.Terminal.Commands;

[TestFixture]
public class FindCommandTests
{
    private DirectoryInfo m_testDirectory;

    [SetUp]
    public void SetUp()
    {
        m_testDirectory = Directory.CreateDirectory(Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"find-command-{Guid.NewGuid():N}"));
    }

    [TearDown]
    public void TearDown() => m_testDirectory.Delete(true);

    [Test]
    public async Task ResultsAreSortedByFullPath()
    {
        File.WriteAllText(Path.Combine(m_testDirectory.FullName, "zeta.cs"), string.Empty);
        var nestedDirectory = m_testDirectory.CreateSubdirectory("nested");
        File.WriteAllText(Path.Combine(nestedDirectory.FullName, "gamma.cs"), string.Empty);
        File.WriteAllText(Path.Combine(m_testDirectory.FullName, "alpha.cs"), string.Empty);

        var state = CreateState();
        var command = new TestFindCommand { FileMask = "*.cs" };
        command.SetState(state);

        Assert.That(await command.RunCommand(state), Is.True);

        var results = state.CliPrompt.TextWithoutPrefix
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var expected = new[]
            {
                Path.Combine(m_testDirectory.FullName, "zeta.cs"),
                Path.Combine(nestedDirectory.FullName, "gamma.cs"),
                Path.Combine(m_testDirectory.FullName, "alpha.cs")
            }
            .OrderBy(o => o, StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o, StringComparer.Ordinal);
        Assert.That(results, Is.EqualTo(expected));
    }

    private TestState CreateState()
    {
        var state = new TestState { CurrentDirectory = m_testDirectory };
        var manager = new WindowManager(120, 25, new RetroMonoDos());
        manager.Root.AddChild(state.CliPrompt);
        return state;
    }

    private sealed class TestFindCommand : FindCommand
    {
        public Task<bool> RunCommand(ITerminalState state) => Run(state);
    }

    private sealed class TestState : ITerminalState
    {
        public DirectoryInfo CurrentDirectory { get; set; }
        public CommandHistory CommandHistory { get; } = new CommandHistory();
        public CliPrompt CliPrompt { get; } = new CliPrompt(120);
        public string LastRunScreensaverName { get; set; }
        public Stack<DirectoryInfo> DirStack { get; } = new Stack<DirectoryInfo>();
        public void LoadSkin(SkinBase skin) { }
        public void LoadScreensaver(string screensaverName, string extendedName) { }
    }
}
