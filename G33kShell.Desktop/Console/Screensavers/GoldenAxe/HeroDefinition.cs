// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// If you modify the code, please retain this copyright header.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.
namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Groups the character-specific ROM scripts and display data for each playable hero.
/// </summary>
/// <remarks>
/// Character selection changes gameplay as well as artwork, including mounted animations.
/// </remarks>
internal sealed record HeroDefinition(string Name, string MagicKey, int Palette, int Portrait, int MagicLevels,
    Moves Moves, Moves MountedMoves, int Magic, int Knee, int Throw, int Back, int Dash)
{
    public int Sleep => MagicKey switch { "ax" => 0xe7dc, "tyris" => 0x1209a, _ => 0x15896 };

    public Moves DragonMoves => MagicKey switch
    {
        "ax" => new Moves(0xfd70, 0xfd8a, 0xfdd4, 0xfe1e, [0xfe68], 0xff54, 0xff54, 0xfd70),
        "tyris" => new Moves(0x1362e, 0x13648, 0x13692, 0x136dc, [0x13726], 0x137a0, 0x137a0, 0x1362e),
        _ => new Moves(0x16dca, 0x16de4, 0x16e2e, 0x16e78, [0x16ec2], 0x16f3c, 0x16f3c, 0x16dca)
    };

    public Moves RedDragonMoves => MagicKey switch
    {
        "ax" => new Moves(0x10928, 0x10942, 0x1098c, 0x109d6, [0x10a20], 0x10b0c, 0x10b0c, 0x10928),
        "tyris" => new Moves(0x14174, 0x1418e, 0x141d8, 0x14222, [0x1426c], 0x142e6, 0x142e6, 0x14174),
        _ => new Moves(0x17900, 0x1791a, 0x17964, 0x179ae, [0x179f8], 0x17a72, 0x17a72, 0x17900)
    };

    public Moves RidingMoves(ActorKind mount) => mount switch
    {
        ActorKind.BlueDragon => DragonMoves,
        ActorKind.RedDragon => RedDragonMoves,
        _ => MountedMoves
    };

    public static readonly HeroDefinition[] All =
    [
        new("AX BATTLER", "ax", 0x13, 0x1c5, 4, Moves.Ax, Moves.AxChicken, 0xe002, 0xe2d8, 0xe482, 0xe4d6, 0xe580),
        new("TYRIS FLARE", "tyris", 0x14, 0x1cd, 6,
            new Moves(0x1192a, 0x11942, 0x11996, 0x119ea, [0x11a3e, 0x11b62, 0x11c80, 0x11ce0], 0x11efe, 0x11fbe, 0x120ca, 0x11e62, 0x11eaa),
            new Moves(0x129e8, 0x12a02, 0x12a96, 0x12b2a, [0x12b74], 0x12bee, 0x12bee, 0x129e8),
            0x118ee, 0x11bcc, 0x11d4c, 0x11da0, 0x11e4a),
        new("GILIUS THUNDERHEAD", "gilius", 0x15, 0x1d5, 3,
            new Moves(0x150ac, 0x15100, 0x15154, 0x151a8, [0x151fc, 0x15300, 0x1542e, 0x154b8], 0x156ee, 0x157ba, 0x158c6, 0x15644, 0x1568c),
            new Moves(0x161e4, 0x161fe, 0x16292, 0x16326, [0x16370], 0x163ea, 0x163ea, 0x161e4),
            0x150c4, 0x1537a, 0x15524, 0x15578, 0x1562c)
    ];
}
