# 360 Interactive Player

An interactive-film player for **Meta Quest** that plays 360° video with timed, branching
choices in the style of *Black Mirror: Bandersnatch*. The viewer looks around a scene, a
choice panel appears near the end of a clip, and the story continues down whichever branch
they pick (or a default if they don't). Flags persist across scenes, so earlier decisions can
change later ones, and stories can loop back on themselves.

> **Status: builds, headset test pending.** The story format, sample story, placeholder clips, the
> full C# runtime (video engine, choice UI, XR input) and a code-driven project bootstrap are in
> the repo, and the Quest APK builds cleanly from the command line. It has not yet been run on a
> device. See [Roadmap](#roadmap).

---

## Contents

| Path | What it is |
| --- | --- |
| `Assets/StreamingAssets/Story/story.json` | The sample story graph *The Tape*: 10 scenes, 6 choice points, 4 endings, one loop |
| `Assets/StreamingAssets/Clips/*.mp4` | 10 placeholder equirectangular clips (2048×1024, H.264, 6–8 s each) |
| `Assets/Scripts/Runtime/` | The player (see [Runtime scripts](#runtime-scripts)) |
| `Assets/Scripts/Editor/` | `ProjectBootstrap` (settings, XR, scene, APK build) and `StoryTools` (validator) |
| `tools/make_placeholder_clips.py` | Regenerates the placeholder clips from `story.json` with ffmpeg |
| `tools/unity-setup.ps1` | Headless Unity run that configures the project and writes the scene |
| `tools/unity-build.ps1` | Headless APK build, then `adb install` if a Quest is connected |
| `Packages/manifest.json` | Unity 6 package set: OpenXR, XR Management, Input System, UGUI, Newtonsoft JSON |
| `ProjectSettings/ProjectVersion.txt` | Pins the editor to Unity 6000.0.25f1 |

### Runtime scripts

| Script | Role |
| --- | --- |
| `StoryPlayer` | The state machine: enters nodes, times the choice window, applies flags, resolves redirects, preloads branches, cuts |
| `StoryModel` / `StoryState` / `StoryLoader` / `StoryValidation` | JSON schema, flag store with `+1` and `>=` semantics, StreamingAssets loading, graph checks |
| `VideoPool` | N `VideoPlayer`s with render textures; one plays while the others pre-buffer likely next clips |
| `PanoramaSkybox` | Points the `Skybox/Panoramic` material at the active video texture; mono / over-under / side-by-side |
| `ChoiceUI` / `ChoiceButton` / `UiFactory` | World-space choice panel with timer bar, built at runtime, re-centres if you look away |
| `PointerBase` → `GazePointer`, `ControllerPointer` | Raycast hover/select against the buttons: controller laser + trigger, gaze + trigger, gaze dwell |
| `HeadTracking` / `DesktopLook` | HMD pose via `TrackedPoseDriver`; right-mouse-drag look-around when no HMD is active |

---

## How it will work

```
story.json ──► Story engine ──► VideoPlayer pool ──► RenderTexture ──► Skybox/Panoramic
                   │                (current clip + preloaded branches)       (mono / stereo)
                   │
                   └─► Choice panel (world-space UI, timer bar)
                           ▲
                 controller ray · gaze + trigger · gaze dwell
```

- **Playback:** Unity's `VideoPlayer` decodes each clip into a render texture that drives a
  `Skybox/Panoramic` material. Mono, over-under and side-by-side stereo layouts are selected
  per scene.
- **Seamless branching:** while a clip plays, the clips for its possible next scenes are
  prepared on spare players, so the cut to the chosen branch happens without a stall.
- **Choices:** a world-space panel appears at the configured time, facing the viewer, with a
  countdown bar. If nothing is picked, the scene's default choice is taken. By default the
  current clip finishes before the branch starts (Bandersnatch behaviour); a scene can opt
  into cutting immediately.
- **Input:** controller pointing with trigger, head gaze with trigger, or gaze dwell when no
  controllers are tracked. A mouse-look fallback is planned for testing in the editor.
- **State:** choices can set flags; scenes can redirect on entry or hide choices based on
  those flags.

---

## Story format

Everything about the film lives in one JSON file so writers can edit it without touching Unity.

```json
{
  "title": "The Tape",
  "start": "intro",
  "defaultChoiceSeconds": 6.0,
  "cutOnChoice": false,
  "nodes": {
    "intro": {
      "title": "INTRO",
      "clip": "Clips/intro.mp4",
      "layout": "mono",
      "choicesAt": 4.0,
      "defaultChoice": 0,
      "choices": [
        { "label": "Open the door",        "goto": "door" },
        { "label": "Look out the window",  "goto": "window", "set": { "sawCity": true } }
      ]
    },
    "basement": {
      "clip": "Clips/basement.mp4",
      "redirect": [ { "when": { "hasTape": true }, "goto": "ending_true" } ],
      "choices": [
        { "label": "Smash the mirror", "goto": "ending_mirror" },
        { "label": "Walk away",        "goto": "intro", "set": { "loops": "+1" } }
      ]
    },
    "ending_true": {
      "clip": "Clips/ending_true.mp4",
      "ending": true,
      "resetState": true,
      "next": "intro"
    }
  }
}
```

### Story-level fields

| Field | Meaning |
| --- | --- |
| `start` | Id of the first node |
| `defaultChoiceSeconds` | If a node has no `choicesAt`, the panel opens this many seconds before the clip ends |
| `cutOnChoice` | `false` = finish the current clip after a choice, `true` = cut at once |

### Node fields

| Field | Meaning |
| --- | --- |
| `clip` | Path relative to `StreamingAssets`, or an `http(s)://` URL |
| `layout` | `mono`, `over-under` or `side-by-side` |
| `choicesAt` | Clip time (seconds) when the choice panel appears |
| `choiceSeconds` | How long the window stays open (default: until the clip ends) |
| `defaultChoice` | Index taken when the window closes with no input |
| `holdLastFrame` | Pause on the last frame and wait instead of taking the default |
| `choices[]` | `label`, `goto`, optional `set` (flags to write) and `when` (flags required to show it) |
| `redirect[]` | Checked on entry: first rule whose `when` matches sends the viewer to `goto` instead |
| `next` | Where to go when there are no choices |
| `ending` | Marks a finale (for UI and validation) |
| `resetState` | Clears all flags when entered |

Flag values can be booleans, numbers or strings. In `set`, `"+1"` / `"-1"` add to a counter.
In `when`, strings like `">=2"`, `"<5"` or `"!=0"` compare numerically.

### The sample story

```
intro ─┬─ door ─┬─ upstairs ─┬─ take tape ─► basement ─(hasTape)─► ENDING: THE TAPE
       │        │            └─ leave it  ─► basement ─┬─ smash ─► ENDING: SHARDS
       │        └─ basement (same as above)            └─ walk away ─► intro (loop)
       └─ window ─┬─ climb out ─► rooftop ─┬─ jump ─► ENDING: FREEFALL
                  └─ turn back ─► door     └─ wait ─► ENDING: DAWN
```

---

## Placeholder clips

The committed clips are synthetic test footage so the branching logic can be exercised before
real 360 video exists. Each one shows its scene title, a **FRONT / LEFT / BACK / RIGHT**
compass so orientation is obvious in the headset, a horizon line, a running timecode, and a
green **CHOICE WINDOW OPEN** banner from `choicesAt` onward. Each has a distinct tone so audio
routing can be checked too.

Regenerate them (requires `ffmpeg` on PATH):

```bash
python tools/make_placeholder_clips.py --width 2048 --seconds 8 --force
```

Image centre = forward (+Z) in Unity's panoramic skybox; the right edge quarter is +X.

---

## Opening and building

1. Install **Unity 6000.0.25f1** with the **Android Build Support** module (SDK, NDK, OpenJDK).
2. Either open the folder in Unity Hub and run **Interactive360 ▸ Setup Project**, or from a terminal:

   ```powershell
   .\tools\unity-setup.ps1     # configure settings + XR, write Assets/Scenes/Main.unity
   .\tools\unity-build.ps1     # build Builds\Interactive360.apk and install over adb
   ```

   Setup applies: Android · ARM64 · IL2CPP · min SDK 29 · Vulkan with GLES3 fallback · ASTC ·
   Linear colour · OpenXR with *Meta Quest Support* and the *Oculus Touch Controller* profile.
3. **Test in the editor** without a headset: press Play, hold the right mouse button to look
   around, click an option (or press Space) when the panel appears, **R** restarts.
4. **In the headset**: point a controller at an option and pull the trigger. With no controllers
   tracked, look at an option and pull the trigger, or just hold your gaze on it for 2 seconds.

---

## Roadmap

- [x] Story JSON format and sample branching story
- [x] Placeholder 360 clip generator
- [x] Unity 6 package manifest (OpenXR, Input System, UGUI, Newtonsoft)
- [x] Runtime: story engine, state/flags, video player pool with branch preloading
- [x] Runtime: panoramic skybox (mono + stereo), world-space choice UI with timer
- [x] Runtime: controller ray, gaze + trigger, gaze dwell; editor mouse-look fallback
- [x] Editor: one-click bootstrap (scene, XR settings, Android player settings), story validator
- [x] Quest APK builds headlessly (`tools/unity-build.ps1`, ~36 MB, 0 errors)
- [ ] In-headset test on a Quest
- [ ] Hand-tracking selection, subtitles, analytics of paths taken

---

## License

Not yet chosen. All placeholder media in this repo is generated and free to reuse.
