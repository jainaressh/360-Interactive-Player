using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Interactive360
{
    /// <summary>Root of a story graph loaded from StreamingAssets/Story/*.json.</summary>
    public sealed class Story
    {
        [JsonProperty("title")] public string Title = "Untitled";
        [JsonProperty("start")] public string Start;

        /// <summary>If a node has no explicit choicesAt, the choice window opens this many seconds before the clip ends.</summary>
        [JsonProperty("defaultChoiceSeconds")] public float DefaultChoiceSeconds = 6f;

        /// <summary>false = Bandersnatch style: after a choice the current clip plays to its end, then the branch continues. true = cut immediately.</summary>
        [JsonProperty("cutOnChoice")] public bool CutOnChoice = false;

        [JsonProperty("nodes")] public Dictionary<string, StoryNode> Nodes = new Dictionary<string, StoryNode>();

        public bool TryGetNode(string id, out StoryNode node)
        {
            node = null;
            return !string.IsNullOrEmpty(id) && Nodes != null && Nodes.TryGetValue(id, out node);
        }
    }

    public sealed class StoryNode
    {
        [JsonIgnore] public string Id;

        [JsonProperty("title")] public string Title;
        [JsonProperty("prompt")] public string Prompt;
        [JsonProperty("clip")] public string Clip;

        /// <summary>mono | side-by-side | over-under (stereo layouts for 3D 360 footage).</summary>
        [JsonProperty("layout")] public string Layout = "mono";

        /// <summary>Clip time (seconds) at which the choice panel appears. Null = length - defaultChoiceSeconds.</summary>
        [JsonProperty("choicesAt")] public float? ChoicesAt;

        /// <summary>How long the choice window stays open. Null = until the clip ends.</summary>
        [JsonProperty("choiceSeconds")] public float? ChoiceSeconds;

        /// <summary>Index into the visible choices picked when the window closes without input.</summary>
        [JsonProperty("defaultChoice")] public int DefaultChoice;

        [JsonProperty("cutOnChoice")] public bool? CutOnChoice;

        /// <summary>Pause on the last frame and wait for a choice instead of auto-picking the default.</summary>
        [JsonProperty("holdLastFrame")] public bool HoldLastFrame;

        [JsonProperty("choices")] public List<StoryChoice> Choices = new List<StoryChoice>();

        /// <summary>Evaluated on entry, before the clip plays: the first matching rule redirects to another node.</summary>
        [JsonProperty("redirect")] public List<StoryRedirect> Redirects = new List<StoryRedirect>();

        /// <summary>Node to continue to when there are no choices (or none are visible).</summary>
        [JsonProperty("next")] public string Next;

        [JsonProperty("ending")] public bool Ending;

        /// <summary>Clear all story flags when this node is entered (useful on endings that loop back to the start).</summary>
        [JsonProperty("resetState")] public bool ResetState;

        public bool HasChoices => Choices != null && Choices.Count > 0;
    }

    public sealed class StoryChoice
    {
        [JsonProperty("label")] public string Label;
        [JsonProperty("goto")] public string Goto;

        /// <summary>Flags written when this choice is taken. Values may be bool/number/string, or "+1"/"-2" to add to a number.</summary>
        [JsonProperty("set")] public Dictionary<string, JToken> Set;

        /// <summary>Only offer this choice when every flag matches. Values may be literals or comparisons such as ">=2".</summary>
        [JsonProperty("when")] public Dictionary<string, JToken> When;
    }

    public sealed class StoryRedirect
    {
        [JsonProperty("when")] public Dictionary<string, JToken> When;
        [JsonProperty("goto")] public string Goto;
    }
}
