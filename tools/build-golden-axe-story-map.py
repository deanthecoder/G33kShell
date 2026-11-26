"""Extract a story-map sheet and timeline from a native arcade capture."""

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image


WIDTH, HEIGHT = 320, 224
PARCHMENT_WIDTH = 160
TILE = 8
ATLAS_COLUMNS = 21
FONT_COLORS = np.array([(0, 0, 0), (148, 165, 214), (90, 107, 156)], dtype=np.uint8)


def font_lookup(assets):
    with Image.open(assets / "tiles.png") as image:
        pens = np.asarray(image.convert("L")) // 32
    lookup = {}
    # O and 0 share a glyph. Prefer the letter in the story text.
    for code in [ord("O"), *range(512)]:
        x, y = code % 128 * TILE, code // 128 * TILE
        tile = pens[y:y + TILE, x:x + TILE]
        if tile.max() < len(FONT_COLORS):
            lookup.setdefault(FONT_COLORS[tile].tobytes(), chr(code))
    return lookup


def changed_tiles(before, after):
    changed = np.any(before != after, axis=2)
    rows, columns = np.where(changed)
    return sorted({(int(x // TILE), int(y // TILE)) for x, y in zip(columns, rows)})


def extract(recording, origins, assets, stem):
    frames = np.memmap(recording, dtype=np.uint8, mode="r").reshape(-1, HEIGHT, WIDTH, 4)[..., :3]
    close_tick = len(frames) - 60
    if len(frames) != origins["ticks"] or np.any(frames[0]) or np.any(frames[-1]):
        raise ValueError("The sprite positions and frames must span the same complete scene.")

    first_open = 54
    plate = frames[first_open, :, :PARCHMENT_WIDTH]
    ink_tiles, patches = [], []
    lines = {}
    lookup = font_lookup(assets)

    for tick in range(first_open + 1, close_tick):
        ink = changed_tiles(frames[tick - 1, :, :PARCHMENT_WIDTH], frames[tick, :, :PARCHMENT_WIDTH])
        for x, y in ink:
            if tick != 126 + 4 * len(ink_tiles):
                raise ValueError(f"Unexpected ink reveal at tick {tick}.")
            ink_tiles.append((x, y))
            patches.append(frames[tick, y * TILE:(y + 1) * TILE, x * TILE:(x + 1) * TILE])

        letters = changed_tiles(frames[tick - 1, :, PARCHMENT_WIDTH:], frames[tick, :, PARCHMENT_WIDTH:])
        for x, y in letters:
            left = PARCHMENT_WIDTH + x * TILE
            glyph = frames[tick, y * TILE:(y + 1) * TILE, left:left + TILE]
            first, text = lines.setdefault(y, (tick - x * 3, []))
            if tick != first + x * 3 or x < len(text) or glyph.tobytes() not in lookup:
                raise ValueError(f"Unexpected font tile at {(x, y)} on tick {tick}.")
            text.extend(" " * (x - len(text)))
            text.append(lookup[glyph.tobytes()])

    rows = (len(patches) + ATLAS_COLUMNS - 1) // ATLAS_COLUMNS
    atlas = np.zeros((HEIGHT + rows * TILE, ATLAS_COLUMNS * TILE, 3), dtype=np.uint8)
    atlas[:HEIGHT, :PARCHMENT_WIDTH] = plate
    for index, patch in enumerate(patches):
        x = index % ATLAS_COLUMNS * TILE
        y = HEIGHT + index // ATLAS_COLUMNS * TILE
        atlas[y:y + TILE, x:x + TILE] = patch

    palette, indices = np.unique(atlas.reshape(-1, 3), axis=0, return_inverse=True)
    if len(palette) > 256:
        raise ValueError("Expected the original arcade palette.")
    image = Image.fromarray(indices.reshape(atlas.shape[:2]).astype(np.uint8))
    image.putpalette(palette.flatten().tolist())
    image.save(stem.with_suffix(".png"), optimize=True)

    text_lines = [dict(firstTick=first, y=y * TILE, text="".join(text))
                  for y, (first, text) in sorted(lines.items())]
    timeline = dict(ticks=len(frames), inkTiles=ink_tiles, text=text_lines,
                    firstQuill=origins["firstQuill"], quill=origins["quill"])
    # Keep long coordinate lists compact while preserving legible text and timing.
    fields = [f'  "{name}": {json.dumps(value)}' for name, value in timeline.items()]
    stem.with_suffix(".json").write_text("{\n" + ",\n".join(fields) + "\n}\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("recording", type=Path, help="320x224 RGBA frames rendered without hardware sprites")
    parser.add_argument("origins", type=Path, help="JSON with ticks, firstQuill and per-tick quill [x,y] sprite origins")
    parser.add_argument("output", type=Path, help="Output basename for the PNG sheet and JSON timeline")
    args = parser.parse_args()
    assets = Path(__file__).resolve().parents[1] / "G33kShell.Desktop/Assets/GoldenAxe"
    extract(args.recording, json.loads(args.origins.read_text()), assets, args.output)


if __name__ == "__main__":
    main()
