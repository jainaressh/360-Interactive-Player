"""
Generates placeholder equirectangular (360 mono) test clips for every node in story.json.

Each clip shows: the node title, a FRONT/LEFT/BACK/RIGHT compass so you can tell
orientation inside the headset, a horizon line, a running timecode, and a
"CHOICE WINDOW OPEN" banner from the node's `choicesAt` time onwards.

Usage:  python tools/make_placeholder_clips.py [--width 2048] [--seconds 8] [--force]
"""
import argparse
import json
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STORY = os.path.join(ROOT, "Assets", "StreamingAssets", "Story", "story.json")
STREAMING = os.path.join(ROOT, "Assets", "StreamingAssets")

# One background colour + audio tone per node so clips are distinguishable even without video.
PALETTE = [
    ("0x1f2a44", 330), ("0x3a2a1f", 392), ("0x1f3a2e", 440), ("0x3a1f36", 494),
    ("0x2a2a2a", 523), ("0x1f3344", 587), ("0x442a1f", 659), ("0x2a1f44", 698),
    ("0x1f4433", 784), ("0x44331f", 880), ("0x33441f", 988), ("0x441f2a", 1047),
]

FONT = "C\\:/Windows/Fonts/arialbd.ttf"  # ffmpeg needs the drive colon escaped


def esc(text: str) -> str:
    """Escape text for ffmpeg drawtext."""
    return text.replace("\\", "\\\\").replace(":", "\\:").replace("'", "\\'").replace(",", "\\,")


def build_filter(title: str, choices_at: float, width: int, height: int, ending: bool) -> str:
    cell = width // 8
    f = [
        f"drawgrid=w={cell}:h={cell}:t=2:c=white@0.18",
        f"drawbox=x=0:y={height // 2 - 2}:w={width}:h=4:color=white@0.5:t=fill",
    ]
    # Compass labels. Equirect centre = forward (+Z) in Unity's panoramic skybox.
    labels = [("FRONT", 0.5), ("RIGHT", 0.75), ("LEFT", 0.25), ("BACK", 0.0), ("BACK", 1.0)]
    for text, frac in labels:
        x = f"{int(width * frac)}-text_w/2"
        f.append(
            f"drawtext=fontfile='{FONT}':text='{text}':fontsize={height // 14}:fontcolor=white@0.6:"
            f"x={x}:y=h/2+30"
        )
    # Node title (big, centred, slightly above horizon)
    f.append(
        f"drawtext=fontfile='{FONT}':text='{esc(title)}':fontsize={height // 7}:fontcolor=white:"
        f"borderw=4:bordercolor=black@0.6:x=(w-text_w)/2:y=h/2-text_h-60"
    )
    # Running timecode on the FRONT side, below horizon
    f.append(
        f"drawtext=fontfile='{FONT}':text='%{{pts\\:hms}}':fontsize={height // 16}:fontcolor=yellow:"
        f"x=(w-text_w)/2:y=h*0.72"
    )
    if ending:
        f.append(
            f"drawtext=fontfile='{FONT}':text='ENDING':fontsize={height // 12}:fontcolor=0xff6666:"
            f"x=(w-text_w)/2:y=h*0.82"
        )
    elif choices_at is not None:
        f.append(
            f"drawtext=fontfile='{FONT}':text='CHOICE WINDOW OPEN':fontsize={height // 12}:fontcolor=0x66ff99:"
            f"x=(w-text_w)/2:y=h*0.82:enable='gte(t\\,{choices_at})'"
        )
    return ",".join(f)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--width", type=int, default=2048)
    ap.add_argument("--seconds", type=float, default=8.0)
    ap.add_argument("--ending-seconds", type=float, default=6.0)
    ap.add_argument("--fps", type=int, default=30)
    ap.add_argument("--force", action="store_true")
    args = ap.parse_args()

    with open(STORY, "r", encoding="utf-8") as fh:
        story = json.load(fh)

    width = args.width
    height = width // 2
    made = 0
    for i, (node_id, node) in enumerate(story["nodes"].items()):
        rel = node["clip"].replace("/", os.sep)
        out = os.path.join(STREAMING, rel)
        os.makedirs(os.path.dirname(out), exist_ok=True)
        if os.path.exists(out) and not args.force:
            print(f"skip  {rel} (exists)")
            continue

        ending = bool(node.get("ending", False))
        seconds = args.ending_seconds if ending else args.seconds
        colour, tone = PALETTE[i % len(PALETTE)]
        title = node.get("title", node_id.upper())
        vf = build_filter(title, node.get("choicesAt"), width, height, ending)

        cmd = [
            "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", f"color=c={colour}:s={width}x{height}:r={args.fps}:d={seconds}",
            "-f", "lavfi", "-i", f"sine=frequency={tone}:duration={seconds}",
            "-vf", vf,
            "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p",
            "-profile:v", "high", "-level", "5.1", "-g", str(args.fps),
            "-c:a", "aac", "-b:a", "96k", "-ac", "2",
            "-movflags", "+faststart", "-shortest",
            out,
        ]
        print(f"make  {rel}  ({seconds:.0f}s, {width}x{height})")
        subprocess.run(cmd, check=True)
        made += 1

    print(f"done: {made} clip(s) generated into {os.path.join(STREAMING, 'Clips')}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
