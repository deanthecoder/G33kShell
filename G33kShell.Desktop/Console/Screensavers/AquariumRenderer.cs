// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
// purpose. If you modify the code, please retain this copyright header.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.IO;
using DTC.Core;
using DTC.Core.Extensions;
using SkiaSharp;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>Pixel sampling of Kenney sprites, depth compositing and a restrained water pass.</summary>
internal sealed class AquariumRenderer
{
    private sealed record Sprite(int Width, int Height, byte[] Pixels);
    private readonly Sprite[] m_fish = Load("fish_pink", "fish_blue", "fish_orange", "fish_green", "fish_red", "eel");
    private readonly Sprite[] m_weed = Load("seaweed_green_a", "seaweed_green_b", "seaweed_green_c", "seaweed_green_d");
    private readonly Sprite[] m_rocks = Load("rock_a", "rock_b");
    private byte[] m_buffer;

    public static Rgb[] CreatePalette(float opacity, Rgb background = null, Rgb foreground = null)
    {
        background ??= Rgb.Black;
        foreground ??= new Rgb(255, 155, 40);
        var palette = new Rgb[32];
        for (var i = 0; i < palette.Length; i++)
        {
            var value = i / 31f * opacity;
            palette[i] = ((double)value).Lerp(background, foreground);
        }
        return palette;
    }

    private static Sprite[] Load(params string[] names)
    {
        var result = new Sprite[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            using var image = LoadBitmap(names[i]);
            if (image == null) throw new InvalidDataException($"Cannot load aquarium sprite: {names[i]}");
            // The pack includes transparent margins. Size the visible animal, not its canvas.
            var left = image.Width;
            var top = image.Height;
            var right = -1;
            var bottom = -1;
            for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                if (image.GetPixel(x, y).Alpha >= 128)
                {
                    left = Math.Min(left, x); top = Math.Min(top, y);
                    right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
            if (right < left) throw new InvalidDataException($"Empty aquarium sprite: {names[i]}");
            var width = right - left + 1;
            var height = bottom - top + 1;
            var pixels = new byte[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var color = image.GetPixel(left + x, top + y);
                if (color.Alpha >= 128)
                    pixels[y * width + x] = (byte)(2 + (color.Red * 0.2126 + color.Green * 0.7152 + color.Blue * 0.0722) / 255 * 29);
            }
            if (names[i].StartsWith("fish_") || names[i] == "eel")
            {
                // Each species uses the full tonal range: source colour must not make blue
                // fish disappear. Preserve dark eyes and fin markings against bright bodies.
                var darkest = 31;
                var lightest = 1;
                foreach (var shade in pixels)
                    if (shade > 0) { darkest = Math.Min(darkest, (int)shade); lightest = Math.Max(lightest, (int)shade); }
                for (var pixel = 0; pixel < pixels.Length; pixel++)
                    if (pixels[pixel] > 0)
                        pixels[pixel] = (byte)(5 + (pixels[pixel] - darkest) * 26 / Math.Max(1, lightest - darkest));
            }
            result[i] = new Sprite(width, height, pixels);
        }
        return result;
    }

    private static SKBitmap LoadBitmap(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Aquarium");
        if (name != "eel")
            return SKBitmap.Decode(Path.Combine(directory, name + ".png"));

        // These are adjoining tail/head tiles, not animation frames. Join before cropping
        // to retain their shared vertical alignment and continuous body at the seam.
        using var tail = SKBitmap.Decode(Path.Combine(directory, "fish_grey_long_a.png"));
        using var head = SKBitmap.Decode(Path.Combine(directory, "fish_grey_long_b.png"));
        if (tail == null || head == null) throw new InvalidDataException("Cannot load eel tiles.");
        var combined = new SKBitmap(tail.Width + head.Width, Math.Max(tail.Height, head.Height));
        using var canvas = new SKCanvas(combined);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(tail, 0, 0);
        canvas.DrawBitmap(head, tail.Width, 0);
        return combined;
    }

    public void Draw(AquariumScene scene, PixelScreenData screen, Rgb background = null, Rgb foreground = null)
    {
        var w = screen.Width;
        var h = screen.Height;
        var time = scene.Time;
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var ground = scene.Ground(x);
            var shade = 3;
            if (y >= ground)
            {
                var grain = unchecked((uint)(x * 73856093 ^ y * 19349663)) % 13;
                shade = y < ground + 2 ? 14 : grain < 3 ? 8 : 5;
                // Broad caustic ribbons travel over the gravel, never flash.
                if (MathF.Sin(x * 0.07f + y * 0.13f + time * 0.4f) + MathF.Sin(x * 0.035f - time * 0.3f) > 1.3f)
                    shade += 2;
            }
            screen.Pixels[y * w + x] = (byte)shade;
        }
        // Fine surface line and suspended motes establish the otherwise dark water.
        for (var x = 0; x < w; x++)
            screen.SetPixel(x, 4 + (int)MathF.Round(MathF.Sin(x * 0.045f + time * 0.6f)), 8);
        for (var i = 0; i < 28; i++)
        {
            var x = (int)((i * 73.1f + MathF.Sin(time * 0.13f + i) * 4 + w) % w);
            var y = (int)((i * 37.7f + time * 0.7f) % (h - 20));
            screen.SetPixel(x, y, 5);
        }
        for (var layer = 0; layer < 5; layer++)
        {
            foreach (var plant in scene.Plants)
                if (Layer(plant.Depth) == layer)
                    DrawPlant(scene, screen, plant);
            if (layer == 2)
                foreach (var rock in scene.Rocks)
                    Blit(screen, m_rocks[rock.Sprite], rock.X, rock.Y - rock.Height / 2,
                        rock.Width, rock.Height, false, 0.65f);
            foreach (var fish in scene.Fishes)
            {
                if (Layer(fish.Depth) != layer) continue;
                var sprite = m_fish[fish.Species];
                var height = fish.Size * sprite.Height / sprite.Width;
                var pitch = Math.Clamp(fish.Velocity.Y / 35, -0.35f, 0.35f) * (fish.VisualFacing >= 0 ? 1 : -1);
                Blit(screen, sprite, fish.Position.X, fish.Position.Y, fish.RenderWidth, height,
                    fish.VisualFacing < 0, 0.9f + fish.Depth * 0.1f, pitch,
                    time * (3 + fish.Velocity.Length() * 0.3f) + fish.Phase);
            }
        }
        foreach (var food in scene.FoodParticles)
            screen.SetPixel((int)food.Position.X, (int)food.Position.Y, 26);
        // Sample from a separate buffer: no accumulated distortion or per-frame allocations.
        if (m_buffer == null || m_buffer.Length != screen.Pixels.Length)
            m_buffer = new byte[screen.Pixels.Length];
        Array.Copy(screen.Pixels, m_buffer, m_buffer.Length);
        for (var y = 0; y < h; y++)
        {
            var shift = (int)MathF.Round(MathF.Sin(y * 0.075f + time * 0.55f) * 0.65f + MathF.Sin(y * 0.031f - time * 0.29f) * 0.35f);
            for (var x = 0; x < w; x++)
                screen.Pixels[y * w + x] = m_buffer[y * w + Math.Clamp(x + shift, 0, w - 1)];
        }
        screen.SetPalette(CreatePalette(scene.Opacity, background, foreground));
    }

