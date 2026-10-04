using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Interactive360
{
    /// <summary>
    /// The interactive-film state machine: loads the story, plays nodes through the VideoPool, opens the
    /// choice window at the right time, applies flags, resolves redirects, preloads likely branches and
    /// cuts to the chosen one when the current clip ends (or immediately, if the story asks for it).
    /// </summary>
    public sealed class StoryPlayer : MonoBehaviour
    {
        [Header("Setup")]
        [Tooltip("Relative to StreamingAssets.")]
        public string storyPath = "Story/story.json";
        public VideoPool pool;
        public PanoramaSkybox skybox;
        public ChoiceUI ui;
        public Camera head;

        [Header("Behaviour")]
        [Tooltip("Seconds to fade to black and back around a cut. 0 = hard cut.")]
        public float transitionFade = 0f;
        public float prepareTimeout = 30f;
        public bool verbose = true;

        public Story Story { get; private set; }
        public StoryState State { get; } = new StoryState();
        public StoryNode Current { get; private set; }

        public event Action<StoryNode> NodeEntered;
        public event Action<StoryNode, StoryChoice> ChoiceMade;

        VideoPool.Slot slot;
        readonly List<StoryChoice> visible = new List<StoryChoice>();
        bool choicesShown, chosen, clipEnded, transitioning, holding;
        string pendingNext;
        double choiceStart, choiceEnd, clipLength;

        IEnumerator Start()
        {
            if (head == null) head = Camera.main;
            if (pool == null) pool = GetComponent<VideoPool>();
            if (skybox == null) skybox = GetComponent<PanoramaSkybox>();
            if (ui == null) ui = GetComponent<ChoiceUI>() ?? ChoiceUI.Instance;
            if (pool == null || skybox == null || ui == null)
            {
                Debug.LogError("[StoryPlayer] Missing VideoPool / PanoramaSkybox / ChoiceUI reference.");
                yield break;
            }

            ui.ShowStatus("Loading…");

            Story story = null; string error = null;
            yield return StoryLoader.Load(storyPath, (s, e) => { story = s; error = e; });
            if (story == null)
            {
                Debug.LogError("[StoryPlayer] " + error);
                ui.ShowStatus("Could not load story\n" + error);
                yield break;
            }

            bool fatal = false;
            foreach (var issue in StoryValidation.Validate(story))
            {
                if (issue.StartsWith("ERROR")) { fatal = true; Debug.LogError("[Story] " + issue); }
                else Debug.LogWarning("[Story] " + issue);
            }
            if (fatal) { ui.ShowStatus("Story has errors.\nSee the log."); yield break; }

            Story = story;
            pool.Finished += OnSlotFinished;
            ui.Chosen += OnChosen;
            if (verbose) Debug.Log("[Story] Loaded '" + story.Title + "' with " + story.Nodes.Count + " nodes.");

            yield return EnterNode(story.Start);
        }

        void OnDestroy()
        {
            if (pool != null) pool.Finished -= OnSlotFinished;
            if (ui != null) ui.Chosen -= OnChosen;
        }

        /// <summary>Clear flags and start the film again.</summary>
        public void Restart()
        {
            if (Story == null) return;
            State.Clear();
            StartCoroutine(Transition(Story.Start));
        }

        // ---------------------------------------------------------------- node entry

        string Resolve(string id, StoryState state)
        {
            for (int guard = 0; guard < 16; guard++)
            {
                if (!Story.TryGetNode(id, out var node)) return id;
                string target = null;
                foreach (var r in node.Redirects)
                    if (state.Matches(r.When)) { target = r.Goto; break; }
                if (string.IsNullOrEmpty(target) || target == id) return id;
                id = target;
            }
            return id;
        }

        IEnumerator EnterNode(string requestedId)
        {
            transitioning = true;

            string id = Resolve(requestedId, State);
            if (!Story.TryGetNode(id, out var node))
            {
                Debug.LogError("[Story] Unknown node '" + id + "'");
                ui.ShowStatus("Unknown scene\n" + id);
                transitioning = false;
                yield break;
            }
            if (node.ResetState) State.Clear();

            var url = StoryLoader.ClipUrl(node.Clip);
            var protect = new HashSet<string> { url };
            var next = pool.Acquire(url, protect);
            if (next == null) { pool.ReleaseAllExcept(protect); next = pool.Acquire(url, protect); }
            if (next == null)
            {
                Debug.LogError("[Story] No free video slot for '" + node.Clip + "'");
                transitioning = false;
                yield break;
            }

            float t0 = Time.realtimeSinceStartup;
            bool showedLoading = false;
            while (!next.IsPrepared && next.Error == null && Time.realtimeSinceStartup - t0 < prepareTimeout)
            {
                if (!showedLoading && Time.realtimeSinceStartup - t0 > 0.5f) { ui.ShowStatus("Loading…"); showedLoading = true; }
                yield return null;
            }
            if (!next.IsPrepared)
            {
                Debug.LogError("[Story] Clip failed to prepare: " + node.Clip + " " + next.Error);
                ui.ShowStatus("Could not load clip\n" + node.Clip);
                pool.Release(next);
                transitioning = false;
                yield break;
            }

            if (transitionFade > 0f && slot != null) yield return Fade(1f, 0f, transitionFade);

            var old = slot;
            slot = next;
            Current = node;
            choicesShown = chosen = clipEnded = holding = false;
            pendingNext = null;
            visible.Clear();

            skybox.SetTexture(slot.Texture, node.Layout);
            slot.Player.Play();
            if (old != null && old != slot) pool.Release(old);

            clipLength = slot.Player.length;
            if (clipLength <= 0 && slot.Player.frameRate > 0) clipLength = slot.Player.frameCount / (double)slot.Player.frameRate;
            if (clipLength <= 0) clipLength = 10;

            foreach (var c in node.Choices) if (State.Matches(c.When)) visible.Add(c);
            if (visible.Count > 0)
            {
                choiceStart = node.ChoicesAt.HasValue ? node.ChoicesAt.Value : Math.Max(0, clipLength - Story.DefaultChoiceSeconds);
                choiceStart = Math.Min(Math.Max(0, choiceStart), clipLength);
                double duration = node.ChoiceSeconds.HasValue ? node.ChoiceSeconds.Value : clipLength - choiceStart;
                choiceEnd = Math.Min(clipLength, choiceStart + Math.Max(0.5, duration));
            }

            if (transitionFade > 0f) yield return Fade(0f, 1f, transitionFade); else skybox.SetExposure(1f);
            ui.HideStatus();
            if (node.Ending) ui.ShowStatus(node.Title, 2.5f);

            if (verbose) Debug.Log("[Story] ▶ " + node.Id + "  (" + clipLength.ToString("0.0") + "s, choices at " + (visible.Count > 0 ? choiceStart.ToString("0.0") : "-") + ")  state " + State);
            NodeEntered?.Invoke(node);
            transitioning = false;

            PreloadCandidates(url);
        }

        void PreloadCandidates(string currentUrl)
        {
            var wanted = new List<string>();
            void Add(string nodeId, StoryState predicted)
            {
                var target = Resolve(nodeId, predicted);
                if (!Story.TryGetNode(target, out var n)) return;
                var u = StoryLoader.ClipUrl(n.Clip);
                if (u != null && u != currentUrl && !wanted.Contains(u)) wanted.Add(u);
            }

            if (visible.Count > 0)
            {
                int d = DefaultIndex();
                var predicted = State.Clone(); predicted.Apply(visible[d].Set);
                Add(visible[d].Goto, predicted);
                for (int i = 0; i < visible.Count; i++)
                {
                    if (i == d) continue;
                    predicted = State.Clone(); predicted.Apply(visible[i].Set);
                    Add(visible[i].Goto, predicted);
                }
            }
            else if (!string.IsNullOrEmpty(Current.Next))
            {
                Add(Current.Next, State);
            }

            var protect = new HashSet<string>(wanted) { currentUrl };
            pool.ReleaseAllExcept(protect);
            foreach (var u in wanted)
                if (pool.Acquire(u, protect) == null) break;
        }

        int DefaultIndex() => visible.Count == 0 ? 0 : Mathf.Clamp(Current.DefaultChoice, 0, visible.Count - 1);

        // ---------------------------------------------------------------- per frame

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame && Story != null) { Restart(); return; }

            if (Current == null || slot == null || transitioning) return;
            double t = slot.Player.time;

            if (visible.Count > 0 && !choicesShown && t >= choiceStart)
            {
                choicesShown = true;
                var labels = new List<string>(visible.Count);
                foreach (var c in visible) labels.Add(c.Label ?? "…");
                ui.Show(Current.Prompt, labels, head != null ? head.transform : null);
            }

            if (choicesShown && !chosen)
            {
                if (Current.HoldLastFrame) ui.SetProgress(-1f);
                else
                {
                    double window = Math.Max(0.01, choiceEnd - choiceStart);
                    ui.SetProgress((float)(1.0 - (t - choiceStart) / window));
                    if (t >= choiceEnd - 0.05 || clipEnded) Choose(DefaultIndex(), true);
                }
            }

            // Fallback end detection in case loopPointReached does not fire.
            if (!clipEnded && !holding && !slot.Player.isPlaying && slot.Player.frameCount > 0 &&
                slot.Player.frame >= 0 && (ulong)slot.Player.frame >= slot.Player.frameCount - 1)
                clipEnded = true;

            if (clipEnded) HandleClipEnd();
        }

        void OnSlotFinished(VideoPool.Slot s)
        {
            if (s == slot) clipEnded = true;
        }

        void HandleClipEnd()
        {
            if (transitioning) return;

            if (choicesShown && !chosen)
            {
                if (Current.HoldLastFrame)
                {
                    if (!holding) { holding = true; slot.Player.Pause(); }
                    return;
                }
                Choose(DefaultIndex(), true);
            }

            string target = pendingNext ?? Current.Next;
            if (string.IsNullOrEmpty(target))
            {
                if (!holding)
                {
                    holding = true;
                    slot.Player.Pause();
                    ui.ShowStatus("THE END");
                    if (verbose) Debug.Log("[Story] Finished at '" + Current.Id + "'. Press R to restart.");
                }
                return;
            }
            StartCoroutine(Transition(target));
        }

        // ---------------------------------------------------------------- choices

        void OnChosen(int index) => Choose(index, false);

        void Choose(int index, bool automatic)
        {
            if (chosen || Current == null || index < 0 || index >= visible.Count) return;
            chosen = true;

            var choice = visible[index];
            State.Apply(choice.Set);
            pendingNext = choice.Goto;
            ui.MarkSelected(index);
            ChoiceMade?.Invoke(Current, choice);
            if (verbose) Debug.Log("[Story] " + (automatic ? "auto" : "chose") + " '" + choice.Label + "' → " + choice.Goto + "  state " + State);

            // Make sure the branch we are now committed to is buffering.
            var target = Resolve(pendingNext, State);
            if (Story.TryGetNode(target, out var n))
                pool.Acquire(StoryLoader.ClipUrl(n.Clip), new HashSet<string> { slot.Url });

            bool cut = Current.CutOnChoice ?? Story.CutOnChoice;
            if (cut || holding) StartCoroutine(Transition(pendingNext));
            else StartCoroutine(HideUiAfter(0.9f));
        }

        IEnumerator HideUiAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (chosen && !transitioning) ui.Hide();
        }

        IEnumerator Transition(string target)
        {
            if (transitioning) yield break;
            transitioning = true;
            ui.Hide();
            yield return EnterNode(target);
        }

        IEnumerator Fade(float from, float to, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float e = Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds));
                skybox.SetExposure(e);
                pool.SetVolume(e);
                yield return null;
            }
            skybox.SetExposure(to);
            pool.SetVolume(to);
        }
    }
}
