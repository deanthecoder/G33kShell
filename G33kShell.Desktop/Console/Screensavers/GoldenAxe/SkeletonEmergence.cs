// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Combines the original cracked-earth frames with the extracted skeleton rise animation.
/// </summary>
internal static class SkeletonEmergence
{
    public const int MoundFrame = 853;
    public const int OpenFrame = 857;
    public const int Animation = 0x24a00;
    public const int GroundPalette = 0x74;
    // MAME: crack and skeleton advance together, entering normal AI after six poses.
    public const int Duration = 48;

    public static int GroundFrame(int tick) => MoundFrame + System.Math.Min(4, tick / 8);
}
