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
using System.Diagnostics;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// Awesome music: https://www.thebritishibm.com/
/// </summary>
[DebuggerDisplay("TbibmCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class TbibmCanvas : ScreensaverBase
{
    private WillyCanvas.Willy m_willy;

    public TbibmCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
    {
        Name = "tbibm";
    }
    
    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);

        var theBritish = new[]
        {
            "                       ######",
            "##### #    # ######    #     # #####  # ##### #  ####  #    #",
            "  #   #    # #         ######  #    # #   #   # #      #    #",
            "  #   ###### #####     #     # #####  #   #   #  ####  ######",
            "  #   #    # #         #     # #   #  #   #   #      # #    #",
            "  #   #    # ######    ######  #    # #   #   #  ####  #    #"
        };
        var ibm = new[]
        {
            "........... ........... ...........",
            ".....#..... ..######... .#.......#.",
            ".....#..... ..#.....#.. .##.....##.",
            ".....#..... ..#.....#.. .#.#...#.#.",
            ".....#..... ..######... .#..#.#..#.",
            ".....#..... ..#.....#.. .#...#...#.",
            ".....#..... ..#.....#.. .#.......#.",
            ".....#..... ..###### .. .#.......#.",
            "........... ........... ..........."
        };

        var guy = new[]
        {
            "======================+==----",
            "====+==+++======+=======-==--",
            "====+++++=============--=----",
            "====+++++++===========-###:--",
            "=====++=++++=====+=-==-######",
            "=======++++===++==---=-######",
            "==+===++++====+===--=-#######",
            "+===++======++=+=+==--#######",
            "+++=+=++++===+======-:###-:-#",
            "==+++==++==========-=###---:#",
            "===+++=+++==+++===#::####::##",
            "===+++++++==+++===###########",
            "===+++==++==+++++=-##########",
            "====+++=+=+++=++==+##########",
            "++===+++++++++==++=-#########",
            "+======+++++++======+########",
            "=========+++++====+++-#######",
            "+=======+++++++=======-######",
            "=++++=++++++++=====+=+-######",
            "==+==++++====+===+=+==-######",
            "==+=++++==========++-########",
            "==+=+=+++======++=...=:######",
            "==+=========..............##:",
            "=+++=+=...................##:",
            "===.......................-##",
            "=+.........................##",
            "+..........................##",
            "...........................:#"
        };
        
        var highResScreen = new HighResScreen(screen);
        
        highResScreen.DrawFilledBox(0, 0, screen.Width, 8, 0.4.Lerp(Background, Foreground));
        highResScreen.DrawFilledBox(0, 9, screen.Width, 9, 0.2.Lerp(Background, Foreground));
        
        // The British.
        var y = 1;
        for (var i = 0; i < theBritish.Length; i++, y++)
        {
            for (var x = 0; x < theBritish[i].Length; x++)
            {
                if (theBritish[i][x] == '#')
                    highResScreen.Plot(x + 1, y, Foreground);
            }
        }
        
        // IBM
        y += 2;
        for (var i = 0; i < ibm.Length; i++, y++)
        {
            for (var x = 0; x < ibm[i].Length; x++)
            {
                if (ibm[i][x] == '.')
                    highResScreen.Plot(x + 24, y, Foreground);
            }
        }
        
        // Guy.
        y /= 2;
        y++;
        var guyBackground = 0.1.Lerp(Background, Foreground);
        for (var i = 0; i < guy.Length; i++, y++)
        {
            for (var x = 0; x < guy[i].Length; x++)
            {
                screen.PrintAt(x + screen.Width - guy[i].Length, y, guy[i][x]);
                screen.SetBackground(x + screen.Width - guy[i].Length, y, guyBackground);
            }
        }
        
        // Date/Time
        screen.PrintAt(81, 6, "It's Friday Night.", Foreground);
        screen.PrintAt(81, 7, "         It's 8pm.", Foreground);
        
        // Willy
        WillyCanvas.LoadWillyFrames(out var willyWidth, out var willyHeight, out var willyFrames);
        m_willy = new WillyCanvas.Willy(screen.Height * 2 - willyHeight - 2, -1, 8, willyWidth, willyHeight, willyFrames);
    }
    
    public override void UpdateFrame(ScreenData screen)
    {
        var loading = new[]
        {
            "          O              OOOO           OOOOO           OOOO         ",
            "          O             O    O            O            O    O        ",
            "          O       OOOO  O    O  OOOO      O    O    O  O             ",
            "          O      O    O OOOOOO  O   O     O    OO   O  O  OOO        ",
            "          O      O    O O    O  O    O    O    O O  O  O    O        ",
            "          OOOOO  O    O O    O  O    O  OOOOO  O  O O   OOOO         ",
            "                 O    O         O   O          O   OO                ",
            "                  OOOO          OOOO           O    O                "
        };
        var twitchBar = new[]
        {
            "       O    O       OOOOO          O    O               OOOO",
            "       O    O         O            O    O              O    O",
            "OOOOO  O    O  OOO    O     OOOO   OOOOOO      OOOOO   O    O  OOOOO",
            "  O    O    O   O     O    O    O  O    O      O    O  OOOOOO  O    O",
            "  O    O OO O   O     O    O       O    O      OOOOO   O    O  O    O",
            "  O     O  O    O     O    O       O    O      O    O  O    O  OOOOO",
            "  O             O          O    O              O    O          O   O",
            "  O            OOO          OOOO               OOOOO           O    O"
        };
        
        // Clear.
        var y = 10;
        screen.Clear(0, y, 70, 28, Foreground, Background);

        // Loading/Twitch Bar.
        y += 2;
        if (Time % 1 < 0.5)
        {
            for (var i = 0; i < loading.Length; i++, y++)
            {
                for (var x = 0; x < loading[i].Length; x++)
                    screen.PrintAt(x, y, loading[i][x] != ' ' ? '█' : ' ');
            }
        }
        else
        {
            for (var i = 0; i < twitchBar.Length; i++, y++)
            {
                for (var x = 0; x < twitchBar[i].Length; x++)
                    screen.PrintAt(x, y, twitchBar[i][x] != ' ' ? '█' : ' ');
            }
        }
        
        // Willy.
        var highResScreen = new HighResScreen(screen);
        m_willy.Draw(highResScreen, Foreground, 70);
        if (m_willy.IsDead)
            m_willy.Reset();
        
        // Floor
        for (var x = 0; x < 70; x++)
            screen.PrintAt(x, 37, "♪ ♫ "[x % 4]);
    }
}