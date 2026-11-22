#!/usr/bin/env python3
"""Convert local map recordings to G33kShell's shared-palette frame format."""

import argparse
import heapq
import os
from pathlib import Path
import struct
import tempfile

import av
import brotli
import numpy as np


WIDTH, HEIGHT = 320, 224
MAGIC = b"G33KMAP3"
PALETTE_SIZE = 256


def encode(source, destination):
    palette, indexes = build_palette(source)
    with av.open(str(source)) as video, tempfile.NamedTemporaryFile(
        dir=destination.parent, prefix=destination.name + ".", delete=False
    ) as temporary:
        try:
            temporary.write(struct.pack("<8sHH", MAGIC, WIDTH, HEIGHT))
            compressor = brotli.Compressor(quality=7)

            def write(data):
                temporary.write(compressor.process(data))

            write(palette.astype(np.uint8).tobytes())
            input_count, record_count = write_frames(video, write, indexes)
            temporary.write(compressor.finish())
            temporary.close()
            os.replace(temporary.name, destination)
        finally:
            Path(temporary.name).unlink(missing_ok=True)
    print(f"{destination.name}: {input_count} decoded, {record_count} stored, {destination.stat().st_size:,} bytes")


def video_frames(video):
    stream = video.streams.video[0]
    scale = stream.width // WIDTH
    if scale not in (1, 2) or (stream.width, stream.height) != (scale * WIDTH, scale * HEIGHT):
        raise ValueError(f"Expected a {WIDTH} by {HEIGHT} recording at 1x or 2x.")

    for frame in video.decode(stream):
        # The reference MP4s were enlarged with nearest-neighbor scaling.
        bgr = frame.to_ndarray(format="bgr24")[::scale, ::scale].reshape(-1, 3)
        blue, green, red = bgr.astype(np.uint32).T
        yield frame, red << 16 | green << 8 | blue


def build_palette(source):
    counts = np.zeros(1 << 24, dtype=np.int32)
    with av.open(str(source)) as video:
        for _, colors in video_frames(video):
            unique, frequencies = np.unique(colors, return_counts=True)
            counts[unique] += frequencies
    colors = np.flatnonzero(counts)
    weights = counts[colors].astype(np.float64)
    samples = np.column_stack((colors & 255, colors >> 8 & 255, colors >> 16)).astype(np.int32)
    palette = np.zeros((PALETTE_SIZE, 3), dtype=np.int32)
    nonblack = np.flatnonzero(colors != 0)
    if len(nonblack) < PALETTE_SIZE:
        palette[1:1 + len(nonblack)] = samples[nonblack]
    else:
        palette[1:] = split_colors(samples, weights, nonblack)
        # Refine the initial weighted splits by minimizing squared RGB error.
        # Keep entry zero black for the map borders and initial frame.
        for _ in range(8):
            labels = nearest_colors(samples, palette)
            totals = np.bincount(labels, weights=weights, minlength=PALETTE_SIZE)
            populated = np.flatnonzero(totals[1:]) + 1
            for channel in range(3):
                sums = np.bincount(labels, weights=weights * samples[:, channel], minlength=PALETTE_SIZE)
                palette[populated, channel] = np.rint(sums[populated] / totals[populated])

    indexes = np.zeros(1 << 24, dtype=np.uint8)
    indexes[colors] = nearest_colors(samples, palette)
    return palette, indexes


def split_colors(samples, weights, nonblack):
    """Seed the palette by splitting the box with the most weighted color error."""
    boxes = []
    sequence = 0

    def push(indices):
        nonlocal sequence
        colors = samples[indices]
        weight = weights[indices]
        center = np.average(colors, axis=0, weights=weight)
        error = np.sum((colors - center) ** 2 * weight[:, None], axis=0)
        axis = int(np.argmax(error))
        sequence += 1
        heapq.heappush(boxes, (-float(error[axis]), sequence, axis, indices, center))

    push(nonblack)
    while len(boxes) < PALETTE_SIZE - 1:
        _, _, axis, indices, _ = heapq.heappop(boxes)
        ordered = indices[np.argsort(samples[indices, axis], kind="stable")]
        cumulative = np.cumsum(weights[ordered])
        middle = np.clip(np.searchsorted(cumulative, cumulative[-1] / 2) + 1, 1, len(ordered) - 1)
        push(ordered[:middle])
        push(ordered[middle:])
    return np.rint([box[-1] for box in boxes]).astype(np.int32)


def nearest_colors(samples, palette):
    labels = np.empty(len(samples), dtype=np.uint8)
    palette_norm = (palette * palette).sum(axis=1, dtype=np.int32)
    for start in range(0, len(samples), 1024):
        block = samples[start:start + 1024]
        # The sample norm is constant across palette entries and cannot change the winner.
        scores = palette_norm - 2 * (block @ palette.T)
        labels[start:start + len(block)] = scores.argmin(axis=1)
    return labels


def write_frames(video, write, indexes):
    previous = np.zeros(WIDTH * HEIGHT, dtype=np.uint8)
    pending = None
    origin = last_time = None
    last_duration = None
    input_count = record_count = 0

    for frame, colors in video_frames(video):
        timestamp = frame.pts * frame.time_base
        if origin is None:
            origin = timestamp
        elif timestamp <= last_time:
            raise ValueError("Frame timestamps must increase.")
        if last_time is not None:
            last_duration = timestamp - last_time
        last_time = timestamp
        input_count += 1

        pixels = indexes[colors]
        if pending is not None and np.array_equal(pixels, pending):
            continue
        if pending is not None:
            write_delta(write, timestamp - origin, previous, pending)
            previous = pending
            record_count += 1
        pending = pixels

    if pending is None:
        raise ValueError("The recording contains no frames.")

    duration = frame.duration * frame.time_base if frame.duration else last_duration
    if not duration:
        raise ValueError("The last frame has no display duration.")
    write_delta(write, last_time + duration - origin, previous, pending)
    write(struct.pack("<I", 0))
    return input_count, record_count + 1


def write_delta(write, end_time, previous, pixels):
    # Unchanged pixels become zero; all 256 palette entries remain usable.
    delta = np.bitwise_xor(previous, pixels).reshape(HEIGHT, WIDTH)
    changed_rows = np.flatnonzero(delta.any(axis=1))
    write(struct.pack("<I", round(end_time * 1_000_000)))
    if not len(changed_rows):
        write(bytes((HEIGHT, 0)))
        return
    first, last = changed_rows[0], changed_rows[-1]
    write(bytes((first, last)))
    write(delta[first:last + 1].tobytes())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("recordings", nargs="+", type=Path)
    parser.add_argument("--output", required=True, type=Path, help="Directory for the resulting .gmap files")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    for source in args.recordings:
        encode(source, args.output / source.with_suffix(".gmap").name)


if __name__ == "__main__":
    main()
