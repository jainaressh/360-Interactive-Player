# 360 Interactive Player

A Meta Quest player for 360° video told as an interactive film: timed on-screen choices
branch the story (Bandersnatch style), with flags that persist across scenes and loops.

**Stack:** Unity 6 (6000.0.25f1), OpenXR + Meta Quest Support, Input System, UGUI,
Newtonsoft JSON. Video is decoded by Unity's `VideoPlayer` into a render texture that
drives a `Skybox/Panoramic` material, so mono and stereo (over-under / side-by-side)
equirectangular footage are both supported.

## Layout

| Path | Purpose |
| --- | --- |
| `Assets/StreamingAssets/Story/story.json` | The story graph: nodes, clips, choices, flags |
| `Assets/StreamingAssets/Clips/` | Equirectangular MP4 clips referenced by the story |
| `Assets/Scripts/Runtime/` | Story engine, video pool, panorama skybox, choice UI, pointers |
| `Assets/Scripts/Editor/` | Project bootstrap (scene, XR, Android settings) and story validator |
| `tools/make_placeholder_clips.py` | Generates labelled placeholder 360 clips with ffmpeg |

## Story format

```json
{
  "title": "The Tape",
  "start": "intro",
  "defaultChoiceSeconds": 6.0,
  "nodes": {
    "intro": {
      "clip": "Clips/intro.mp4",
      "layout": "mono",
      "choicesAt": 4.0,
      "defaultChoice": 0,
      "choices": [
        { "label": "Open the door", "goto": "door" },
        { "label": "Look out the window", "goto": "window", "set": { "sawCity": true } }
      ]
    },
    "basement": {
      "clip": "Clips/basement.mp4",
      "redirect": [ { "when": { "hasTape": true }, "goto": "ending_true" } ],
      "choices": [ "..." ]
    },
    "ending_true": { "clip": "Clips/ending_true.mp4", "ending": true, "resetState": true, "next": "intro" }
  }
}
```

- `choicesAt` / `choiceSeconds` control when the choice panel appears and how long it stays.
- If the window closes with no input, `defaultChoice` is taken.
- `set` writes flags when a choice is taken (`"+1"` increments a counter).
- `redirect` rules run when a node is entered and send the viewer elsewhere based on flags.
- `when` on a choice hides it unless the flags match (supports `">=2"` style comparisons).
- `next` continues without choices; `ending` marks finales; `resetState` clears flags.

## Placeholder clips

```bash
python tools/make_placeholder_clips.py --width 2048 --seconds 8
```

Each clip shows its scene title, a FRONT / LEFT / BACK / RIGHT compass, a horizon line,
a timecode, and a banner from `choicesAt` onward so timing can be checked in-headset.

## Target settings (Quest)

Android, ARM64, IL2CPP, min SDK 29, Vulkan (GLES3 fallback), ASTC, linear colour,
OpenXR with Meta Quest Support and the Oculus Touch interaction profile.