    private static int Layer(float depth) => Math.Min(4, (int)(depth * 5));

    private void DrawPlant(AquariumScene scene, PixelScreenData screen, AquariumScene.Plant plant)
    {
        var sprite = m_weed[plant.Sprite];
        var height = (int)plant.Height;
        var width = Math.Max(7, height * sprite.Width / sprite.Height);
        var root = scene.Ground(plant.X) + 3;
        for (var y = 0; y < height; y++)
        {
            var tip = 1 - y / (float)height;
            var sway = tip * tip * (MathF.Sin(scene.Time * 0.65f + plant.X * 0.018f) * 4 + MathF.Sin(scene.Time * 1.1f + plant.Phase + tip * 2) * 1.5f);
            for (var x = 0; x < width; x++)
            {
                var shade = sprite.Pixels[y * sprite.Height / height * sprite.Width + x * sprite.Width / width];
                if (shade == 0) continue;
                screen.SetPixel((int)(plant.X - width / 2f + x + sway), (int)(root - height + y),
                    (byte)Math.Clamp((int)(shade * (0.35f + plant.Depth * 0.5f)), 2, 31));
            }
        }
    }

    private static void Blit(PixelScreenData screen, Sprite sprite, float cx, float cy,
        float width, float height, bool flip, float brightness, float pitch = 0, float tailPhase = 0)
    {
        var w = Math.Max(1, (int)width);
        var h = Math.Max(1, (int)height);
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var sx = (int)(((flip ? w - 1 - x : x) + 0.5f) * sprite.Width / w);
            var shade = sprite.Pixels[y * sprite.Height / h * sprite.Width + sx];
            if (shade == 0) continue;
            // The source fish face right. Flex the tail more than the body.
            var tail = Math.Max(0, 1 - sx / (float)sprite.Width * 2.5f);
            var dy = MathF.Sin(tailPhase) * tail * 1.3f;
            screen.SetPixel((int)(cx - w / 2f + x), (int)(cy - h / 2f + y + pitch * (x - w / 2f) + dy),
                (byte)Math.Clamp((int)(shade * brightness), 1, 31));
        }
    }
}
