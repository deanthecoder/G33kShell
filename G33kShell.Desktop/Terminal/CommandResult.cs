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

namespace G33kShell.Desktop.Terminal;

/// <summary>
/// Represents the result of a command execution, including information about the directory, command, output, and success status.
/// </summary>
/// <remarks>
/// This class is used to track the results of executed commands in a terminal application.
/// </remarks>
public record CommandResult(
    DirectoryInfo Directory,
    string Command,
    string Output,
    bool IsSuccess
);