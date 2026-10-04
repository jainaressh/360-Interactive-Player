using System.Collections.Generic;

namespace Interactive360
{
    public static class StoryValidation
    {
        /// <summary>Returns human readable problems. Entries starting with "ERROR" will break playback; "WARN" entries are advisory.</summary>
        public static List<string> Validate(Story story)
        {
            var issues = new List<string>();
            if (story == null) { issues.Add("ERROR: story is null"); return issues; }
            if (story.Nodes == null || story.Nodes.Count == 0) { issues.Add("ERROR: story has no nodes"); return issues; }
            if (!story.TryGetNode(story.Start, out _)) issues.Add("ERROR: start node '" + story.Start + "' does not exist");

            foreach (var kv in story.Nodes)
            {
                var id = kv.Key; var n = kv.Value;
                if (string.IsNullOrEmpty(n.Clip)) issues.Add("ERROR: node '" + id + "' has no clip");
                if (n.Choices != null)
                    for (int i = 0; i < n.Choices.Count; i++)
                    {
                        var c = n.Choices[i];
                        if (!story.TryGetNode(c.Goto, out _)) issues.Add("ERROR: node '" + id + "' choice " + i + " goes to unknown node '" + c.Goto + "'");
                        if (string.IsNullOrEmpty(c.Label)) issues.Add("WARN: node '" + id + "' choice " + i + " has no label");
                    }
                if (n.Redirects != null)
                    foreach (var r in n.Redirects)
                        if (!story.TryGetNode(r.Goto, out _)) issues.Add("ERROR: node '" + id + "' redirect goes to unknown node '" + r.Goto + "'");
                if (!string.IsNullOrEmpty(n.Next) && !story.TryGetNode(n.Next, out _)) issues.Add("ERROR: node '" + id + "' next is unknown node '" + n.Next + "'");
                if (!n.HasChoices && string.IsNullOrEmpty(n.Next) && !n.Ending) issues.Add("WARN: node '" + id + "' is a dead end (no choices, no next, not marked ending)");
                if (n.HasChoices && (n.DefaultChoice < 0 || n.DefaultChoice >= n.Choices.Count)) issues.Add("WARN: node '" + id + "' defaultChoice out of range; 0 will be used");
                if (n.Layout != null && n.Layout != "mono" && n.Layout != "side-by-side" && n.Layout != "over-under") issues.Add("WARN: node '" + id + "' has unknown layout '" + n.Layout + "'");
            }

            // Reachability from start.
            var seen = new HashSet<string>();
            var stack = new Stack<string>();
            if (story.TryGetNode(story.Start, out _)) stack.Push(story.Start);
            while (stack.Count > 0)
            {
                var id = stack.Pop();
                if (!seen.Add(id) || !story.TryGetNode(id, out var n)) continue;
                if (n.Choices != null) foreach (var c in n.Choices) if (c.Goto != null) stack.Push(c.Goto);
                if (n.Redirects != null) foreach (var r in n.Redirects) if (r.Goto != null) stack.Push(r.Goto);
                if (n.Next != null) stack.Push(n.Next);
            }
            foreach (var id in story.Nodes.Keys) if (!seen.Contains(id)) issues.Add("WARN: node '" + id + "' is unreachable from start");
            return issues;
        }
    }
}
