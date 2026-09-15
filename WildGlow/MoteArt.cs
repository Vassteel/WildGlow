using System;

namespace WildGlow
{
    // Procedural facets and family motifs: four distinct atlas cells per style, not flat tinted dots.
    internal static class MoteArt
    {
        internal struct Pixel { internal float R, G, B, A; }
        internal static float Clamp(float x) => Math.Max(0, Math.Min(1, x));
        internal static string Motif(string id)
        {
            if (id.Contains("berry")) return "berry";
            if (id.StartsWith("mushroom") || id == "jotunpuffs" || id == "magecap" || id == "smokepuff") return "spore";
            if (id.EndsWith("core")) return "core";
            if (id == "honey" || id == "chitin" || id == "dragonegg") return "cell";
            if (id == "fenris") return "fiber";
            return "facet";
        }
        internal static Pixel Sample(float x, float y, string shape, string motif, int variant, Pixel body, Pixel rim)
        {
            float u = x / (0.84f + variant * 0.045f), v = y / (1f - variant * 0.045f);
            u += v * (variant - 1.5f) * 0.12f;
            if (motif == "spore")
            {
                // Translucent irregular spore flecks: no opaque bead silhouette or polished rim.
                float sporeAngle = (float)Math.Atan2(v, u);
                float sporeEdge = .54f + .07f * (float)Math.Sin(sporeAngle * (3 + variant) + variant);
                float dSpore = (float)Math.Sqrt(u * u + v * v * (1.1f + variant * .2f)) / sporeEdge;
                float soft = Clamp(1 - dSpore);
                float fleck = .75f + .25f * (float)Math.Sin(u * 19 + v * 13 + variant);
                return new Pixel { R = body.R * .3f + rim.R * .7f, G = body.G * .3f + rim.G * .7f,
                    B = body.B * .3f + rim.B * .7f, A = soft * soft * fleck * .78f };
            }
            float distance = (float)Math.Sqrt(u * u + v * v), d;
            switch (shape)
            {
                case "shard": d = Math.Abs(u + 0.23f * v) / (v > 0 ? 0.53f : 0.66f) + Math.Abs(v) / 0.92f; break;
                case "seed": d = Math.Abs(u) / 0.56f + Math.Abs(v) / 0.93f; break;
                case "leaf": d = Math.Abs(u) / 0.53f + v * v / 0.82f; break;
                case "hex": d = Math.Max(Math.Abs(v) / 0.78f, (Math.Abs(u) * 0.866f + Math.Abs(v) * 0.5f) / 0.78f); break;
                case "star": d = (float)(Math.Pow(Math.Abs(u), 0.60) + Math.Pow(Math.Abs(v), 0.60)); break;
                default: d = distance / 0.72f; break;
            }
            float alpha = Clamp((1 - d) / 0.055f);
            float angle = (float)Math.Atan2(v, u) + variant * 0.37f;
            int face = (int)Math.Floor((angle + Math.PI * 2) / (Math.PI / 3));
            float shade = 0.52f + (face % 3) * 0.23f;
            float seam = Clamp(1 - Math.Abs(u * 0.8f + v * 0.23f) / 0.035f) * Clamp(1 - d);
            float edge = Clamp((d - 0.80f) / 0.15f) * (u < 0 ? 0.88f : 0.38f);
            float accent = Math.Max(edge, seam * 0.30f);
            if (shape == "round")
            {
                shade = 0.45f + Clamp(1 - distance / 0.72f) * 0.48f;
                float gleam = Clamp(1 - (float)Math.Sqrt((u + 0.23f) * (u + 0.23f) + (v - 0.22f) * (v - 0.22f)) / 0.13f);
                accent = Math.Max(accent, gleam * 0.9f);
            }
            if (shape == "leaf" || motif == "fiber")
            {
                float vein = Math.Abs(u) < 0.025f || Math.Abs((v + Math.Abs(u) * 1.1f + 1) % 0.24f - 0.12f) < 0.015f ? 0.46f : 0;
                accent = Math.Max(accent, vein * Clamp(1 - d));
            }
            if (motif == "berry" || motif == "spore")
            {
                float gx = (u + 1.2f + variant * 0.07f) % 0.28f - 0.14f, gy = (v + 1.2f) % 0.26f - 0.13f;
                float speck = Clamp(1 - (float)Math.Sqrt(gx * gx + gy * gy) / 0.048f) * Clamp(1 - d);
                if (motif == "spore") accent = Math.Max(accent, speck * 0.9f);
                else shade *= 1 - speck * 0.55f;
            }
            if (motif == "cell" || motif == "core")
            {
                float band = Clamp(1 - Math.Abs(d - 0.60f) / 0.045f);
                accent = Math.Max(accent, band * 0.60f);
                if (motif == "core") accent = Math.Max(accent, Clamp(1 - distance / 0.24f) * 0.7f);
            }
            return new Pixel { R = body.R * shade * (1 - accent) + rim.R * accent, G = body.G * shade * (1 - accent) + rim.G * accent, B = body.B * shade * (1 - accent) + rim.B * accent, A = alpha };
        }
    }
}
