using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Interactive360
{
    public static class StoryLoader
    {
        /// <summary>Loads and parses a story JSON from StreamingAssets. Works on Android (jar: URLs) and desktop.</summary>
        public static IEnumerator Load(string relativePath, Action<Story, string> onDone)
        {
            var path = StreamingPath(relativePath);
            string json = null, error = null;

            if (path.Contains("://"))
            {
                using (var req = UnityWebRequest.Get(path))
                {
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success) error = req.error + " (" + path + ")";
                    else json = req.downloadHandler.text;
                }
            }
            else
            {
                try { json = File.ReadAllText(path); }
                catch (Exception e) { error = e.Message; }
            }

            Story story = null;
            if (json != null)
            {
                try { story = Parse(json); }
                catch (Exception e) { error = "Story JSON parse error: " + e.Message; }
            }
            onDone?.Invoke(story, error);
        }

        public static Story Parse(string json)
        {
            var story = JsonConvert.DeserializeObject<Story>(json);
            if (story == null) throw new InvalidDataException("empty story");
            foreach (var kv in story.Nodes)
            {
                if (kv.Value == null) throw new InvalidDataException("node '" + kv.Key + "' is null");
                kv.Value.Id = kv.Key;
                if (string.IsNullOrEmpty(kv.Value.Title)) kv.Value.Title = kv.Key;
                if (kv.Value.Choices == null) kv.Value.Choices = new System.Collections.Generic.List<StoryChoice>();
                if (kv.Value.Redirects == null) kv.Value.Redirects = new System.Collections.Generic.List<StoryRedirect>();
            }
            return story;
        }

        public static string StreamingPath(string relative)
        {
            return (Application.streamingAssetsPath + "/" + relative.TrimStart('/', '\\')).Replace('\\', '/');
        }

        /// <summary>URL for VideoPlayer. Absolute http(s)/file URLs pass through; everything else is relative to StreamingAssets.</summary>
        public static string ClipUrl(string clip)
        {
            if (string.IsNullOrEmpty(clip)) return null;
            if (clip.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                clip.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                clip.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                return clip;
            return StreamingPath(clip);
        }
    }
}
