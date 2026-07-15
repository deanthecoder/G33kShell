using NUnit.Framework;
using G33kShell.Desktop.Skins;

namespace G33kShell.Desktop.Console.Controls;

[TestFixture]
public class TextBoxTests
{
    [TestCase("first\r\nsecond", "first\nsecond")]
    [TestCase("first\rsecond", "first\nsecond")]
    [TestCase("first\nsecond", "first\nsecond")]
    public void Append_NormalizesLineEndings(string text, string expected)
    {
        var windowManager = new WindowManager(80, 25, new RetroMonoDos());
        var textBox = new TextBox(80);
        windowManager.Root.AddChild(textBox);

        textBox.Append(text);

        Assert.That(textBox.TextWithoutPrefix, Is.EqualTo(expected));
    }
}
