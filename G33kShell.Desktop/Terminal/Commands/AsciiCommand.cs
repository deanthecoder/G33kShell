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
using System.Text;
using System.Threading.Tasks;
using G33kShell.Desktop.Terminal.Attributes;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription("Displays a full ASCII table (0–255) using box drawing characters.", "Example: ascii")]
public class AsciiCommand : CommandBase
{
    private static readonly char[] Cp437 =
    {
        '·','☺','☻','♥','♦','♣','♠','•','◘','○','◙','♂','♀','♪','♫','☼',
        '►','◄','↕','‼','¶','§','▬','↨','↑','↓','→','←','∟','↔','▲','▼',
        ' ','!','"','#','$','%','&','\'','(',')','*','+',',','-','.','/',
        '0','1','2','3','4','5','6','7','8','9',':',';','<','=','>','?',
        '@','A','B','C','D','E','F','G','H','I','J','K','L','M','N','O',
        'P','Q','R','S','T','U','V','W','X','Y','Z','[','\\',']','^','_',
        '`','a','b','c','d','e','f','g','h','i','j','k','l','m','n','o',
        'p','q','r','s','t','u','v','w','x','y','z','{','|','}','~','⌂',
        'Ç','ü','é','â','ä','à','å','ç','ê','ë','è','ï','î','ì','Ä','Å',
        'É','æ','Æ','ô','ö','ò','û','ù','ÿ','Ö','Ü','¢','£','¥','₧','ƒ',
        'á','í','ó','ú','ñ','Ñ','ª','º','¿','⌐','¬','½','¼','¡','«','»',
        '░','▒','▓','│','┤','Á','Â','À','©','╣','║','╗','╝','¢','¥','┐',
        '└','┴','┬','├','─','┼','ã','Ã','╚','╔','╩','╦','╠','═','╬','¤',
        'ð','Ð','Ê','Ë','È','ı','Í','Î','Ï','┘','┌','█','▄','¦','Ì','▀',
        'Ó','ß','Ô','Ò','õ','Õ','µ','þ','Þ','Ú','Û','Ù','ý','Ý','¯','´',
        '≡','±','‗','¾','¶','§','÷','¸','°','¨','·','¹','³','²','■',' '
    };

    protected override Task<bool> Run(ITerminalState state)
    {
        const int cols = 16;
        const int rows = 16;

        WriteLine("┌─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┐");
        for (var row = 0; row < rows; row++)
        {
            var line = new StringBuilder("│");
            for (var col = 0; col < cols; col++)
            {
                var code = row * cols + col;
                var c = Cp437[code];
                var display = char.IsControl(c) ? "·" : c.ToString();
                line.Append($"{code,3:D3}:{display}│");
            }
            WriteLine(line.ToString());
            if (row < rows - 1)
                WriteLine("├─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┤");
        }
        WriteLine("└─────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┘");

        return Task.FromResult(true);
    }
}