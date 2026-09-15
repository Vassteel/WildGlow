using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildGlow
{
    // Per-effect material instances, using the original shader/mesh/alpha clipping/LOD behavior.
    // No duplicate surface mesh, replacement albedo, glow billboards or local Light components.
    internal sealed class ModelEmission : IDisposable
    {
        private sealed class Glow
        {
            internal Material Original, Material;
            internal string ColorProperty;
            internal Color Baseline, Tint;
            internal float Scale;
        }
        private sealed class Binding
        {
            internal Renderer Renderer;
            internal Material[] Original, Emissive;
        }
        private readonly Dictionary<Tuple<Material, float>, Glow> materials = new Dictionary<Tuple<Material, float>, Glow>();
        private readonly Dictionary<Material, Material> originals = new Dictionary<Material, Material>();
        private readonly List<Binding> bindings = new List<Binding>();
        private bool applied;
        internal int MaterialCount => materials.Count;

        internal void Refresh(TargetGroup group)
        {
            Restore(); bindings.Clear();
            foreach (var target in group.Members)
            {
                if (!target.Root || target.Style.SurfaceGlow <= 0) continue;
                foreach (var renderer in target.Root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                    var source = renderer.sharedMaterials; var replacement = (Material[])source.Clone(); bool supported = false;
                    for (int i = 0; i < source.Length; i++)
                    {
                        var original = source[i]; if (!original) continue;
                        // Mining may propagate our material instance into a newly rebuilt renderer.
                        if (originals.TryGetValue(original, out var baseMaterial)) source[i] = original = baseMaterial;
                        string color = original.HasProperty("_EmissionColor") ? "_EmissionColor" : original.HasProperty("_EmissiveColor") ? "_EmissiveColor" : null;
                        if (color == null) { replacement[i] = original; continue; }
                        var key = Tuple.Create(original, target.Style.SurfaceGlow);
                        if (!materials.TryGetValue(key, out var glow))
                        {
                            var material = new Material(original) { name = original.name + " [WildGlow emission]", hideFlags = HideFlags.DontSave };
                            string texture = material.HasProperty("_EmissiveTex") ? "_EmissiveTex" : material.HasProperty("_EmissionMap") ? "_EmissionMap" : null;
                            if (texture != null && material.HasProperty("_MainTex"))
                            {
                                // Reuse albedo and its actual tiling; no newly drawn surface pattern.
                                material.SetTexture(texture, original.GetTexture("_MainTex") ?? Texture2D.whiteTexture);
                                material.SetTextureScale(texture, original.GetTextureScale("_MainTex"));
                                material.SetTextureOffset(texture, original.GetTextureOffset("_MainTex"));
                            }
                            material.EnableKeyword("_EMISSION");
                            glow = new Glow { Original = original, Material = material, ColorProperty = color, Scale = target.Style.SurfaceGlow, Baseline = original.GetColor(color), Tint = original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white };
                            materials.Add(key, glow); originals.Add(material, original);
                        }
                        replacement[i] = glow.Material; supported = true;
                    }
                    if (supported) bindings.Add(new Binding { Renderer = renderer, Original = source, Emissive = replacement });
                }
            }
        }
        internal void Tick(float amount, float night)
        {
            if (amount <= .0001f) { Restore(); return; }
            // Neutral self-illumination keeps the plant/ore's own texture colors readable.
            float strength = Mathf.Clamp(amount, 0, 2) * Mathf.Lerp(.18f, .65f, night);
            foreach (var glow in materials.Values)
            {
                if (!glow.Material) continue;
                var color = glow.Baseline + glow.Tint * (strength * glow.Scale); color.a = glow.Baseline.a;
                glow.Material.SetColor(glow.ColorProperty, color);
            }
            if (applied) return;
            foreach (var binding in bindings) if (binding.Renderer) binding.Renderer.sharedMaterials = binding.Emissive;
            applied = true;
        }
        internal void Restore()
        {
            if (!applied) return;
            foreach (var binding in bindings)
            {
                if (!binding.Renderer) continue;
                var current = binding.Renderer.sharedMaterials; bool changed = false;
                for (int i = 0; i < current.Length; i++)
                    if (current[i] && originals.TryGetValue(current[i], out var original)) { current[i] = original; changed = true; }
                if (changed) binding.Renderer.sharedMaterials = current;
            }
            applied = false;
        }
        public void Dispose()
        {
            Restore();
            foreach (var glow in materials.Values) if (glow.Material) UnityEngine.Object.Destroy(glow.Material);
            bindings.Clear(); materials.Clear(); originals.Clear();
        }
    }
}
