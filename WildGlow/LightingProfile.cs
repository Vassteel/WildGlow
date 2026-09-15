using System;

namespace WildGlow
{
    internal static class LightingProfile
    {
        internal static float GlowScale(string id)
        {
            if (id.StartsWith("mushroom", StringComparison.Ordinal) || id == "jotunpuffs" || id == "magecap" || id == "smokepuff") return 1.25f;
            if (id.EndsWith("berry", StringComparison.Ordinal)) return .065f;
            if (EffectBehavior.Geological(EffectBehavior.Family(id))) return .085f;
            return .15f;
        }
        internal static bool ExcludedSource(string name) =>
            name.Equals("Spawner_GreydwarfNest", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Spawner_GreydwarfNest_", StringComparison.OrdinalIgnoreCase);
    }
}
