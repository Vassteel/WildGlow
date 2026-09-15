using System;

namespace WildGlow
{
    internal enum EffectFamily { Plant, Spore, Berry, Mineral, Obsidian, Hive, Chest, Treasure, Magic, Drift }

    // Artistic categories, not a claim about drop rates or an official game rarity tier.
    internal static class EffectBehavior
    {
        internal static EffectFamily Family(string id)
        {
            switch (id)
            {
                case "blackcore": case "moltencore": case "frostcore": case "dyrnwyn": return EffectFamily.Magic;
                case "amber": case "ruby": case "crystal": case "dragonegg": return EffectFamily.Treasure;
                case "honey": return EffectFamily.Hive;
                case "chest": return EffectFamily.Chest;
                case "obsidian": return EffectFamily.Obsidian;
                case "copper": case "tin": case "iron": case "silver": case "blackmetal": case "flametal":
                case "meteorite": case "gold": case "chitin": case "blackmarble": case "grausten": case "sulfur": return EffectFamily.Mineral;
                case "mushroom": case "mushroomyellow": case "mushroomblue": case "jotunpuffs": case "magecap": case "smokepuff": return EffectFamily.Spore;
                case "carrot": case "turnip": case "onion": case "dandelion": case "thistle": case "barley": case "flax":
                case "fiddlehead": case "kale": case "oat": case "poteitr": case "seed": return EffectFamily.Plant;
                default: return id.Contains("berry") ? EffectFamily.Berry : EffectFamily.Drift;
            }
        }
        internal static bool Geological(EffectFamily f) => f == EffectFamily.Mineral || f == EffectFamily.Obsidian;
        internal static float Night(float dayFraction)
        {
            float daylight = Math.Min(Clamp((dayFraction - 0.20f) / 0.10f), Clamp((0.80f - dayFraction) / 0.10f));
            return 1 - daylight * daylight * (3 - 2 * daylight);
        }
        internal static float Clamp(float x) => Math.Max(0, Math.Min(1, x));
        private static float Sin(float x) => (float)Math.Sin(x);
        private static float Cos(float x) => (float)Math.Cos(x);
        internal static float Reach(EffectFamily f) => f == EffectFamily.Magic ? 1 : f == EffectFamily.Treasure ? 0.65f :
            f == EffectFamily.Plant ? 0.38f : f == EffectFamily.Spore ? 1f : f == EffectFamily.Berry ? 0.15f :
            Geological(f) ? 0.13f : f == EffectFamily.Hive ? 0.22f : f == EffectFamily.Chest ? 0.26f : 0.25f;

        // Offsets from individual source anchors; zero anchors produce the console/browser demonstration.
        internal static MoteMotion.Point Position(EffectFamily f, float p, float t, int key, float r, float h, float twist, float night)
        {
            float q = MoteMotion.Hash(key + 43) * 6.283185f;
            float angle = q + t * (0.16f + night * 0.07f) * twist;
            float height = h * Reach(f);
            switch (f)
            {
                case EffectFamily.Plant:
                    return new MoteMotion.Point(Cos(q) * r * 0.45f + Sin(t * .6f + q) * r * .22f * twist * p,
                        .025f + p * height, Sin(q) * r * .45f + Cos(t * .4f + q) * r * .18f * twist * p);
                case EffectFamily.Spore:
                    float cloud = r * (.18f + .65f * (1 - p)) * (1 + night * .1f);
                    return new MoteMotion.Point(Cos(angle) * cloud, .05f + p * height, Sin(angle) * cloud);
                case EffectFamily.Berry:
                    return new MoteMotion.Point(Cos(q) * r * .8f + Sin(angle * 2) * r * .06f * twist,
                        .02f + MoteMotion.Hash(key + 8) * height * .6f + p * height * .3f, Sin(q) * r * .8f);
                case EffectFamily.Mineral:
                    return new MoteMotion.Point(Sin(t * .25f + q) * .035f * twist * p, .012f + p * height, Cos(q) * .035f * p);
                case EffectFamily.Obsidian:
                    return new MoteMotion.Point(Sin(angle * 1.5f) * .08f * twist * p, .012f + p * height, Cos(angle) * .06f * twist * p);
                case EffectFamily.Hive:
                    return new MoteMotion.Point(Cos(angle + p * 6.28f * twist) * r * (.65f + .2f * Sin(q)),
                        .04f + height * (.3f + .24f * Sin(angle * 2 + q)), Sin(angle + p * 6.28f * twist) * r * .65f);
                case EffectFamily.Chest:
                    // Four edges of a rectangular lid, with dust lifting from its seams.
                    float edge = MoteMotion.Hash(key + 2) * 2 - 1;
                    return new MoteMotion.Point(key % 2 == 0 ? edge * r : (key % 4 == 1 ? r : -r),
                        .015f + p * height, key % 2 != 0 ? edge * r * .6f : (key % 4 == 0 ? r : -r) * .6f);
                case EffectFamily.Treasure:
                    return new MoteMotion.Point(Cos(angle + p * 3 * twist) * r * (.3f + .4f * Sin(p * 3.14f)),
                        .04f + p * height, Sin(angle + p * 3 * twist) * r * .6f);
                case EffectFamily.Magic:
                    if (key % 3 == 0) return new MoteMotion.Point(Cos(angle * 2) * r * .8f,
                        height * (.18f + .05f * Sin(angle * 2 + q)), Sin(angle * 2) * r * .8f);
                    return MoteMotion.Position(p, t, key, r, h, twist);
                default:
                    return new MoteMotion.Point(Cos(angle) * r * .55f, .03f + p * height, Sin(angle) * r * .55f);
            }
        }
        internal static float Alpha(EffectFamily f, float p, float t, int key, float night)
        {
            float fade = Clamp(p / .12f) * Clamp((1 - p) / .2f);
            if (f == EffectFamily.Berry) fade *= (float)Math.Pow(Math.Max(0, Sin(t * 1.1f + key / 4)), 4);
            if (f == EffectFamily.Spore) fade *= .5f + .5f * Sin(p * 3.14159f);
            if (f == EffectFamily.Magic || f == EffectFamily.Treasure) fade *= .72f + .28f * Sin(t * .9f + key / 8);
            return fade;
        }
        internal static MoteMotion.Point Path(EffectFamily f, bool column, MoteMotion.Point anchor, float p, float t, int key, float r, float h, float twist, float night)
        {
            var v = column ? MoteMotion.Position(p, t, key, r, h, twist) : Position(f, p, t, key, r, h, twist, night);
            float k = column || f == EffectFamily.Spore ? (1 - p) * (1 - p) : 1;
            return new MoteMotion.Point(v.X + anchor.X * k, v.Y + anchor.Y * k, v.Z + anchor.Z * k);
        }
        internal static MoteMotion.Point TrailPoint(EffectFamily f, bool column, MoteMotion.Point anchor, float p, float t, int key, float r, float h, float speed, float twist, float length, float fraction, float night)
        {
            var head = Path(f, column, anchor, p, t, key, r, h, twist, night);
            float delay = Math.Min(.28f * length, p * MoteMotion.Lifetime / Math.Max(.01f, speed)) * fraction;
            var tail = Path(f, column, anchor, Math.Max(0, p - delay * speed / MoteMotion.Lifetime), t - delay, key, r, h, twist, night);
            float dx = tail.X - head.X, dy = tail.Y - head.Y, dz = tail.Z - head.Z;
            float d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz), cap = .22f * length;
            float k = d > cap && d > 0 ? cap / d : 1;
            return new MoteMotion.Point(head.X + dx * k, head.Y + dy * k, head.Z + dz * k);
        }
    }
}
