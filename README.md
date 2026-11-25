[![Twitter Follow](https://img.shields.io/twitter/follow/deanthecoder?style=social)](https://twitter.com/deanthecoder)

# G33kShell
**G33kShell** is a cross-platform retro-themed desktop shell that wraps a CRT aesthetic around a custom command experience.

![G33kShell Screenshot](img/BIOS1.png)
![G33kShell Screenshot](img/BIOS2.png)
![G33kShell Screenshot](img/Login.png)

## Highlights
- Boot-up BIOS prelude, login theatrics, and optional webcam face detection.
- Powered by Avalonia on .NET for cross-platform hardware-accelerated rendering with crisp pixel fonts and shader-based scanlines.
- Extensive built-in command set spanning navigation, search, system info, automation, and novelty utilities.
- Inline `man` pages, persistent history, and settings that remember your favorite skins, screensavers, and working directories.
- Skin system with `RetroPlasma`, `RetroGreenDos`, `RetroMonoDos`, and `SimpleSkin`.
- Idle timeout hands ownership to animated ASCII and pixel-art screensavers — many driven by AI brains you can train live.

## Built-in Commands
- Navigate your filesystem with `dir`, `tree`, `cd`, `pushd`, `popd`, `paths`, and `location`.
- Inspect and manipulate files via `cat`, `tail`, `copy`, `rm`, `mkdir`, `find`, `grep`, `whereis`, and `open`.
- Integrate with the OS using `reveal`, `space`, `clip`, `now`, `ver`, `worktime`, `shutdown`, and `max`.
- Customize the experience with `skin`, `screensaver`, `man`, `help`, `history`, and the CP437 `ASCII` table.

Tip: run `man <command>` for auto-generated manual pages, or `help` to see every command and alias.

## Useful Command Tips
- Scroll the terminal with the mouse wheel or Ctrl-Up/Down. Typing returns to the current view.
- Reuse lines from the previous command's output with `$<n>` (for example, `cat $4`).
- Manage environment variables with `set` (list all), `set path` (case-insensitive prefix), `set foo=bar` (set for this session), or `set foo=` (remove). Use `set -u foo=bar` to also save for your Windows user, or `set -u foo=` to remove from both scopes. `set -u` lists saved user variables. Quote values containing spaces, for example `set foo="hello world"`. Saved changes apply to future processes; existing applications keep their own environment.
- Copy the last command output to the clipboard with `clip` (use `clip -n` to show line numbers or `clip 4` for a single line).

## Screensavers
Cycle anything you like with `screensaver -l` and `screensaver <name>` - add `_train` to the AI-powered ones to watch them learn in real time.

| Screensaver | Description |
| --- | --- |
| `aquarium` | Amber pixel aquarium with schooling fish, swaying weed, feeding and fading habitat changes. |
| `asciiroids` | AI Asteroids in ASCII; add `_train` to teach it. |
| `badapple` | Mono pixel-art silhouette animation in the Bad Apple style. |
| `boids` | Flocking boids orbit a wandering sphere. |
| `bounce` | Breakout-style brick field with ricocheting faces. |
| `clock` | Big retro clock for the night shift. |
| `conway` | Conway's Game of Life, always plotting. |
| `crystal` | Growing crystal shard in chunky pixels. |
| `cube` | Spinning 3D wireframe cube. |
| `defrag` | Disk defrag blocks click into place. |
| `donut` | The classic spinning ASCII torus. |
| `earth` | Rotating ASCII Earth, tiny and proud. |
| `fire` | Flickering fire buffer, CRT cozy. |
| `fluid` | Low-res fluid sim, surprisingly hypnotic. |
| `heist` | Your last screen gets stolen, then returned. |
| `lemmings` | Tiny lemmings on an endless march. |
| `mandelbrot` | Mandelbrot zooms in chunky characters. |
| `matrix` | Matrix-style code rain. |
| `neo` | Matrix corridor with glowing trails. |
| `pipes` | Chunky monochrome 3D pipes grow through a shaded pixel grid. |
| `plasma` | Old-school plasma waves. |
| `pong` | AI pong duel; add `_train` to coach it. |
| `quiverbloom` | A luminous parametric creature blooms and quivers in monochrome. |
| `sand` | Sand pours over your last screen and settles. |
| `science` | A roaming glass lens refracts a snapshot of your terminal. |
| `scroller` | Retro text scroller for demo vibes. |
| `snake` | AI snake that learns; add `_train`. |
| `stars` | Warp-speed starfield. |
| `swirl` | Hypnotic swirl patterns. |
| `tbibm` | The British IBM tribute with Friday-night swagger. |
| `terminator` | Pixelated Terminator portrait. |
| `tiefighter` | 3D Tie Fighter flyby. |
| `tron` | Light-cycle trails with a tracking camera. |
| `tunnel` | Endless ASCII tunnel run. |
| `twister` | Column twist effect. |
| `willy` | Miner Willy marches across the screen. |
| `worms` | Glowing worms wriggle across the grid. |
| `xenon` | Xenon 2 shopkeeper cameo. |

The `aquarium` screensaver uses a full-buffer palette blended from the active background and foreground colours and an aspect-matched pixel surface (normally 384 pixels wide). Fish school, feed and move between layers of swaying plants. After about two minutes they swim offscreen; the habitat then fades out, regenerates, and fades in. Its fish, rock and weed sprites come from the [Kenney Fish Pack](https://kenney.nl/assets/fish-pack), licensed CC0; the license is included with the assets.

In `random` mode, screensavers switch at a natural cycle ending when available: the end of Bad Apple, a completed defrag (after its completion message), a reassembled heist, a full sand screen, or the end of an AI game. Other effects, and cycles that run longer, switch after five minutes. Selecting a screensaver directly keeps its normal looping behavior.

AI-enabled screensavers (`asciiroids`, `pong`, `snake`) store their trained neural weights between runs so you can pick up right where your last session left off.

The `quiverbloom` screensaver is inspired by [Vitaliy Kaurov's Wolfram Community visualization](https://community.wolfram.com/groups/-/m/t/3516580) and the equations shared there by Daniel Sanchez.

### Bad Apple compression notes

The `badapple` screensaver uses a tiny custom 1bpp movie format rather than shipping thousands of source frames. The source animation started as 6,572 480x360 JPEG frames at 30 fps. We reduce those to 128x120 mono frames, sample the movie down to 5,258 frames at 24 fps, then store the pixels rotated by 270 degrees because those scanlines produce far fewer RLE runs. Playback rotates the pixels back while decoding into the pixel screen.

The frame stream is encoded as one continuous alternating run-length stream across frame boundaries. A single initial colour is stored, then each run byte either draws that many pixels and toggles colour (`1..255`), or extends the current colour by 255 pixels without toggling (`0`). The resulting blob is wrapped with the existing `ByteExtensions.Compress()` LZ4 helper.

Final asset numbers:

| Stage | Size |
| --- | ---: |
| Raw 128x120 1bpp frames at 24 fps | ~9.6 MiB |
| Rotated continuous RLE stream | 1,358,933 bytes |
| LZ4-packed `.bar` asset | 987,617 bytes |

Several tempting alternatives were tested and lost to this simpler format: row dictionaries, duplicate-frame references, cropped frame bounds, XOR deltas, bit-packed run tokens, previous-row reuse, and 16-bit run lengths. Rotation and 24 fps sampling were the only tricks that delivered worthwhile savings without complicating playback.

## Quick Start
- Install the [.NET 8 SDK](https://dotnet.microsoft.com/download).
- Clone the repo and restore dependencies: `dotnet restore`.
- Launch the desktop shell: `dotnet run --project G33kShell.Desktop`.

The application persists settings (skin, screensaver, path history, trained brains) under your user profile. Delete the settings file if you ever want a clean slate.

G33kShell also monitors its own memory use. It writes a detailed process, managed-heap, garbage-collection, renderer, visual-tree, terminal-history, and screensaver summary to the app log at startup and whenever working-set or private memory crosses another 256 MB boundary. On Windows, the log is stored at `%APPDATA%\G33kShell\log.txt`.

## Development Notes
- Run the unit suite with `dotnet test` from the repo root.
- Avalonia assets live under `G33kShell.Desktop/Assets`; screensavers under `G33kShell.Desktop/Console/Screensavers`.
- To build your own screensaver, implement `IScreensaver`, expose a unique `Name`, and drop the class into the screensavers folder — `screensaver -l` will pick it up automatically. Derive from `ScreensaverBase` and call `OnCycleCompleted()` at natural stopping points to support cycle-aware random switching.

## License
MIT © Dean Edis. See `LICENSE` for details.

### Golden Axe (private exercise)

Special thanks to **[atomic14](https://github.com/atomic14)** for
**[golden-axe](https://github.com/atomic14/golden-axe)**. That project's arcade data
extraction, technical documentation and TypeScript implementation were substantial
references for this native C# recreation. We directly reuse its extracted sprites,
palettes, animation scripts, level layouts, height maps, enemy spawn data and magic
effects. G33kShell's simulation, renderer and coded player controller were written
in C#, informed by that work. Golden Axe and its original artwork and game data
belong to **Sega**. See the [asset provenance notice](G33kShell.Desktop/Assets/GoldenAxe/NOTICE.txt)
for the reference revision and file sources.

This version also corrects rider positioning on the Chicken Leg mount: rider
offsets include the mount's per-frame animation offsets, keeping characters seated
over its body instead of too far forward while standing, walking and attacking.

`screensaver goldenaxe` plays a native C# recreation of all five main arcade areas with
skin-tinted monochrome graphics. Use `screensaver goldenaxe_color` for color.
Soft ground shadows, softened sprite outlines, campfire effects, drifting ground fog,
glowing dragon breath and fireballs with heat haze are enabled by default in color and mono.
Use `screensaver goldenaxe_color` for autoplay or `screensaver goldenaxe_play_color` to play.
The renderer also adds distance shading and soft ground shadows near ledges in the main areas.
Add `_ao` to inspect the terrain shading mask. White means no adjustment and darker tones show stronger shading.
For short effect clips, run `python3 tools/capture-golden-axe.py --output ~/Movies/GoldenAxe --gif` with the .NET SDK and FFmpeg installed.

For repeatable final-battle CPU profiling, set `G33K_GOLDEN_AXE_PROFILE_DIR` to an output directory and run
`dotnet test G33kShell.Desktop.Tests -c Release --filter FullyQualifiedName~GoldenAxeProfileTests.ProfileFinalBattle`.
The opt-in profile warms up and replays all three heroes with effects on/off, reporting simulation,
rendering stages, pixel conversion, allocations and screenshots in `profile.json` and PNG files.
It uses the final-area spawns with Death Adder alive, protects the hero and raises enemy health for repeatability.
Asset loading and desktop scaling/presentation are excluded; renderer timing callbacks are inactive during normal play.
Use `--scene skeleton-dust` for the eagle-area skeleton emergence, including the
mud mound, dust particles and wind; `--scene landing-dust` captures a knockdown.
The script writes MP4 clips and, with `--gif`, looping GIFs. On Windows, for example:
`python tools/capture-golden-axe.py --scene skeleton-dust --output ~/Videos/GoldenAxe --gif`.
It finds FFmpeg on PATH or uses an installed `imageio-ffmpeg` package; `--ffmpeg`
can select a specific executable. No encoder is downloaded automatically.
Weapon-trail previews are available as `weapon-swipes-ax`, `weapon-swipes-tyris`,
`weapon-swipes-gilius` and `weapon-swipes-enemies`, showing swings in both directions.
The `turtle-eye` and `eagle-eye` scenes show the original three-frame eye movements
at the Turtle Village exit and castle approach. These follow the scenery as the
camera scrolls and are hidden during camp. The turtle matches the MAME set-6
position, palette and 100/10/10/10-tick gaze sequence; eagle timing remains approximate.
Dragon fireballs and flame breath leave smoke that rises, expands and fades after
the attack has passed. Use `dragon-fire` and `dragon-flame` to preview them.
The `running-dust` scene shows smaller ground-level puffs from running and barging;
these use the same terrain tint and eagle-area wind as landing dust.
The `stone-bodies` scene shows defeated enemies settling into their original prone
poses and remaining as stone-colored scenery, with contact shadows. Over two seconds,
their normal colors fade into the MAME-confirmed original palettes: 0x97 warm stone
for humanoids and 0x72 ochre bone for skeletons.
Bodies remain until scrolled off or the area/camp changes and do not block progression.
The two-second fade is an intentional enhancement over the arcade's abrupt palette swap.
The `castle-camera` capture demonstrates the measured Stage 9 lower camera limit
of -736 at camera X -632 (25 more pixels than the original extracted path).
The Stage 5 village-cliff sample at X -666 also reaches its measured limit of -749.
These are targeted camera-path corrections; player collision and unmeasured route
limits are unchanged. The `eagle-sunset` capture previews the Stage 7 sky's
progress-driven blue-to-peach blend from 5055 to 5107, using the MAME reference
colors in two sky palette groups. Intermediate colors are interpolated; the arcade's
individual color-step thresholds and later return to blue still need measurement.
Eight reserved palette entries isolate the sky from actors, terrain and HUD colors,
while retaining monochrome rendering and scene fades.
The eagle sky now uses a complete 512x256 repeating cloud map recovered from
Dean's MAME hardware-page capture, reusing all 120 original atlas tiles. Clouds
scroll independently of the foreground, including while the player stands still.
The captured acceleration and vertical motion match the 642-sample reference;
the native clock starts at the first captured progress (4990), then continues the
last measured rate of 6 pixels left and 1.5 pixels down per tick. The exact earlier
activation and any later change in direction/rate still need MAME verification.
Use `--scene eagle-clouds` for a ten-second stationary-camera preview.
It renders the shadows, campfire, dragon fire and magic scenes headlessly to silent MP4s, with optional looping GIF previews.
Use `--scene campfire` to regenerate a single clip.
Use `--ffmpeg /path/to/ffmpeg` to select a specific encoder executable.
The bundled arcade opening card fades in, holds for two seconds, then fades through black
into the title. The title displays for 2½ seconds before play starts automatically.
The four bundled map recordings play after their corresponding camps. To override any of these
assets, place `intro.png`, `title.png`, or `map1.gmap` through `map4.gmap` in the
`GoldenAxe` folder in the application's user settings directory. Use an opaque 320×224 PNG
with at most 256 colors for either card.
The map encoder uses one optimized 256-color palette per recording, stores deltas only across
the changed scanlines, and retains the source timing.
To convert local MP4 recordings, install Python's `av`, `brotli`, and `numpy` packages and run
`python tools/encode-golden-axe-maps.py --output <settings-directory>/GoldenAxe <map-files>`.
Add `_play` to take control: `screensaver goldenaxe_play_color` (color) or
`screensaver goldenaxe_play` (mono). Use the arrow keys to move, hold **Ctrl** to
attack, press **Tab** for magic and **Space** to jump. Magic and jumping work on foot
and in the saddle. Press
**Shift+Space** to dismount. **Ctrl+Space** performs a
back attack; double-tap Left or Right to run, then attack for a shoulder charge.
Ridden chickens and both dragons also run and charge using these controls, leaving
foot dust and knocking down each enemy struck once. Mounted charges currently use
the original run poses with an approximate body-impact hitbox and timing.
**Escape** returns to the console. Held keys clear when the window loses focus.
Press **1** for Ax Battler, **2** for Tyris Flare, or **3** for Gilius Thunderhead
(the dwarf). Selection is saved across runs and app restarts. A switch preserves
health and position and waits for an active move or spell to finish. Each hero uses
their own attacks, magic, portrait and mounted animations.
Clearing each area continues through the campaign, preserving the selected hero,
health, remaining lives and potions. Fresh starts and respawns grant one magic potion.
Tyris's seven-, eight- and nine-potion casts now use the original fire pillars,
mirrored molten core/rock rim, and giant dragon with multipart breath. Frame families,
palettes and effect windows follow the MAME timing handoff; trajectories, the larger
potion fan and entrance/exit motion are native reconstructions pending a full trace.
The dragon's piece alignment matches the supplied arcade still; its shallow right-to-left
sweep follows Dean's recollection and remains to be checked against a motion trace. Flames cast warm light
and emit smoke that rises and fades after the spell. Existing recordings for one
through six potions are preserved; the other characters' higher potion counts still
need the separate magic audit. Review `tyris-seven`, `tyris-eight` and `tyris-dragon`
with the capture tool.
Four playable camp bonus rounds separate the five main areas, so the arcade stage
counter advances through stages 1 to 9. To start directly
in Turtle Village, use `screensaver goldenaxe_stage3_play_color` (omit `_play` for
the coded player, or `_color` for monochrome). Use `screensaver goldenaxe_stage5_play_color`
for the third area, with its broken bridge, red dragon and armoured knight.
`screensaver goldenaxe_stage7_play_color` and `screensaver goldenaxe_stage9_play_color`
start the last two areas directly, including skeletons, stronger soldier variants,
additional knights and the final Death Adder fight with his skeleton guards.
The `_stage1` through `_stage9` selectors match the HUD; stages 2, 4, 6 and 8
start directly in the intervening camps. These replace the old `_areaN` selectors.
On the eagle's back in the fourth area, original feather sprite 1042 drifts in
front of the finished scene, at half size with a muted grey-brown palette (0x87).
Horizontal drift is three times the initial native approximation.
The effect works in color and mono and stops during camp; exact arcade scaling,
drift timing, placement and palette selection remain unverified.
After recovering from a knockdown, Death Adder winds up a cast and sends a lightning
bolt along the floor. It uses the extracted casting frames and projectile damage
boxes, deals one health bar of damage, and can be jumped. The recovery trigger,
travel speed and gentle depth tracking are approximations. The coded player attempts
to jump incoming bolts.

At camp, carried potions are visible beside the sleeping hero from the start.
Blue thieves steal them before the hero wakes to give chase; hitting them drops
potions to recover. Green thieves drop food that restores one health bar, up to
full health. Thieves carry a finite supply and run off when empty or when time expires.
The night palettes and camp spawn lists come from the extracted data.
Camp placements are adjusted to keep the group above the HUD. The coded player
chases thieves, prioritises food when hurt, and carries the collected supplies into
the next area. The round ends once the thieves have left and remaining drops have
been collected or expired. Manual controls work as usual;
magic is disabled during camp.

Without `_play`, the coded player controls the selected hero, fights the enemy waves and Bad Brothers,
collects potions, casts magic and rides Chicken Leg, the fire-breathing blue dragon or
the red dragon, which shoots travelling fireballs. Riderless beasts sit in their extracted resting pose for
five seconds, then run off if nobody mounts them. The coded player waits at the
screen edge while scrolling is locked, then resumes when the GO prompt appears.
Turtle Village includes its scrolling camera path, Amazon enemies and original spawn waves. Close attacks on ordinary
soldiers become knee-and-throw combos, and the hero uses back attacks against enemies
behind them. Dropped potions arc onto the ground; spell damage follows the peak
of the recorded effect. Grounded enemies collapse when the spell ends, then recover
or die according to their remaining health. The hero mixes walking, running, running jumps and shoulder
charges. Running jumps cross the broken bridge; missing a jump costs a life and
respawns the hero on safe ground. The HUD includes the hero's portrait, three health
bars and potion icons aligned within their slots. The MAGIC heading uses original
letter tiles, and filled slots pulse brighter red while empty slots remain dark;
the portrait and HUD potion use the original text tiles. Invulnerability and brief
magic-animation gaps deliberately pulse opacity without disappearing.
Gilius lightning surrounds the caster and visible enemies with the original cage
sprites. Victims blend between red and white at five cycles per second. Enemy
placement and timing across spell strengths reuse the first captured cage sequence
and still need detailed MAME verification.
Waiting potions emit subtle blue motes; collecting one releases a sparkle burst
and sends three glints towards its HUD slot. Collected potions and food fade out
over a quarter of a second, with their rewards applied immediately. Stolen camp
potions use the same fade without pickup sparkles. Food has no expiry timer. These added effects follow the selected
color or monochrome palette, and full-meter pickups only produce the local burst.
The visual camera eases towards the arcade camera with roughly 0.6 seconds of
settling. Golden Axe's finished gameplay image uses a 1.5 power curve for richer
shadows and midtones, including lighting and HUD colors, without changing the source
artwork or other screensavers. Camera movement uses bounded lag and whole-pixel
positions. Gameplay progression and stage
locks continue to use the original camera. Jumps leave the vertical view steady unless
the hero approaches the top; ledge and terrain-boundary adjustments ease vertically
by at most two pixels per tick. Solid-floor movement can reach 12 pixels from the
bottom of the screen, while camps retain their more generous HUD clearance.
Each area and camp fades in and fades out. Defeating Death Adder and clearing his
guards shows a victory message. Completing the campaign or losing all lives starts
another run after a short pause; random mode receives a cycle event then.
The coded player walks through cleared scenery and waits when scrolling is locked.
Magic decisions anticipate nearby scripted enemies within the current scroll stop,
including delayed doorway entrances, and wait up to two seconds for the group to become
visible and hittable while continuing to fight. Low health plus an imminent nearby strike
can override that wait. Tyris saves for her second spell level outside boss fights and
emergencies. Death Adder's recovery and casting animation warn the controller to make
space for lightning; jump timing accounts for the hero's crouch delay and mounts' immediate
take-off. All three characters complete the fixed-seed progression tests, though these
heuristics do not guarantee every autonomous playthrough.
It keeps running for gap crossings and selected approaches to distant opponents.
Villagers use five original running sequences rather than two shared appearances.
MAME confirms six villagers in the opening Turtle Village crowd and nine in the
later crowd. Civilian speeds now use the spawn parameter's low byte divided by 16,
matching the measured per-actor variation instead of a uniform 1.8 pixels/tick.
The opening and late crowds now approach their measured depth lanes at one pixel
per tick, using the signed high parameter byte as a lane offset. Those lane centres
come from the captured routes; their selection elsewhere remains unverified.
The opening injured man uses poses 611 then 612 and stays on the ground; his soldier
partner uses the original running script. The later waiting child uses poses 737/738
and palette 0x4c before switching to its running sequence. Its proximity-based release
is provisional because the MAME trace does not identify the exact trigger.
No extra crowd members were added. Capture `village-opening` or `village-routing`
with `tools/capture-golden-axe.py` to review the two crowds.
Fleeing villagers now route around height-map walls and pits instead of passing through buildings.
The coded player routes around holes and rocks. Potion thieves now check terrain,
jump onto low ledges and turn away from walls or holes instead of walking through rock.
Stepping off a ledge now falls under gravity, including while mounted; downward
movement keeps the hero's feet above the HUD. Ordinary enemies seek out unoccupied
Chicken Legs and dragons and can reclaim them after being knocked off. They
wait about one second within mounting range before climbing aboard; getting hit
interrupts the attempt.
Riderless Chicken Legs and dragons wait five seconds, then run towards the nearer
screen edge and leave. Remounting resets that wait; fleeing beasts cannot be mounted.
A flashing **GO!** sword prompt uses original sprite frame 646 while the route ahead
is clear and scrolling is available. Its gold palette, placement and flashing cadence
remain approximations pending comparison with a set-6 capture.
Palette fades use a private display copy, so death/restart and area
transitions preserve the original artwork colors.

The six skeleton spawn points on the eagle's mud platforms show the original mound
(frame 853), then open using frames 853–857 and rise animation 0x24a00. Opened mounds
remain behind. Stone-floor skeletons arrive normally. Mound skeletons use the white-bone
palette. The ground and skeleton share their anchor and advance together every eight
ticks, with normal AI starting at tick 48, matching the MAME trace. Which individual
spawns use this entrance, including Stage 9, still needs verification.
Landing and emergence dust particles rise, disperse and fade over 2–2.5 seconds,
with gentle leftward drift where the eagle's wind blows.
Magic shakes the scene after the casting potions disappear until the spell ends,
scaling with spell strength and easing in and out while the HUD stays fixed.
Matching hero sword/axe swings, Heninger mace swings and skeleton sword swings use
original swipe sprites around their active attack frame. Kicks, stabs and charges
do not receive a sweep. Trail attachments, timing and silver/gold palettes are
approximations pending arcade comparison; the effects do not change attack damage or reach.

The set-6 assets are bundled in `G33kShell.Desktop/Assets/GoldenAxe` and included
in build and publish output. See `NOTICE.txt` there for their source and Sega attribution;
the game assets are not covered by the source-code license. Rendering uses extracted sprites, animation timing, tile layers,
height data and spawn positions with a stable 256-color palette. Simulation runs
at 60 Hz independently of rendering; the coded input policy is separate from it.
Enemy decisions and some behaviours remain approximations. The original map and
ending cinematics are not reproduced; sound, music and trained AI remain future work.
