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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

public class LearningConfig
{
    private double m_explorationDecayRate = 0.999;

    public double LearningRate = 0.01;
    public double DiscountFactor = 0.9;
    public double ExplorationRate = 1.0;
    public double MinExplorationRate = 0.001;
    public double Death = -100.0;
    public double EatFood = 10.0;
    public double AwayFood = -0.1;
    public double TimePenaltyPerStep = -0.05;

    public void DecayExplorationRate() =>
        ExplorationRate = Math.Max(MinExplorationRate, ExplorationRate * m_explorationDecayRate);

    public static IEnumerable<LearningConfig> AllCombinations(LearningConfig baseConfig)
    {
        foreach (var _ in new[]{ 0.0 })
        {
            var config = baseConfig.Clone();
            //config.FoodBonusFactor = d;
            yield return config;
        }
    }
    
    public override string ToString()
    {
        var allFields =
            GetType().GetFields(BindingFlags.Public |
                                BindingFlags.NonPublic |
                                BindingFlags.Instance)
                .Where(o => !o.Name.Contains("ExplorationRate"))
                .ToArray();
        var allProperties = GetType().GetProperties();

        // Create value pairs
        var fieldValues = string.Join(",", allFields.Select(f => $"{f.Name}:{f.GetValue(this)}"));
        var propertyValues = string.Join(",", allProperties.Select(p => $"{p.Name}:{p.GetValue(this)}"));

        return $"{propertyValues}{(allProperties.Length > 0 && allFields.Length > 0 ? "," : string.Empty)}{fieldValues}";
    }

    public LearningConfig Clone() =>
        new LearningConfig
        {
            LearningRate = LearningRate,
            DiscountFactor = DiscountFactor,
            ExplorationRate = ExplorationRate,
            MinExplorationRate = MinExplorationRate,
            m_explorationDecayRate = m_explorationDecayRate,
            Death = Death,
            EatFood = EatFood,
            AwayFood = AwayFood,
            TimePenaltyPerStep = TimePenaltyPerStep
        };
}