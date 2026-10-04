using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Interactive360
{
    /// <summary>Mutable flag store that persists across nodes (and across loops back to the start).</summary>
    public sealed class StoryState
    {
        readonly Dictionary<string, object> values = new Dictionary<string, object>();

        public IReadOnlyDictionary<string, object> Values => values;

        public void Clear() => values.Clear();

        public StoryState Clone()
        {
            var copy = new StoryState();
            foreach (var kv in values) copy.values[kv.Key] = kv.Value;
            return copy;
        }

        public void Apply(Dictionary<string, JToken> set)
        {
            if (set == null) return;
            foreach (var kv in set)
            {
                if (kv.Value != null && kv.Value.Type == JTokenType.String)
                {
                    var s = kv.Value.Value<string>();
                    if (s.Length > 1 && (s[0] == '+' || s[0] == '-') &&
                        double.TryParse(s.Substring(1), NumberStyles.Float, CultureInfo.InvariantCulture, out var delta))
                    {
                        var current = AsNumber(values.TryGetValue(kv.Key, out var v) ? v : null);
                        values[kv.Key] = s[0] == '+' ? current + delta : current - delta;
                        continue;
                    }
                }
                values[kv.Key] = ToValue(kv.Value);
            }
        }

        public bool Matches(Dictionary<string, JToken> when)
        {
            if (when == null || when.Count == 0) return true;
            foreach (var kv in when)
            {
                values.TryGetValue(kv.Key, out var actual);
                if (!MatchesOne(actual, kv.Value)) return false;
            }
            return true;
        }

        static readonly string[] Operators = { ">=", "<=", "!=", "==", ">", "<" };

        static bool MatchesOne(object actual, JToken expected)
        {
            if (expected != null && expected.Type == JTokenType.String)
            {
                var s = expected.Value<string>().Trim();
                foreach (var op in Operators)
                {
                    if (!s.StartsWith(op, StringComparison.Ordinal)) continue;
                    if (!double.TryParse(s.Substring(op.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out var rhs))
                        break;
                    var lhs = AsNumber(actual);
                    switch (op)
                    {
                        case ">=": return lhs >= rhs;
                        case "<=": return lhs <= rhs;
                        case "!=": return Math.Abs(lhs - rhs) > 1e-9;
                        case "==": return Math.Abs(lhs - rhs) <= 1e-9;
                        case ">": return lhs > rhs;
                        case "<": return lhs < rhs;
                    }
                }
            }

            var want = ToValue(expected);
            switch (want)
            {
                case null: return actual == null;
                case bool b: return (actual is bool ab ? ab : AsNumber(actual) != 0) == b;
                case double d: return Math.Abs(AsNumber(actual) - d) <= 1e-9;
                default: return string.Equals(actual?.ToString() ?? string.Empty, want.ToString(), StringComparison.Ordinal);
            }
        }

        static double AsNumber(object v)
        {
            switch (v)
            {
                case null: return 0;
                case bool b: return b ? 1 : 0;
                case double d: return d;
                case string s: return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0;
                default: return Convert.ToDouble(v, CultureInfo.InvariantCulture);
            }
        }

        static object ToValue(JToken t)
        {
            if (t == null) return null;
            switch (t.Type)
            {
                case JTokenType.Boolean: return t.Value<bool>();
                case JTokenType.Integer: return (double)t.Value<long>();
                case JTokenType.Float: return t.Value<double>();
                case JTokenType.String: return t.Value<string>();
                case JTokenType.Null: return null;
                default: return t.ToString();
            }
        }

        public override string ToString()
        {
            var parts = new List<string>();
            foreach (var kv in values) parts.Add(kv.Key + "=" + kv.Value);
            return "{" + string.Join(", ", parts) + "}";
        }
    }
}
