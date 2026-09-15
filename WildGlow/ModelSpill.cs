using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildGlow
{
    internal sealed class ModelSpill : IDisposable
    {
        private readonly Transform parent;
        private readonly List<Light> lights = new List<Light>();
        internal int ActiveCount { get; private set; }
        internal ModelSpill(Transform parent) { this.parent = parent; }
        internal void Tick(TargetGroup group, Color tint, float amount, float padding, float night)
        {
            int count = amount > .0001f && group != null && group.HasLightBounds ? group.SpillBudget : 0;
            while (lights.Count > count)
            {
                var last = lights[lights.Count - 1];
                last.enabled = false; UnityEngine.Object.Destroy(last.gameObject); lights.RemoveAt(lights.Count - 1);
            }
            while (lights.Count < count)
            {
                var go = new GameObject("WildGlow soft spill") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(parent, false);
                var light = go.AddComponent<Light>(); light.enabled = false;
                light.type = LightType.Point; light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForcePixel; light.bounceIntensity = 0;
                lights.Add(light);
            }
            ActiveCount = count;
            if (count == 0) return;
            var bounds = group.LightBounds; var e = bounds.extents;
            float range = SpillLayout.Range(e.x, e.y, e.z, padding);
            float intensity = SpillLayout.Intensity(amount, night, count);
            for (int i = 0; i < count; i++)
            {
                var offset = SpillLayout.Position(e.x, e.y, e.z, i, count);
                var light = lights[i];
                light.transform.position = bounds.center + new Vector3(offset.X, offset.Y, offset.Z);
                light.range = range; light.intensity = intensity;
                // Mostly neutral so obsidian does not produce black light or copper stain the ground green.
                light.color = Color.Lerp(Color.white, tint, .18f); light.enabled = true;
            }
        }
        internal void Hide() { foreach (var light in lights) if (light) light.enabled = false; ActiveCount = 0; }
        public void Dispose()
        {
            Hide(); foreach (var light in lights) if (light) UnityEngine.Object.Destroy(light.gameObject);
            lights.Clear();
        }
    }
}
