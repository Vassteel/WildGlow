using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WildGlow
{
    internal struct Appearance
    {
        internal float Density, Radius, Height, Brightness, Speed, Dust, TrailLength, Twist, AreaGlow, SpillIntensity, SpillRange, Night;
    }

    internal sealed class MoteEffect : IDisposable
    {
        private readonly GameObject root;
        private readonly ParticleSystem motes, dust, glints;
        private readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[128];
        private readonly ParticleSystem.Particle[] dustParticles = new ParticleSystem.Particle[192];
        private readonly ParticleSystem.Particle[] glintParticles = new ParticleSystem.Particle[32];
        private readonly Style style;
        private readonly int seed;
        private readonly float created;
        private readonly TrailMesh trails;
        private readonly ModelEmission emission = new ModelEmission();
        private readonly ModelSpill spill;
        private TargetGroup sources;
        private readonly EffectFamily family;
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly List<Texture2D> Textures = new List<Texture2D>();

        internal MoteEffect(Style style, int id)
        {
            this.style = style; family = EffectBehavior.Family(style.Id); seed = (int)((uint)id % 997); created = Time.time;
            root = new GameObject("WildGlow_" + style.Id) { hideFlags = HideFlags.DontSave };
            spill = new ModelSpill(root.transform);
            try
            {
                motes = CreateSystem("Faceted motes", GetMaterial(style, "mote"), 128);
                var sheet = motes.textureSheetAnimation;
                sheet.enabled = true; sheet.numTilesX = 2; sheet.numTilesY = 2;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0);
                // StartFrame is measured in atlas frames, not normalized UVs (Unity 6 API).
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0, 3.999f);
                dust = CreateSystem("Fine dust", GetMaterial(style, "dust"), 192);
                glints = CreateSystem("Edge glints", GetMaterial(style, "glint"), 32);
                trails = new TrailMesh(root.transform, GetMaterial(style, "trail"));
            }
            catch { trails?.Dispose(); UnityEngine.Object.Destroy(root); throw; }
        }

        private ParticleSystem CreateSystem(string name, Material material, int count)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = false; main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0; main.startLifetime = 1000; main.gravityModifier = 0;
            main.scalingMode = ParticleSystemScalingMode.Local;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.maxParticleSize = 0.5f; renderer.sortMode = ParticleSystemSortMode.Distance;
            ps.Play(); ps.Pause();
            return ps;
        }

        private static ParticleSystem.Particle Particle(Vector3 position, float size, Color color, float rotation, uint key) =>
            new ParticleSystem.Particle { position = position, startSize = size, startColor = color, rotation = rotation,
                startLifetime = 1000, remainingLifetime = 1000, randomSeed = key + 1 };

        internal void SetVisible(bool visible) { if (root) root.SetActive(visible); if (!visible) { emission.Restore(); spill.Hide(); } }
        internal void BindSources(TargetGroup group)
        {
            if (!ReferenceEquals(sources, group)) emission.Refresh(group);
            sources = group;
        }
        internal int EmissiveMaterials => emission.MaterialCount;
        internal int SpillLights => spill.ActiveCount;
        private SurfaceAnchor SourceAnchor(int index, bool upward)
        {
            int first = (int)(MoteMotion.Hash(index + 31) * sources.Anchors.Count);
            // Columns emerge from the upper rock; side/underside anchors feed the surface layer.
            if (upward && sources.IsDeposit)
                for (int i = 0; i < sources.Anchors.Count; i++)
                {
                    var a = sources.Anchors[(first + i) % sources.Anchors.Count];
                    if (a.Valid && a.WorldNormal.y >= .25f) return a;
                }
            return sources.Anchors[first];
        }
        private MoteMotion.Point Anchor(int index, Vector3 center, bool upward = false)
        {
            if (sources == null || sources.Anchors.Count == 0) return default;
            var anchor = SourceAnchor(index, upward);
            var v = anchor.Valid ? anchor.World - center : Vector3.zero;
            return new MoteMotion.Point(v.x, v.y, v.z);
        }

        private Vector3 AnchorNormal(int index)
        {
            if (sources == null || !sources.IsDeposit || sources.Anchors.Count == 0) return Vector3.zero;
            var anchor = SourceAnchor(index, false);
            return anchor.Valid ? anchor.WorldNormal : Vector3.up;
        }

        internal void Tick(Vector3 center, float time, Appearance a, float fade, Camera camera)
        {
            if (!root) return;
            root.transform.position = center;
            if (sources != null && sources.Anchors.Count == 0) { SetVisible(false); return; }
            float opacity = a.Brightness * fade * Mathf.Clamp01((time - created) / 0.8f);
            int count = Mathf.Clamp(Mathf.RoundToInt(64 * a.Density), 12, 128), glintCount = 0;
            float radius = style.Radius * a.Radius, height = style.Height * a.Height;
            // Model anchors already supply the object's shape; keep local flourishes close to it.
            float localRadius = sources != null && !sources.IsDeposit ? Mathf.Min(radius, .16f) : radius;
            Vector3 cameraLocal = camera ? camera.transform.position - center : new Vector3(0, 2, -5);
            Color highlight = Color.Lerp(style.Color, style.Accent, 0.8f);
            float coverage = Mathf.Max(radius, sources != null ? sources.SurfaceRadius : radius);
            float night = a.Night;
            trails.Begin();
            for (int i = 0; i < count; i++)
            {
                int key = i + seed;
                float phase = MoteMotion.Phase(time, a.Speed, key);
                bool column = i % 3 == 0;
                var anchor = Anchor(i, center, column);
                Vector3 position = TrailMesh.ToVector(EffectBehavior.Path(family, column, anchor, phase, time, key, column ? radius : localRadius, height, a.Twist, night));
                var normal = column ? Vector3.zero : AnchorNormal(i);
                position = TrailMesh.Orient(position, anchor, normal);
                float twinkle = 0.82f + 0.18f * Mathf.Sin(time * 1.8f + key * 2.7f);
                float alpha = (column ? Mathf.Clamp01(phase / .12f) * Mathf.Clamp01((1 - phase) / .2f) : EffectBehavior.Alpha(family, phase, time, key, night)) * twinkle * opacity;
                float size = (style.Shape == "shard" ? 0.085f : 0.066f) * (0.7f + 0.6f * MoteMotion.Hash(key + 31));
                size *= Mathf.Lerp(1.3f, 1f, night) * (family == EffectFamily.Spore ? .38f : 1);
                particles[i] = Particle(position, size, new Color(1, 1, 1, Mathf.Clamp01(alpha)), MoteMotion.Spin(time, key, a.Twist * (family == EffectFamily.Obsidian ? 1.8f : 1)), (uint)key);
                if (i % 2 == 0) trails.Add(family, column, anchor, night, phase, time, key, column ? radius : localRadius, height, a.Speed, a.Twist, (family == EffectFamily.Spore ? Mathf.Min(a.TrailLength, .35f) : a.TrailLength), size, alpha * Mathf.Lerp(.5f, 1f, night), highlight, cameraLocal, normal);
                if (family != EffectFamily.Spore && i % 4 == 0 && glintCount < glintParticles.Length)
                {
                    float flash = Mathf.Pow(Mathf.Max(0, Mathf.Sin(time * 1.2f + key)), 14);
                    var tint = highlight; tint.a = alpha * flash * Mathf.Lerp(.4f, .9f, night);
                    glintParticles[glintCount++] = Particle(position + Vector3.up * size * 0.2f, size * 1.25f, tint, 15, (uint)key);
                }
            }
            motes.SetParticles(particles, count); glints.SetParticles(glintParticles, glintCount); trails.End(coverage + radius, height);
            int dustCount = Mathf.Clamp(Mathf.RoundToInt(80 * a.Density * a.Dust), 0, dustParticles.Length);
            for (int i = 0; i < dustCount; i++)
            {
                int key = seed + 1000 + i;
                float phase = MoteMotion.Phase(time, a.Speed * 0.65f, key);
                var anchor = Anchor(i + 19, center, i % 4 == 0);
                Vector3 position = TrailMesh.ToVector(EffectBehavior.Path(family, i % 4 == 0, anchor, phase, time, key, (i % 4 == 0 ? radius : localRadius) * 1.1f, height * .95f, a.Twist * .7f, night));
                position = TrailMesh.Orient(position, anchor, i % 4 == 0 ? Vector3.zero : AnchorNormal(i + 19));
                position += new Vector3(Mathf.Sin(time * 0.45f + key), Mathf.Sin(time * 0.6f + key), Mathf.Cos(time * 0.4f + key)) * 0.025f;
                var tint = highlight;
                tint.a = opacity * Mathf.Lerp(.3f, .5f, night) * Mathf.Sin(phase * Mathf.PI) * (0.6f + 0.4f * MoteMotion.Hash(key + 31));
                dustParticles[i] = Particle(position, 0.013f + 0.009f * MoteMotion.Hash(key + 93), tint, 0, (uint)key);
            }
            dust.SetParticles(dustParticles, dustCount);
            float lightFade = fade * Mathf.Clamp01((time - created) / .8f);
            emission.Tick(lightFade * a.AreaGlow, night);
            spill.Tick(sources, style.Color, lightFade * a.SpillIntensity * style.LightSpill, a.SpillRange, night);
        }

        private static Material GetMaterial(Style style, string layer)
        {
            string motif = MoteArt.Motif(style.Id);
            string key = (layer == "mote") ? layer + motif + style.Shape + ColorUtility.ToHtmlStringRGB(style.Color) + ColorUtility.ToHtmlStringRGB(style.Accent) : layer;
            if (Materials.TryGetValue(key, out var material) && material) return material;
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (!shader) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (!shader) throw new InvalidOperationException("No supported unlit particle shader is loaded.");
            bool additive = layer != "mote" && shader.name == "Particles/Standard Unlit";
            material = new Material(shader) { name = "WildGlow_" + key, hideFlags = HideFlags.DontSave, renderQueue = 3000 };
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", additive ? 4 : 2);
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_ALPHABLEND_ON"); material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("SOFTPARTICLES_ON"); material.DisableKeyword("_FADING_ON");
            Texture2D texture = MakeTexture(style, layer);
            material.mainTexture = texture; Materials[key] = material; Textures.Add(texture);
            return material;
        }

        private static Texture2D MakeTexture(Style style, string layer)
        {
            const int cell = 96;
            int size = layer == "mote" ? cell * 2 : cell;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "WildGlow_" + layer, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var pixels = new Color[size * size];
            var body = new MoteArt.Pixel { R = style.Color.r, G = style.Color.g, B = style.Color.b };
            var rim = new MoteArt.Pixel { R = style.Accent.r, G = style.Accent.g, B = style.Accent.b };
            string motif = MoteArt.Motif(style.Id);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = (x % cell + 0.5f) / cell * 2 - 1, v = (y % cell + 0.5f) / cell * 2 - 1;
                Color c = Color.white;
                if (layer == "mote")
                {
                    var p = MoteArt.Sample(u, v, style.Shape, motif, x / cell + y / cell * 2, body, rim);
                    c = new Color(p.R, p.G, p.B, p.A);
                }
                else if (layer == "trail") c.a = Mathf.Pow(Mathf.Max(0, 1 - Mathf.Abs(u)), 1.5f);
                else if (layer == "glint")
                {
                    float star = Mathf.Max(0, 1 - (Mathf.Pow(Mathf.Abs(u), 0.5f) + Mathf.Pow(Mathf.Abs(v), 0.5f)));
                    c.a = star + Mathf.Pow(Mathf.Max(0, 1 - Mathf.Sqrt(u * u + v * v)), 6) * 0.4f;
                }
                else c.a = Mathf.Pow(Mathf.Max(0, 1 - Mathf.Sqrt(u * u + v * v)), layer == "dust" ? 1.6f : 2.2f);
                pixels[y * size + x] = c;
            }
            texture.SetPixels(pixels); texture.Apply(false, true); return texture;
        }

        public void Dispose() { emission.Dispose(); spill.Dispose(); trails?.Dispose(); if (root) UnityEngine.Object.Destroy(root); }
        internal static void ReleaseMaterials()
        {
            foreach (var material in Materials.Values) if (material) UnityEngine.Object.Destroy(material);
            foreach (var texture in Textures) if (texture) UnityEngine.Object.Destroy(texture);
            Materials.Clear(); Textures.Clear();
        }
    }
}
