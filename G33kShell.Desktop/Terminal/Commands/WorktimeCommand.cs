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
    private readonly Random m_random = new Random();

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
    
    private string GetMotivationalMessage(double progress)
    {
        string[] messages;

        var now = DateTime.Now;
        var isAfterHours = now.Hour >= 18; // After 6 PM

        if (isAfterHours)
        {
            messages = new[]
            {
                "Night owl mode activated! Let's crush some bugs while the world sleeps.",
                "The silence of the evening is the perfect time to write your masterpiece.",
                "Late-night coding: where legends are born, and caffeine becomes life.",
                "Dark mode is your ally. Light up the night with brilliant ideas!",
                "Every keystroke tonight is tomorrow's success.",
                "You're not just debugging code; you're debugging the fabric of reality.",
                "When the stars are out, the real devs shine. Keep going!",
                "Even the moon's impressed by your dedication. Keep at it!",
                "This is where the magic happens — when everyone else has given up.",
                "You're the 24/7 build pipeline. Keep shipping greatness!"
            };
        }
        else if (progress < 0.33)
        {
            messages = new[]
            {
                "You've got this! The day is young, and so are we (in spirit)!",
                "Rise and grind! Coffee in one hand, ambition in the other.",
                "Early bird gets the worm, but don't overthink it. Worms are gross.",
                "The day's potential is as big as your imagination. Go wild!",
                "Rise and shine, dev warrior! Time to squash some bugs and refactor your destiny!",
                "You're the main method in today's program—run strong!",
                "Think of today as a new repo: commit to making it awesome!",
                "Coffee is the fuel, code is the engine. Let's build something incredible!",
                "Fresh day, fresh logs. No errors so far—let's keep it that way!"
            };
        }
        else if (progress < 0.67)
        {
            messages = new[]
            {
                "You're in the zone! Keep up the great work.",
                "Midday vibes: The perfect mix of coffee buzz and snack motivation.",
                "You're halfway there — living on more than a prayer!",
                "Lunch break on the horizon. Finish strong!",
                "Time flies when you're smashing goals, doesn't it?",
                "Midday check: Your code is compiling, and so are your efforts!",
                "Don't let the imposter syndrome throw an exception—you've got this!",
                "You're refactoring today's chaos into something beautiful!",
                "Halfway there! Remember: clean code is happy code.",
                "You're debugging life one breakpoint at a time — keep stepping through."
            };
        }
        else
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
        
        return messages[m_random.Next(messages.Length)];
    }
}