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

using NUnit.Framework;

namespace G33kShell.Desktop.Terminal;

[TestFixture]
public class CommandLineTokenizerTests
{
    [TestCase("set foo=\"hello world\"", new[] { "set", "foo=hello world" })]
    [TestCase("set \"foo=hello world\"", new[] { "set", "foo=hello world" })]
    [TestCase("set foo=\"  hello   world  \"", new[] { "set", "foo=  hello   world  " })]
    [TestCase("set foo=\"\"", new[] { "set", "foo=" })]
    [TestCase("grep file.txt \"\"", new[] { "grep", "file.txt", "" })]
    [TestCase("  set\tfoo=bar  ", new[] { "set", "foo=bar" })]
    [TestCase("set foo=\"hello world\"suffix", new[] { "set", "foo=hello worldsuffix" })]
    [TestCase("set foo=\"one\"=\"two words\"", new[] { "set", "foo=one=two words" })]
    [TestCase("copy \"C:\\some folder\\\" \"D:\\other folder\\\"", new[] { "copy", "C:\\some folder\\", "D:\\other folder\\" })]
    [TestCase("set -u foo=\"hello world\"", new[] { "set", "-u", "foo=hello world" })]
    [TestCase("cat file.txt", new[] { "cat", "file.txt" })]
    public void PreservesArgumentBoundariesAndQuotedContent(string commandLine, string[] expected) =>
        Assert.That(CommandLineTokenizer.Tokenize(commandLine), Is.EqualTo(expected));

    [TestCase("")]
    [TestCase(" \t ")]
    public void BlankInputHasNoArguments(string commandLine) =>
        Assert.That(CommandLineTokenizer.Tokenize(commandLine), Is.Empty);
}
