#!/usr/bin/env python3
# Code authored by Dean Edis (DeanTheCoder).
# Anyone is free to copy, modify, use, compile, or distribute this software,
# either in source code form or as a compiled binary, for any purpose.
#
# If you modify the code, please retain this copyright header,
# and consider contributing back to the repository or letting us know
# about your modifications. Your contributions are valued!
#
# THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

"""Capture short clips from the Golden Axe screensaver for sharing."""

import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


SCENES = ("knight-door", "brothers-door", "bridge-barge", "castle-ending", "forest-opening", "shadows", "campfire", "camp-thieves", "dragon-fire", "magic", "landing-dust", "skeleton-dust",
          "weapon-swipes-ax", "weapon-swipes-tyris", "weapon-swipes-gilius", "weapon-swipes-enemies",
          "turtle-eye", "eagle-eye", "dragon-flame", "running-dust", "stone-bodies", "eagle-sunset", "eagle-clouds", "castle-camera", "potion-sparkles", "camera-follow", "ledge-camera", "dwarf-lightning", "villagers", "village-routing", "village-opening", "tyris-six", "tyris-seven", "tyris-eight", "tyris-dragon", "ax-one", "ax-two", "ax-three", "ax-four", "ax-five", "ax-six", "gilius-four", "gilius-one", "gilius-two", "gilius-three", "mounted-barge-chicken", "mounted-barge-blue", "mounted-barge-red", "hero-respawn", "autoplay-edge")
REPO = Path(__file__).resolve().parent.parent
TEST_PROJECT = REPO / "G33kShell.Desktop.Tests" / "G33kShell.Desktop.Tests.csproj"
CAPTURE_TEST = "G33kShell.Desktop.Tests.GoldenAxeCaptureTests.RenderClips"


def encode(ffmpeg, source, destination, options):
    subprocess.run(
        [ffmpeg, "-hide_banner", "-loglevel", "error", "-nostdin",
         "-f", "rawvideo", "-pixel_format", "bgra", "-video_size", "320x224", "-framerate", "30",
         "-i", str(source), *options, str(destination)],
        check=True,
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True, help="Directory for the finished clips.")
    parser.add_argument("--scene", choices=SCENES, action="append", help="Capture one scene. Repeat to select several.")
    parser.add_argument("--gif", action="store_true", help="Also export smaller looping GIF previews.")
    parser.add_argument("--ffmpeg", help="FFmpeg executable to use. Defaults to PATH, then an installed imageio-ffmpeg copy.")
    args = parser.parse_args()
    dotnet = shutil.which("dotnet")
    ffmpeg = shutil.which(os.path.expanduser(args.ffmpeg or "ffmpeg"))
    if not ffmpeg and not args.ffmpeg:
        try:
            import imageio_ffmpeg
            ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
        except (ImportError, RuntimeError):
            pass
    if not dotnet or not ffmpeg:
        parser.error("The .NET SDK and FFmpeg are required. Supply --ffmpeg, add FFmpeg to PATH, or install imageio-ffmpeg.")
    if subprocess.run([ffmpeg, "-version"], stdout=subprocess.DEVNULL).returncode:
        parser.error("FFmpeg failed to start. Use --ffmpeg to select a working executable.")

    scenes = list(dict.fromkeys(args.scene or SCENES))
    output = args.output.expanduser().resolve()
    output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".capture-", dir=output) as temporary:
        scratch = Path(temporary)
        env = {
            **os.environ,
            "G33K_GOLDEN_AXE_CAPTURE_DIR": str(scratch),
            "G33K_GOLDEN_AXE_CAPTURE_SCENES": ",".join(scenes),
        }
        subprocess.run([dotnet, "test", str(TEST_PROJECT), "--filter", f"FullyQualifiedName={CAPTURE_TEST}"],
                       cwd=REPO, env=env, check=True)
        for scene in scenes:
            source = scratch / f"{scene}.bgra"
            if not source.is_file() or source.stat().st_size == 0 or source.stat().st_size % (320 * 224 * 4):
                parser.exit(1, f"No complete frames were captured for {scene}.\n")
            video = scratch / f"{scene}.mp4"
            encode(ffmpeg, source, video, [
                "-vf", "scale=1280:896:flags=neighbor", "-c:v", "libx264", "-crf", "16",
                "-maxrate", "10M", "-bufsize", "20M", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
            ])
            video.replace(output / video.name)
            print(output / video.name)
            if args.gif:
                gif = scratch / f"{scene}.gif"
                encode(ffmpeg, source, gif, [
                    "-filter_complex_threads", "1", "-filter_complex",
                    "fps=15,scale=640:448:flags=neighbor,split[a][b];"
                    "[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4",
                    "-loop", "0",
                ])
                gif.replace(output / gif.name)
                print(output / gif.name)


if __name__ == "__main__":
    try:
        main()
    except subprocess.CalledProcessError as error:
        raise SystemExit(error.returncode) from error
