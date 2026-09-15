using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildGlow
{
    // Read mesh triangles without changing any model/material or adding physics components.
    internal static class ModelSurface
    {
        internal static bool Prepare(GameObject root, List<SurfaceAnchor> anchors, ref int signature, out Bounds bounds, out bool changed)
        {
            bounds = default; changed = false;
            var parts = new List<MeshFilter>(); var hiddenLods = new HashSet<Renderer>();
            foreach (var lod in root.GetComponentsInChildren<LODGroup>(true))
            {
                var levels = lod.GetLODs();
                for (int i = 1; i < levels.Length; i++) foreach (var renderer in levels[i].renderers) if (renderer) hiddenLods.Add(renderer);
                if (levels.Length > 0) foreach (var renderer in levels[0].renderers) hiddenLods.Remove(renderer);
            }
            int current = 23;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                var renderer = filter.GetComponent<MeshRenderer>(); var mesh = filter.sharedMesh;
                if (!renderer || !renderer.enabled || renderer.forceRenderingOff || hiddenLods.Contains(renderer) || !mesh || !mesh.isReadable) continue;
                if (parts.Count == 0) bounds = renderer.bounds; else bounds.Encapsulate(renderer.bounds);
                parts.Add(filter);
                unchecked { current = current * 31 + filter.GetInstanceID(); current = current * 31 + mesh.GetInstanceID(); current = current * 31 + renderer.bounds.GetHashCode(); current = current * 31 + mesh.vertexCount; }
            }
            if (parts.Count == 0) return false;
            if (current == signature && anchors.Count > 0) return true;
            signature = current; changed = true; anchors.Clear();
            int budget = Math.Max(1, 48 / parts.Count);
            foreach (var filter in parts)
            {
                var mesh = filter.sharedMesh; var vertices = mesh.vertices; var triangles = mesh.triangles;
                var cumulative = new float[triangles.Length / 3]; float area = 0;
                for (int i = 0; i < cumulative.Length; i++)
                {
                    var a = filter.transform.TransformPoint(vertices[triangles[i * 3]]);
                    var b = filter.transform.TransformPoint(vertices[triangles[i * 3 + 1]]);
                    var c = filter.transform.TransformPoint(vertices[triangles[i * 3 + 2]]);
                    area += Vector3.Cross(b - a, c - a).magnitude * .5f; cumulative[i] = area;
                }
                if (area <= .000001f) continue;
                for (int i = 0; i < budget && anchors.Count < 96; i++)
                {
                    float pick = (i + .5f) / budget * area;
                    int triangle = Array.BinarySearch(cumulative, pick); if (triangle < 0) triangle = ~triangle;
                    triangle = Math.Min(triangle, cumulative.Length - 1) * 3;
                    var a = filter.transform.TransformPoint(vertices[triangles[triangle]]);
                    var b = filter.transform.TransformPoint(vertices[triangles[triangle + 1]]);
                    var c = filter.transform.TransformPoint(vertices[triangles[triangle + 2]]);
                    var weights = CoverageLayout.Barycentric(MoteMotion.Hash(i + 13), MoteMotion.Hash(i + 71));
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    var anchor = SurfaceAnchor.At(filter.transform, a * weights.X + b * weights.Y + c * weights.Z + normal * .01f, null, normal);
                    anchor.Renderer = filter.GetComponent<MeshRenderer>(); anchor.RequiresRenderer = true;
                    anchors.Add(anchor);
                }
            }
            return anchors.Count > 0;
        }
    }
}
