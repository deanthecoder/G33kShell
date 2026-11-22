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
/// Animation roles identified in the set-6 ROM. These are data addresses, not executable code.
/// </summary>
/// <remarks>
/// Role mappings select extracted scripts without executing the original program.
/// </remarks>
internal sealed record Moves(int Idle, int Walk, int WalkUp, int Run, int[] Attacks, int Hurt, int Fly, int GetUp,
    int Jump = 0, int JumpAttack = 0, int Dash = 0)
{
    public double WalkSpeed { get; init; } = 0.7;
    public double RunSpeed { get; init; } = 2.75;
    public int FollowUpHurt { get; init; }
    public int Collapse => Idle switch
    {
        0x199e0 => 0x19f82,
        0x1d522 => 0x1db04,
        0x1e8fc => 0x1eede,
        0x224ba => 0x228a2,
        0x28daa => 0x2920e,
        0x35c58 => 0x35f98,
        0x1c140 => 0x1c6fa,
        0x210fa => 0x216f4,
        _ => Fly
    };

    public static readonly Moves Ax = new(0xdfea, 0xe03e, 0xe092, 0xe0e6, [0xe13a, 0xe25e, 0xe38c, 0xe416], 0xe634, 0xe700, 0xe80c, 0xe598, 0xe5e0);
    public static readonly Moves Heninger = new(0x199e0, 0x19a04, 0x19aac, 0x19b54, [0x19bfc, 0x19d20, 0x19d9a], 0x19efe, 0x19fca, 0x1a02a, Dash: 0x19e24)
        { WalkSpeed = 0.5, FollowUpHurt = 0x19f2e };
    public static readonly Moves Longmoan = new(0x1d522, 0x1d546, 0x1d5ee, 0x1d696, [0x1d73e, 0x1d8a2, 0x1d90c], 0x1da80, 0x1db4c, 0x1dbac, JumpAttack: 0x1da06)
        { WalkSpeed = 0.5, RunSpeed = 3, FollowUpHurt = 0x1dab0 };
    public static readonly Moves Brother = new(0x28daa, 0x28dce, 0x28e46, 0x28ebe, [0x28f66, 0x28fc6, 0x29090], 0x291ba, 0x29256, 0x292b6);
    public static readonly Moves Thief = new(0x1873a, 0x1876a, 0x1876a, 0x1876a, [], 0x189aa, 0x189aa, 0x18a3a);
    public const int ThiefEmptyRun = 0x187d6;
    public const int ThiefEmptyHurt = 0x189f2;
    public static readonly Moves Chicken = new(0x2c244, 0x2beee, 0x2beee, 0x2beee, [], 0x2be1e, 0x2be1e, 0x2be1e);
    public static readonly Moves AxChicken = new(0xf12a, 0xf144, 0xf1d8, 0xf26c, [0xf2b6], 0xf330, 0xf330, 0xf12a, 0xf35a);
    public static readonly Moves EnemyChicken = new(0x2bd70, 0x2bd8a, 0x2be5a, 0x2bf74, [0x2bffa], 0x2c0d4, 0x2c0d4, 0x2bd70);
    public static readonly Moves Amazon = new(0x224ba, 0x224de, 0x22586, 0x2262e, [0x226d6, 0x22760], 0x2282a, 0x228ea, 0x2294a, 0x227e2, Dash: 0x227ca);
    public static readonly Moves FastLongmoan = new(0x1e8fc, 0x1e920, 0x1e9c8, 0x1ea70, [0x1eb18, 0x1ec7c, 0x1ece6], 0x1ee5a, 0x1ef26, 0x1ef86, 0x1ed98, 0x1ede0)
        { RunSpeed = 3 };
    public static readonly Moves BlueDragon = new(0x2f5e4, 0x2f29c, 0x2f29c, 0x2f29c, [], 0x2f1cc, 0x2f1cc, 0x2f1cc);
    public static readonly Moves AmazonDragon = new(0x301d0, 0x301ea, 0x302ba, 0x303d4, [0x3045a], 0x3054c, 0x3054c, 0x301d0);
    public static readonly Moves AmazonChicken = new(0x2ce16, 0x2ce30, 0x2cf00, 0x2d01a, [0x2d0a0], 0x2d17a, 0x2d17a, 0x2ce16);
    public static readonly Moves LongmoanChicken = new(0x2dedc, 0x2def6, 0x2dfc6, 0x2e0e0, [0x2e166], 0x2e240, 0x2e240, 0x2dedc);
    public static readonly Moves HeningerDragon = new(0x2f11e, 0x2f138, 0x2f208, 0x2f322, [0x2f3a8], 0x2f49a, 0x2f49a, 0x2f11e);
    public static readonly Moves LongmoanDragon = new(0x312ae, 0x312c8, 0x31398, 0x314b2, [0x31538], 0x3162a, 0x3162a, 0x312ae);
    public static readonly Moves Skeleton = new(0x24a54, 0x24a6c, 0x24b14, 0x24bbc, [0x24c64, 0x24d68, 0x24eb6], 0x251ca, 0x2528a, 0x252ea, 0x2508e, 0x250d6);
    public static readonly Moves DeathAdder = new(0x35c58, 0x35c70, 0x35d18, 0x35dc0, [0x35e14], 0x35f20, 0x35fe0, 0x36028);
    public const int DeathAdderCast = 0x35e9e;
    public static readonly Moves Lightning = new(0x3618e, 0x3618e, 0x3618e, 0x3618e, [], 0x3618e, 0x3618e, 0x3618e);
    public static readonly Moves StrongHeninger = new(0x1c140, 0x1c17c, 0x1c224, 0x1c2cc, [0x1c374, 0x1c41e, 0x1c498], 0x1c676, 0x1c742, 0x1c7a2, 0x1c5b4, 0x1c5fc, 0x1c59c);
    public static readonly Moves StrongLongmoan = new(0x210fa, 0x21136, 0x211de, 0x21286, [0x2132e, 0x213f8, 0x21492], 0x21670, 0x2173c, 0x2179c, 0x215ae, 0x215f6);
    public static readonly Moves Knight = new(0x2ac72, 0x2ac96, 0x2ad3e, 0x2ac96, [0x2ade6, 0x2ae60], 0x2af62, 0x2afda, 0x2b022);
    public static readonly Moves HeningerRedDragon = new(0x32678, 0x32692, 0x32762, 0x3287c, [0x32918], 0x32992, 0x32992, 0x32678);
    public static readonly Moves AmazonRedDragon = new(0x33798, 0x337b2, 0x33882, 0x3399c, [0x33a38], 0x33ab2, 0x33ab2, 0x33798);
    public static readonly Moves LongmoanRedDragon = new(0x34818, 0x34832, 0x34902, 0x34a1c, [0x34ab8], 0x34b32, 0x34b32, 0x34818);
}
