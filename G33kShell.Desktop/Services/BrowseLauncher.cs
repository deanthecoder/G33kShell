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
using System.Diagnostics;
using System.IO;

namespace G33kShell.Desktop.Services;

/// <summary>
/// Opens paths in Browse when it is installed on Windows.
/// </summary>
/// <remarks>
/// Browse is an optional companion app, so callers can retain their usual file-explorer behavior when it is unavailable.
/// </remarks>
internal static class BrowseLauncher
{
    private const string BrowseExecutableName = "Browse.exe";

    public static bool TryLaunch(string path)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        var executablePath = GetExecutablePath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), File.Exists);
        if (executablePath == null)
            return false;

        try
        {
            Process.Start(CreateStartInfo(executablePath, path));
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static string GetExecutablePath(string programFilesPath, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(programFilesPath))
            return null;

        var executablePath = Path.Combine(programFilesPath, "Browse", BrowseExecutableName);
        return fileExists(executablePath) ? executablePath : null;
    }

    internal static ProcessStartInfo CreateStartInfo(string executablePath, string path)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add(path);
        return startInfo;
    }
}
