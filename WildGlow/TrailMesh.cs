using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace WildGlow
{
    // One dynamic mesh per effect, reused. No TrailRenderer objects per individual particle.
    internal sealed class TrailMesh : IDisposable
    {
        private const int Sections = 5, MaxTrails = 64, VerticesPerTrail = (Sections + 1) * 2;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly Vector3[] vertices = new Vector3[MaxTrails * VerticesPerTrail];
        private readonly Color[] colors = new Color[MaxTrails * VerticesPerTrail];
        private readonly Vector2[] uv = new Vector2[MaxTrails * VerticesPerTrail];
        private readonly int[] triangles = new int[MaxTrails * Sections * 6];
        private int count;

        internal TrailMesh(Transform parent, Material material)
        {
            var go = new GameObject("Short trails") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            mesh = new Mesh { name = "WildGlow short trail ribbons", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            for (int i = 0; i < MaxTrails; i++) for (int j = 0; j <= Sections; j++)
            {
                int v = i * VerticesPerTrail + j * 2;
                uv[v] = new Vector2(0, (float)j / Sections); uv[v + 1] = new Vector2(1, (float)j / Sections);
                if (j == Sections) continue;
                int t = (i * Sections + j) * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.triangles = triangles;
        }

        internal void Begin() { count = 0; }
        internal void Add(EffectFamily family, bool column, MoteMotion.Point anchor, float night, float phase, float time, int key, float radius, float height, float speed, float twist, float length, float size, float alpha, Color tint, Vector3 cameraLocal, Vector3 surfaceNormal)
        {
            if (count >= MaxTrails || length <= 0 || alpha <= 0.001f || phase <= 0) return;
            Vector3 previous = ToVector(EffectBehavior.Path(family, column, anchor, phase, time, key, radius, height, twist, night));
            previous = Orient(previous, anchor, surfaceNormal);
            for (int j = 0; j <= Sections; j++)
            {
                float fraction = (float)j / Sections;
                Vector3 p = ToVector(EffectBehavior.TrailPoint(family, column, anchor, phase, time, key, radius, height, speed, twist, length, fraction, night));
                Vector3 next = ToVector(EffectBehavior.TrailPoint(family, column, anchor, phase, time, key, radius, height, speed, twist, length, Mathf.Min(1, fraction + 1f / Sections), night));
                p = Orient(p, anchor, surfaceNormal); next = Orient(next, anchor, surfaceNormal);
                Vector3 tangent = j == Sections ? p - previous : next - p;
                Vector3 side = Vector3.Cross(tangent, cameraLocal - p).normalized;
                if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                float width = size * 0.11f * (1 - fraction);
                int v = count * VerticesPerTrail + j * 2;
                vertices[v] = p - side * width; vertices[v + 1] = p + side * width;
                tint.a = alpha * 0.65f * (1 - fraction) * (1 - fraction);
                colors[v] = colors[v + 1] = tint;
                previous = p;
            }
            count++;
        }

        internal void End(float radius, float height)
        {
            renderer.enabled = count > 0;
            if (count == 0) return;
            // Explicitly clear unused ribbons so density reductions and births cannot leave old trails.
            Array.Clear(colors, count * VerticesPerTrail, colors.Length - count * VerticesPerTrail);
            mesh.vertices = vertices; mesh.colors = colors;
            mesh.bounds = new Bounds(new Vector3(0, height * 0.5f, 0), new Vector3(radius * 2 + 1, height + radius * 2 + 1, radius * 2 + 1));
        }
        internal static Vector3 Orient(Vector3 p, MoteMotion.Point anchor, Vector3 normal)
        {
            if (normal.sqrMagnitude < .01f) return p;
            var origin = ToVector(anchor);
            return origin + Quaternion.FromToRotation(Vector3.up, normal) * (p - origin);
        }
        internal static Vector3 ToVector(MoteMotion.Point p) => new Vector3(p.X, p.Y, p.Z);
        public void Dispose() { if (mesh) UnityEngine.Object.Destroy(mesh); }
    }
}
