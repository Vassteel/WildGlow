using System;
namespace WildGlow
{
    internal static class TreeFilter
    {
        internal static bool Excluded(string name)
        {
            // Prefab families, including seedlings, small trees, dead variants, logs and stumps.
            // Do not match berry bushes or arbitrary names merely containing these words.
            foreach (string prefix in new[] { "Beech", "PineTree", "FirTree", "SnowFirTree", "shrub" })
            {
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Length == prefix.Length) return true;
                char next = name[prefix.Length];
                if (next == '_' || next == ' ' || char.IsDigit(next)) return true;
            }
            return false;
        }
    }
}
