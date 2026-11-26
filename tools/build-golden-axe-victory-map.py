"""Extract the static parchment and ordered Fin ink tiles from a lossless arcade capture."""

import argparse
from pathlib import Path

import av
import numpy as np
from PIL import Image


def extract(recording, output):
    parchment_width, parchment_height = 160, 224
    tile_size, tile_count = 8, 21
    plate_tick, first_reveal, reveal_interval = 125, 126, 4
    last_reveal = first_reveal + (tile_count - 1) * reveal_interval
    atlas = np.zeros((parchment_height + tile_size, tile_count * tile_size, 3), dtype=np.uint8)
    previous = None
    with av.open(str(recording)) as video:
        for tick, frame in enumerate(video.decode(video=0)):
            if tick < plate_tick:
                continue
            if (frame.width, frame.height) != (320, parchment_height):
                raise ValueError("Expected a native 320x224 capture.")
            if tick == plate_tick:
                parchment = frame.to_ndarray(format="rgb24")[:, :parchment_width]
                atlas[:parchment_height, :parchment_width] = parchment
                previous = parchment
            elif (tick - first_reveal) % reveal_interval == 0:
                parchment = frame.to_ndarray(format="rgb24")[:, :parchment_width]
                y, x = np.where(np.any(previous != parchment, axis=2))
                if len(x) == 0 or x.min() // tile_size != x.max() // tile_size or y.min() // tile_size != y.max() // tile_size:
                    raise ValueError(f"Expected one ink tile to change at frame {tick}.")
                left, top = x.min() // tile_size * tile_size, y.min() // tile_size * tile_size
                slot = (tick - first_reveal) // reveal_interval * tile_size
                atlas[parchment_height:, slot:slot + tile_size] = parchment[top:top + tile_size, left:left + tile_size]
                previous = parchment
                if tick == last_reveal:
                    break
        else:
            raise ValueError("The capture ends before the last ink tile.")

    palette, indices = np.unique(atlas.reshape(-1, 3), axis=0, return_inverse=True)
    if len(palette) > 256:
        raise ValueError("Expected the original arcade palette.")
    image = Image.fromarray(indices.reshape(atlas.shape[:2]).astype(np.uint8))
    image.putpalette(palette.flatten().tolist())
    image.save(output, optimize=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("recording", type=Path,
                        help="Victory capture rendered without sprites, starting at the first black frame")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    assets = Path(__file__).resolve().parents[1] / "G33kShell.Desktop/Assets/GoldenAxe"
    extract(args.recording, args.output or assets / "victory-map.png")


if __name__ == "__main__":
    main()
