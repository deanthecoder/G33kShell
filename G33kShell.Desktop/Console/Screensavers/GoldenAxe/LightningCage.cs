// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Collections.Generic;
using System.Linq;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Reuses the recorded dwarf lightning cage at each spell victim's world anchor.
/// </summary>
internal static class LightningCage
{
    public static bool IsSprite(int[] effect) => effect.Length == 6 && effect[0] is >= 569 and <= 574;

    // The two- and three-potion spell has no cage around the caster in the arcade.
    public static IEnumerable<Fighter> Targets(Game game) =>
        game.MagicLevel == 2 ? game.MagicTargets : game.MagicTargets.Prepend(game.Hero);

    public static int SampleTick(Game game)
    {
        if (game.Character.MagicKey != "gilius" || game.MagicTick < game.MagicShakeStart)
            return -1;
        var source = game.MagicEffects[0];
        // Preserve the original sequence, fitting its post-potion portion to the selected spell.
        const int start = 40;
        return Math.Clamp(start + (game.MagicTick - game.MagicShakeStart) * (source.Length - start - 1) /
            Math.Max(1, game.MagicDuration - game.MagicShakeStart - 1), start, source.Length - 1);
    }
}
