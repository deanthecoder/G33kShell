"""Build map5.gmap from the parchment segment of atomic14's set-6 ending.mp4."""
import argparse
import io
import json
import runpy
import struct
from pathlib import Path

import av
import brotli
import numpy as np
from PIL import Image, ImageDraw


def build(recording, assets, output):
    with av.open(str(recording)) as video:
        opening = []
        for number, frame in enumerate(video.decode(video=0)):
            if number > 592:
                break
            if number >= 560:
                opening.append(frame.to_image().resize((320, 224), Image.Resampling.NEAREST))
    if len(opening) != 33:
        raise ValueError("Missing victory parchment frames 560 through 592.")

    base = opening[-1].copy()
    base.paste((0, 0, 0), (160, 0, 320, 224))
    atlas = json.loads((assets / "sprites.json").read_text())
    _, x, y, w, h, *_ = atlas["frames"][1042]
    pens = np.asarray(Image.open(assets / "sprites-0.png").convert("L").crop((x, y, x + w, y + h))) // 17
    colors = np.frombuffer(bytes.fromhex(atlas["palettes"][0x80]), dtype=np.uint8).reshape(16, 3)
    quill = Image.fromarray(colors[pens])
    mask = Image.fromarray((((pens > 0) & (pens < 15)) * 255).astype(np.uint8))
    letters = Image.open(assets / "tiles.png").convert("L")
    text_pens = [(0, 0, 0), (148, 165, 214), (90, 107, 156)]
    ink = (164, 16, 8)

    # Reserve exact font and quill colors alongside the captured parchment tones.
    palette = [0, 0, 0] + base.quantize(219).getpalette()[:657]
    palette += list(colors.flatten()) + [v for rgb in text_pens + [ink] for v in rgb]
    palette += [0] * (768 - len(palette))
    indexed = Image.new("P", (1, 1))
    indexed.putpalette(palette)

    # Coordinates are nib positions in the original 320x224 map. Lift between strokes.
    strokes = [
        [(42, 72), (46, 55), (58, 55)], [(44, 63), (54, 63)],
        [(62, 55), (58, 72)], [(56, 72), (62, 72)],
        [(66, 72), (70, 55), (77, 71), (81, 54)]
    ]
    motion = []
    last = (25, 86)
    for stroke in strokes:
        for segment, point in enumerate(stroke):
            dx, dy = point[0] - last[0], point[1] - last[1]
            steps = max(1, round((dx * dx + dy * dy) ** 0.5 / 2))
            for i in range(1, steps + 1):
                motion.append(((round(last[0] + dx * i / steps), round(last[1] + dy * i / steps)), segment > 0))
            last = point

    def animation():
        yield from opening[:-1]
        page = base.copy()
        draw = ImageDraw.Draw(page)
        message = "CONGRATULATIONS !!"
        last = (25, 86)
        for tick in range(155):
            if tick % 3 == 0 and tick // 3 < len(message):
                j = tick // 3
                code = ord(message[j])
                for yy in range(8):
                    for xx in range(8):
                        pen = letters.getpixel((code % 128 * 8 + xx, code // 128 * 8 + yy)) // 32
                        if pen:
                            page.putpixel((160 + j * 8 + xx, 48 + yy), text_pens[pen])
            age = tick - 12
            if 0 <= age < len(motion):
                point, writing = motion[age]
                if writing:
                    draw.line([last, point], fill=ink, width=1)
                last = point
                shot = page.copy()
                shot.paste(quill, (point[0], point[1] - h + 1), mask)
                yield shot
            else:
                yield page
        for tick in range(1, 29):
            dx, dy = 160 * tick // 28, 112 * tick // 28
            shot = Image.new("RGB", base.size)
            if dy < 112:
                box = (dx, dy, 320 - dx, 224 - dy)
                shot.paste(page.crop(box), box[:2])
            yield shot

    write_delta = runpy.run_path(str(Path(__file__).with_name("encode-golden-axe-maps.py")))["write_delta"]
    payload = io.BytesIO()
    # GMAP stores BGR triples. PIL palettes use RGB.
    payload.write(np.asarray(palette, dtype=np.uint8).reshape(-1, 3)[:, ::-1].tobytes())
    previous = np.zeros(320 * 224, dtype=np.uint8)
    for number, shot in enumerate(animation(), 1):
        pixels = np.asarray(shot.quantize(palette=indexed, dither=Image.Dither.NONE)).flatten()
        # PIL's fast palette lookup approximates close colors. Preserve the two observed text pens exactly.
        rgb = np.asarray(shot)
        for index in (1, 2):
            pixels[np.all(rgb == text_pens[index], axis=2).ravel()] = 220 + 16 + index
        write_delta(payload.write, number / 30, previous, pixels)
        previous = pixels
    payload.write(struct.pack("<I", 0))
    output.write_bytes(struct.pack("<8sHH", b"G33KMAP3", 320, 224) + brotli.compress(payload.getvalue(), quality=7))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("recording", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    assets = Path(__file__).resolve().parents[1] / "G33kShell.Desktop/Assets/GoldenAxe"
    build(args.recording, assets, args.output or assets / "map5.gmap")
