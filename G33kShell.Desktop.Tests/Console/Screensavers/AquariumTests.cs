using System;
using System.IO;
using System.Linq;
using G33kShell.Desktop.Console.Screensavers;
using NUnit.Framework;
using SkiaSharp;

namespace G33kShell.Desktop.Console;

[TestFixture]
public class AquariumTests
{
    [TestCase(80, 25)]
    [TestCase(160, 45)]
    [TestCase(40, 80)]
    [TestCase(240, 20)]
    public void BufferMatchesConsoleAspect(int columns, int rows)
    {
        var size = AquariumCanvas.GetBufferSize(columns, rows);
        Assert.That(size.Width / (double)size.Height, Is.EqualTo(columns / (rows * 2.0)).Within(0.01));
    }

    [TestCase(1)]
    [TestCase(42)]
    [TestCase(987)]
    public void FishLeaveBeforeFadeAndSceneRegenerates(int seed)
    {
        var scene = new AquariumScene(384, 240, seed);
        var sawLeaving = false;
        var sawFade = false;
        var completed = 0;
        for (var frame = 0; frame < 150 * 30; frame++)
        {
            if (scene.Step(1f / 30)) completed++;
            sawLeaving |= scene.Phase == AquariumScene.ScenePhase.Leaving;
            if (scene.Phase == AquariumScene.ScenePhase.Fading)
            {
                sawFade = true;
                Assert.That(scene.Fishes.All(f => f.Position.X < -f.Size || f.Position.X > scene.Width + f.Size), Is.True);
            }
            foreach (var fish in scene.Fishes)
            {
                Assert.That(float.IsFinite(fish.Position.X) && float.IsFinite(fish.Position.Y), Is.True);
                Assert.That(fish.Position.Y, Is.InRange(0, scene.Ground(fish.Position.X)));
            }
        }
        Assert.That(sawLeaving && sawFade, Is.True);
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(scene.Generation, Is.EqualTo(2));
    }

    [TestCase(1)]
    [TestCase(42)]
    [TestCase(987)]
    public void ShoalsExploreAcrossAndDownTheTank(int seed)
    {
        var scene = new AquariumScene(384, 240, seed);
        var minX = new[] { float.MaxValue, float.MaxValue };
        var maxX = new[] { float.MinValue, float.MinValue };
        var minY = new[] { float.MaxValue, float.MaxValue };
        var maxY = new[] { float.MinValue, float.MinValue };
        for (var frame = 0; frame < 110 * 30; frame++)
        {
            scene.Step(1f / 30);
            if (frame < 20 * 30) continue; // Exclude the offscreen entrance.
            for (var school = 0; school < 2; school++)
            {
                var members = scene.Fishes.Where(f => f.School == school).ToArray();
                var x = members.Average(f => f.Position.X);
                var y = members.Average(f => f.Position.Y);
                minX[school] = Math.Min(minX[school], x); maxX[school] = Math.Max(maxX[school], x);
                minY[school] = Math.Min(minY[school], y); maxY[school] = Math.Max(maxY[school], y);
            }
        }
        for (var school = 0; school < 2; school++)
        {
            Assert.That(maxX[school] - minX[school], Is.GreaterThan(150), $"School {school} horizontal travel");
            Assert.That(maxY[school] - minY[school], Is.GreaterThan(50), $"School {school} vertical travel");
        }
    }

    [Test]
    public void PaletteUsesSkinColoursAndFadesToBackground()
    {
        var background = new DTC.Core.Rgb(12, 20, 30);
        var foreground = new DTC.Core.Rgb(230, 210, 190);
        var palette = AquariumRenderer.CreatePalette(1, background, foreground);
        Assert.That(palette[0].R, Is.EqualTo(background.R));
        Assert.That(palette[31].G, Is.EqualTo(foreground.G));
        Assert.That(palette[3].R, Is.GreaterThan(background.R));
        var faded = AquariumRenderer.CreatePalette(0, background, foreground);
        Assert.That(faded.All(c => c.R == background.R && c.G == background.G && c.B == background.B), Is.True);
    }

    [Test]
    public void TurnNarrowsBeforeChangingVisibleSideThenExpands()
    {
        var fish = new AquariumScene.Fish { Size = 24, Velocity = new System.Numerics.Vector2(-10, 0) };
        fish.UpdateFacing(0.125f);
        Assert.That(fish.VisualFacing, Is.GreaterThan(0));
        Assert.That(fish.RenderWidth, Is.EqualTo(12));
        fish.UpdateFacing(0.125f);
        Assert.That(fish.RenderWidth, Is.EqualTo(2));
        fish.UpdateFacing(0.125f);
        Assert.That(fish.VisualFacing, Is.LessThan(0));
        Assert.That(fish.RenderWidth, Is.EqualTo(12));
        fish.UpdateFacing(0.125f);
        Assert.That(fish.RenderWidth, Is.EqualTo(24));
        fish.Velocity = new System.Numerics.Vector2(1, 0);
        fish.UpdateFacing(0.1f);
        Assert.That(fish.VisualFacing, Is.EqualTo(-1), "Small velocity changes must not trigger another turn.");
    }

    [Test]
    public void PackRendersAnimatedFramesWithValidPalette()
    {
        var scene = new AquariumScene(384, 240, 42);
        var renderer = new AquariumRenderer();
        var screen = new PixelScreenData(384, 240, AquariumRenderer.CreatePalette(1));
        for (var i = 0; i < 30 * 30; i++) scene.Step(1f / 30);
        renderer.Draw(scene, screen);
        var before = (byte[])screen.Pixels.Clone();
        Assert.That(before.Max(), Is.LessThan(32));
        Assert.That(before.Distinct().Count(), Is.GreaterThan(15));
        for (var i = 0; i < 30; i++) scene.Step(1f / 30);
        renderer.Draw(scene, screen);
        Assert.That(screen.Pixels, Is.Not.EqualTo(before));

        // Optional visual artifact from the production renderer, useful when tuning the habitat.
        var preview = Environment.GetEnvironmentVariable("AQUARIUM_PREVIEW");
        if (string.IsNullOrEmpty(preview)) return;
        using var bitmap = new SKBitmap(screen.Width, screen.Height);
        for (var y = 0; y < screen.Height; y++)
        for (var x = 0; x < screen.Width; x++)
        {
            var color = screen.Palette[screen.Pixels[y * screen.Width + x]];
            bitmap.SetPixel(x, y, new SKColor(color.R, color.G, color.B));
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var output = File.Create(preview);
        data.SaveTo(output);
    }
}
