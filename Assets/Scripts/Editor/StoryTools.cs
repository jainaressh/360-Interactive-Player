using System.IO;
using UnityEditor;
using UnityEngine;

namespace Interactive360.EditorTools
{
    /// <summary>Editor-side story checks: graph validity plus clip files present in StreamingAssets.</summary>
    public static class StoryTools
    {
        const string DefaultStory = "Assets/StreamingAssets/Story/story.json";

        [MenuItem("Interactive360/Validate Story", priority = 20)]
        public static void ValidateMenu()
        {
            int errors = Validate(DefaultStory);
            if (errors == 0) EditorUtility.DisplayDialog("Story", "Story is valid.", "OK");
            else EditorUtility.DisplayDialog("Story", errors + " error(s). See the Console.", "OK");
        }

        /// <summary>Returns the number of errors found.</summary>
        public static int Validate(string storyAssetPath)
        {
            if (!File.Exists(storyAssetPath)) { Debug.LogError("[Story] Not found: " + storyAssetPath); return 1; }

            Story story;
            try { story = StoryLoader.Parse(File.ReadAllText(storyAssetPath)); }
            catch (System.Exception e) { Debug.LogError("[Story] Parse failed: " + e.Message); return 1; }

            int errors = 0;
            foreach (var issue in StoryValidation.Validate(story))
            {
                if (issue.StartsWith("ERROR")) { errors++; Debug.LogError("[Story] " + issue); }
                else Debug.LogWarning("[Story] " + issue);
            }

            string streaming = Path.Combine(Application.dataPath, "StreamingAssets");
            foreach (var kv in story.Nodes)
            {
                var clip = kv.Value.Clip;
                if (string.IsNullOrEmpty(clip) || clip.StartsWith("http")) continue;
                var full = Path.Combine(streaming, clip.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full)) { errors++; Debug.LogError("[Story] node '" + kv.Key + "' clip missing: " + full); }
            }

            Debug.Log("[Story] '" + story.Title + "': " + story.Nodes.Count + " nodes, " + errors + " error(s).");
            return errors;
        }

        public static void ValidateFromCommandLine()
        {
            int errors = Validate(DefaultStory);
            EditorApplication.Exit(errors == 0 ? 0 : 1);
        }
    }
}
