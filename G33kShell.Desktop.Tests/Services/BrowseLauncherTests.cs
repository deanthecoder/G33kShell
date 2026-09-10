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

using System.IO;
using G33kShell.Desktop.Services;
using NUnit.Framework;

namespace G33kShell.Desktop.Tests.Services;

/// <summary>Checks how G33kShell locates and starts the optional Browse application.</summary>
/// <remarks>Ensures Browse receives paths safely while unavailable installations continue to use the OS explorer.</remarks>
[TestFixture]
public class BrowseLauncherTests
{
    [Test]
    public void GetExecutablePathFindsBrowseInProgramFiles()
    {
        var programFiles = @"C:\Program Files";
        var expectedPath = Path.Combine(programFiles, "Browse", "Browse.exe");

        var result = BrowseLauncher.GetExecutablePath(programFiles, path => path == expectedPath);

        Assert.That(result, Is.EqualTo(expectedPath));
    }

    [Test]
    public void GetExecutablePathReturnsNullWhenBrowseIsNotInstalled()
    {
        var result = BrowseLauncher.GetExecutablePath(@"C:\Program Files", _ => false);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void CreateStartInfoPassesTheFullPathAsOneArgument()
    {
        var startInfo = BrowseLauncher.CreateStartInfo(@"C:\Program Files\Browse\Browse.exe", @"C:\Folder With Spaces\file.txt");

        Assert.That(startInfo.FileName, Is.EqualTo(@"C:\Program Files\Browse\Browse.exe"));
        Assert.That(startInfo.UseShellExecute, Is.True);
        Assert.That(startInfo.ArgumentList, Is.EquivalentTo(new[] { @"C:\Folder With Spaces\file.txt" }));
    }
}
