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
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using G33kShell.Desktop.Console;
using G33kShell.Desktop.Skins;
using G33kShell.Desktop.Terminal.Controls;
using NClap;
using NUnit.Framework;

namespace G33kShell.Desktop.Terminal.Commands;

[TestFixture]
[NonParallelizable]
public class SetCommandTests
{
    private string m_name;

    [SetUp]
    public void SetUp() => m_name = "G33KSHELL_SET_TEST_" + Guid.NewGuid().ToString("N");

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(m_name, null);
        Environment.SetEnvironmentVariable(m_name + "_OTHER", null);
        if (OperatingSystem.IsWindows())
            Environment.SetEnvironmentVariable(m_name, null, EnvironmentVariableTarget.User);
    }

    [TestCase("bar")]
    [TestCase("one=two=three")]
    [TestCase("hello world")]
    [TestCase(" leading and trailing ")]
    public async Task AssignmentPreservesValue(string value)
    {
        var result = await Run($"set {m_name}=\"{value}\"");
        Assert.That(result.Success, Is.True);
        Assert.That(Environment.GetEnvironmentVariable(m_name), Is.EqualTo(value));
        Assert.That(Environment.GetEnvironmentVariable(m_name, EnvironmentVariableTarget.User), Is.Null);
    }

    [Test]
    public async Task UnquotedWordsFormOneValue()
    {
        Assert.That((await Run($"set {m_name}=hello world")).Success, Is.True);
        Assert.That(Environment.GetEnvironmentVariable(m_name), Is.EqualTo("hello world"));
    }

    [TestCase("")]
    [TestCase("\"\"")]
    public async Task EmptyValueRemovesVariable(string value)
    {
        Environment.SetEnvironmentVariable(m_name, "old");
        Assert.That((await Run($"set {m_name}={value}")).Success, Is.True);
        Assert.That(Environment.GetEnvironmentVariable(m_name), Is.Null);
    }

    [Test]
    public async Task ListsAllAndFiltersPrefixIgnoringCase()
    {
        Environment.SetEnvironmentVariable(m_name, "first");
        Environment.SetEnvironmentVariable(m_name + "_OTHER", "second");
        Assert.That((await Run("set")).Output, Does.Contain(m_name + "=first"));
        var filtered = await Run("set " + m_name.ToLowerInvariant());
        Assert.That(filtered.Output.ReplaceLineEndings("\n").Trim(),
            Is.EqualTo($"{m_name}=first\n{m_name}_OTHER=second"));
        Assert.That((await Run("set " + m_name + "_MISSING")).Output.Trim(), Is.Empty);
    }

    [Test]
    public async Task MissingNameReportsError()
    {
        var result = await Run("set =value");
        Assert.That(result.Success, Is.False);
        Assert.That(result.Output, Does.Contain("name is required"));
    }

    [Test]
    [Platform("Win")]
    public async Task UserSwitchPersistsUpdatesAndRemovesBothScopes()
    {
        Assert.That((await Run($"set -u {m_name}=saved")).Success, Is.True);
        Assert.That(Environment.GetEnvironmentVariable(m_name), Is.EqualTo("saved"));
        Assert.That(Environment.GetEnvironmentVariable(m_name, EnvironmentVariableTarget.User), Is.EqualTo("saved"));
        Environment.SetEnvironmentVariable(m_name, "session");
        Assert.That((await Run($"set -u {m_name}")).Output, Does.Contain(m_name + "=saved"));
        Assert.That((await Run($"set -u")).Output, Does.Contain(m_name + "=saved"));
        Assert.That((await Run($"set -u {m_name}=")).Success, Is.True);
        Assert.That(Environment.GetEnvironmentVariable(m_name), Is.Null);
        Assert.That(Environment.GetEnvironmentVariable(m_name, EnvironmentVariableTarget.User), Is.Null);
    }

    private static async Task<(bool Success, string Output)> Run(string line)
    {
        Assert.That(CommandLineParser.TryParse(CommandLineTokenizer.Tokenize(line), out ProgramArguments parsed), Is.True, line);
        Assert.That(parsed.PrimaryCommand.InstantiatedCommand, Is.TypeOf<SetCommand>());
        var parsedCommand = (SetCommand)parsed.PrimaryCommand.InstantiatedCommand;
        var command = new TestSetCommand { Expression = parsedCommand.Expression, User = parsedCommand.User };
        var state = new TestState();
        var manager = new WindowManager(240, 25, new RetroMonoDos());
        manager.Root.AddChild(state.CliPrompt);
        command.SetState(state);
        var success = await command.RunCommand(state);
        return (success, state.CliPrompt.TextWithoutPrefix);
    }

    private sealed class TestSetCommand : SetCommand
    {
        public Task<bool> RunCommand(ITerminalState state) => Run(state);
    }

    private sealed class TestState : ITerminalState
    {
        public DirectoryInfo CurrentDirectory { get; set; }
        public CommandHistory CommandHistory { get; } = new CommandHistory();
        public CliPrompt CliPrompt { get; } = new CliPrompt(240);
        public string LastRunScreensaverName { get; set; }
        public Stack<DirectoryInfo> DirStack { get; } = new Stack<DirectoryInfo>();
        public void LoadSkin(SkinBase skin) { }
        public void LoadScreensaver(string screensaverName, string extendedName) { }
    }
}
