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
using System;
using System.Linq;
using System.Reflection;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

public class LearningConfig
{
    public const int LearningGameCount = 4000;
    public const double DiscountFactor = 0.7;
    public const double Death = -1.0;
    public const double EatFood = 1.0;
    public const double AwayFood = -0.1;
    public const double TimePenaltyPerStep = -0.02;

    public static double ExplorationRate(int gamesPlayed, bool isTraining)
    {
        const double epsilonStart = 1.0;
        const double epsilonEnd = 0.01;

        if (!isTraining)
            return epsilonEnd * 0.1; // Keep a bit of training.
        
        var decayRate = Math.Pow(epsilonEnd / epsilonStart, 1.0 / LearningGameCount);
        return Math.Max(epsilonEnd, epsilonStart * Math.Pow(decayRate, gamesPlayed));
    }

    public override string ToString()
    {
        var allFields = GetType().GetFields(BindingFlags.Public |
                                            BindingFlags.NonPublic |
                                            BindingFlags.Static |
                                            BindingFlags.Instance);
        return string.Join(",", allFields.Select(f => $"{f.Name}:{f.GetValue(null)}"));
    }
}