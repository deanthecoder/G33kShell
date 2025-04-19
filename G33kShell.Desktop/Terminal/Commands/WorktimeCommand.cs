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
using System.Threading.Tasks;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Terminal.Attributes;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription("Display work-day progress and a motivational message.")]
public class WorktimeCommand : CommandBase
{
    protected override Task<bool> Run(ITerminalState state)
    {
        var now = DateTime.Now;
        var startOfWorkDay = new DateTime(now.Year, now.Month, now.Day, 9, 0, 0);
        var endOfWorkDay = new DateTime(now.Year, now.Month, now.Day, 17, 30, 0);
        var totalWorkHours = (endOfWorkDay - startOfWorkDay).TotalHours;
        var elapsedWorkHours = (now - startOfWorkDay).TotalHours;
        var progress = (elapsedWorkHours / totalWorkHours).Clamp(0.0, 1.0);

        var totalWidth = state.CliPrompt.Width;
        var progressWidth = totalWidth - 2 - 5;
        var filledWidth = (int)Math.Round(progressWidth * progress);

        // Display progress bar
        WriteLine($"[{new string('=', filledWidth).PadRight(progressWidth, '.')}] {progress:P0}");

        // Select motivational message
        var message = GetMotivationalMessage(progress);

        // Display the message
        WriteLine(message);
        WriteLine();

        return Task.FromResult(true);
    }
    
    private static string GetMotivationalMessage(double progress)
    {
        string[] messages;
        if (progress < 0.33)
        {
            messages = new[]
            {
                "The day is wrong - You've got this!",
                "Rise and grind! Coffee in one hand, ambition in the other.",
                "Early bird gets the worm, but don't overthink it. Worms are gross.",
                "The day's potential is as big as your imagination.",
                "Rise and shine! Time to squash some bugs!",
                "Think of today as a new repo: commit to making it awesome!",
                "Coffee is the fuel, code is the engine.",
                "Fresh day, fresh logs. No errors so far!"
            };
        }
        else if (progress < 0.67)
        {
            messages = new[]
            {
                "You're in the zone! Keep up the great work.",
                "You're halfway there!",
                "Lunch break on the horizon.",
                "Time flies when you're smashing goals.",
                "Midday check: Your code is compiling, and so are your efforts!",
                "Refactor today's chaos into something beautiful.",
                "Halfway there! Remember: Clean code is happy code.",
                "You're debugging life one breakpoint at a time."
            };
        }
        else if (progress < 1.0)
        {
            messages = new[]
            {
                "Almost there! Finish like the legend you are.",
                "You've done most of the work; now it's time for a strong finale!",
                "Your reward? Freedom. Sweet, sweet freedom.",
                "Smell that? It's the weekend/relaxation/evening calling you!",
                "The end is near, and so is the merge request. Ship it!",
                "Almost there! Just a few more lines before you can `return;`.",
                "The day's almost over. Time to run the final tests and celebrate!",
                "Congratulations! Today's log is full of success messages."
            };
        }
        else if (progress < 1.06)
        {
            messages = new[]
            {
                "Pack it up! Time to swap code for couch mode.",
                "The build is complete. Time to deploy yourself home.",
                "Keyboard down, feet up. You've earned it.",
                "Clocking out? More like logging off in style!",
                "You survived another day.",
                "No more commits today — Merge yourself with the sofa.",
                "The only bug left to squash is in tonight’s TV show.",
                "Exit(0); See you tomorrow!"
            };
        }
        else
        {
            messages = new[]
            {
                "The silence of the evening is the perfect time to write your masterpiece.",
                "Late-night coding: Where legends are born.",
                "Dark mode is your ally.",
                "Every keystroke tonight is tomorrow's success.",
                "When the stars are out, the real devs shine.",
                "This is where the magic happens — when everyone else has given up.",
                "You're the 24/7 build pipeline."
            };
        }
        
        return messages[Random.Shared.Next(messages.Length)];
    }
}