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
using System.Reflection;
using CSharp.Core.Extensions;
using CSharp.Core.Settings;

namespace G33kShell.Desktop.Terminal;

public class AppSettings : UserSettingsBase
{
    public static AppSettings Instance { get; } = new AppSettings();

    public List<string> UsedCommands
    {
        get => Get<List<string>>();
        set => Set(value);
    }

    public DirectoryInfo Cwd
    {
        get => Get<DirectoryInfo>();
        set => Set(value);
    }

    protected override void ApplyDefaults()
    {
        UsedCommands = new List<string>();
        Cwd = Assembly.GetExecutingAssembly().GetDirectory();
    }
}