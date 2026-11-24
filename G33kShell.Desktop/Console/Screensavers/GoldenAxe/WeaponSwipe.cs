// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Attaches extracted weapon trails to the active portion of matching attack scripts.
/// </summary>
internal static class WeaponSwipe
{
    public const int SilverPalette = 0x16;
    public const int GoldPalette = 0x40;

    public static (int Frame, int Palette, double Dx, double Dy, bool Flip)? Sample(Fighter actor, ArcadeData data)
    {
        if (actor.State != FighterState.Attacking || actor.Mount != null ||
            !data.Attacks.TryGetValue(actor.Animation.Address, out var attack))
            return null;

        var move = Array.IndexOf(actor.Moves.Attacks, actor.Animation.Address);
        // These groups follow the corresponding weapon poses in the atlas. Kicks, stabs,
        // mounted attacks and charges have no matching sweep and must not acquire one.
        ReadOnlySpan<int> frames = (actor.Kind, actor.Moves.Idle, move) switch
        {
            (ActorKind.Hero, 0xdfea, 0) => [73, 74, 75, 76],
            (ActorKind.Hero, 0xdfea, 1) => [78, 79, 80, 81],
            (ActorKind.Hero, 0xdfea, 2) => [83, 84, 85, 86],
            (ActorKind.Hero, 0x1192a, 0) => [367, 368, 369, 370],
            (ActorKind.Hero, 0x1192a, 1) => [371, 372, 373, 374],
            (ActorKind.Hero, 0x150ac, 0) => [948, 949, 950],
            (ActorKind.Hero, 0x150ac, 1) => [951, 952, 953, 954],
            (ActorKind.Hero, 0x150ac, 2) => [955, 956, 957],
            (ActorKind.Heninger, _, 0) => [166, 167, 168, 169, 170],
            (ActorKind.Heninger, _, 1) => [171, 172, 173, 174, 175],
            (ActorKind.Heninger, _, 2) => [176, 177, 178, 179],
            (ActorKind.Skeleton, _, 0) => [858, 859, 860, 861],
            (ActorKind.Skeleton, _, 1) => [862, 863, 864, 865, 866],
            (ActorKind.Skeleton, _, 2) => [867, 868, 869, 870, 871],
            _ => []
        };
        if (frames.IsEmpty)
            return null;

        var animation = actor.Animation;
        var elapsed = animation.StepTicks;
        for (var i = attack[2]; i < animation.Index; i++)
            elapsed += animation.Steps[i].Duration;
        for (var i = animation.Index; i < attack[2]; i++)
            elapsed -= animation.Steps[i].Duration;
        var trailTick = elapsed + 2;
        if (trailTick < 0 || trailTick >= frames.Length * 2)
            return null;

        var index = frames[trailTick / 2];
        var frame = data.Frames[index];
        var step = animation.Current;
        var flip = actor.Facing > 0;
        // Align the trail's center with the weapon's strike box and mirror the whole attachment.
        var dx = (step.Dx + (attack[3] + attack[4]) / 2.0) * actor.Facing -
            (frame.Ox + frame.Width / 2.0) * (flip ? -1 : 1);
        var dy = step.Dy + (attack[5] + attack[6]) / 2.0 - (frame.Oy + frame.Height / 2.0);
        var palette = actor.Kind == ActorKind.Hero && actor.Moves.Idle == 0x150ac ? GoldPalette : SilverPalette;
        // Attachment timing and palettes are approximations pending arcade comparison.
        return (index, palette, dx, dy, flip);
    }
}
