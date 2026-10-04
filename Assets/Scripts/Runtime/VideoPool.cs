using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace Interactive360
{
    /// <summary>
    /// A small pool of VideoPlayers. One plays the current scene while the others pre-buffer the clips
    /// the viewer might branch to, so the cut to a chosen branch happens without a decode stall.
    /// </summary>
    public sealed class VideoPool : MonoBehaviour
    {
        public sealed class Slot
        {
            public VideoPlayer Player;
            public AudioSource Audio;
            public RenderTexture Texture;
            public string Url;
            public string Error;
            public bool IsPrepared => Player != null && Player.isPrepared;
            public bool IsPlaying => Player != null && Player.isPlaying;
            public bool IsFree => Url == null;
            public override string ToString() => Player != null ? Player.gameObject.name : "Slot";
        }

        [Tooltip("Current clip + preloaded branches. 2 is the minimum for seamless cuts; 3 lets both options of a binary choice pre-buffer.")]
        [Range(1, 4)] public int slotCount = 3;

        [Tooltip("Render texture size used until the real clip dimensions are known.")]
        public int fallbackWidth = 2048;
        public int fallbackHeight = 1024;

        [Range(0f, 1f)] public float volume = 1f;

        /// <summary>Raised when a slot's clip reaches its end.</summary>
        public event Action<Slot> Finished;

        /// <summary>Raised when a slot finishes preparing (texture is now valid and sized to the clip).</summary>
        public event Action<Slot> Prepared;

        readonly List<Slot> slots = new List<Slot>();
        public IReadOnlyList<Slot> Slots => slots;

        void Awake()
        {
            for (int i = 0; i < slotCount; i++) slots.Add(CreateSlot(i));
        }

        Slot CreateSlot(int index)
        {
            var go = new GameObject("VideoSlot" + index);
            go.transform.SetParent(transform, false);

            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.volume = volume;

            var vp = go.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.isLooping = false;
            vp.skipOnDrop = true;
            vp.waitForFirstFrame = true;
            vp.source = VideoSource.Url;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.aspectRatio = VideoAspectRatio.Stretch;
            vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
            vp.controlledAudioTrackCount = 1;
            vp.EnableAudioTrack(0, true);
            vp.SetTargetAudioSource(0, audio);

            var slot = new Slot { Player = vp, Audio = audio, Texture = NewTexture(fallbackWidth, fallbackHeight) };
            vp.targetTexture = slot.Texture;

            vp.prepareCompleted += p => OnPrepared(slot);
            vp.loopPointReached += p => Finished?.Invoke(slot);
            vp.errorReceived += (p, msg) =>
            {
                slot.Error = msg;
                Debug.LogError("[VideoPool] " + slot + " error for '" + slot.Url + "': " + msg);
            };
            return slot;
        }

        static RenderTexture NewTexture(int w, int h)
        {
            var rt = new RenderTexture(Mathf.Max(16, w), Mathf.Max(16, h), 0, RenderTextureFormat.ARGB32)
            {
                name = "Video " + w + "x" + h,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            rt.Create();
            return rt;
        }

        void OnPrepared(Slot slot)
        {
            var p = slot.Player;
            int w = (int)p.width, h = (int)p.height;
            if (w > 0 && h > 0 && (slot.Texture.width != w || slot.Texture.height != h))
            {
                var fresh = NewTexture(w, h);
                p.targetTexture = fresh;
                slot.Texture.Release();
                Destroy(slot.Texture);
                slot.Texture = fresh;
            }
            Prepared?.Invoke(slot);
        }

        /// <summary>Slot already holding this URL (prepared, preparing or playing), or null.</summary>
        public Slot Find(string url)
        {
            if (url == null) return null;
            foreach (var s in slots) if (s.Url == url) return s;
            return null;
        }

        /// <summary>
        /// Returns a slot preparing <paramref name="url"/>: an existing one if present, otherwise a free or
        /// evictable slot (never a playing slot, never one whose URL is in <paramref name="protect"/>).
        /// Returns null when every slot is busy.
        /// </summary>
        public Slot Acquire(string url, ICollection<string> protect = null)
        {
            if (string.IsNullOrEmpty(url)) return null;
            var existing = Find(url);
            if (existing != null) return existing;

            Slot victim = null;
            foreach (var s in slots) if (s.IsFree) { victim = s; break; }
            if (victim == null)
                foreach (var s in slots)
                    if (!s.IsPlaying && (protect == null || !protect.Contains(s.Url))) { victim = s; break; }
            if (victim == null) return null;

            Assign(victim, url);
            return victim;
        }

        void Assign(Slot slot, string url)
        {
            slot.Player.Stop();
            slot.Url = url;
            slot.Error = null;
            slot.Player.url = url;
            slot.Player.Prepare();
        }

        /// <summary>Stops the slot and frees its decoder so it can take another clip.</summary>
        public void Release(Slot slot)
        {
            if (slot == null) return;
            slot.Player.Stop();
            slot.Url = null;
            slot.Error = null;
        }

        public void ReleaseAllExcept(ICollection<string> keepUrls)
        {
            foreach (var s in slots)
                if (!s.IsFree && !s.IsPlaying && (keepUrls == null || !keepUrls.Contains(s.Url))) Release(s);
        }

        public void SetVolume(float v)
        {
            volume = Mathf.Clamp01(v);
            foreach (var s in slots) s.Audio.volume = volume;
        }

        void OnDestroy()
        {
            foreach (var s in slots)
            {
                if (s.Texture != null) { s.Texture.Release(); Destroy(s.Texture); }
            }
        }
    }
}
