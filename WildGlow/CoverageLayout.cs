using System;
using System.Collections.Generic;

namespace WildGlow
{
    // Deterministic, spatially separated attachment selection for glow and real lights.
    internal static class CoverageLayout
    {
        internal static MoteMotion.Point Barycentric(float u, float v)
        {
            float r = (float)Math.Sqrt(Math.Max(0, Math.Min(1, u)));
            return new MoteMotion.Point(1 - r, r * (1 - v), r * v);
        }
        internal static float Distance2(MoteMotion.Point a, MoteMotion.Point b) =>
            (a.X-b.X)*(a.X-b.X) + (a.Y-b.Y)*(a.Y-b.Y) + (a.Z-b.Z)*(a.Z-b.Z);
        internal static int[] Select(IReadOnlyList<MoteMotion.Point> points, int budget, float spacing)
        {
            var result = new List<int>(); if (points.Count == 0 || budget <= 0) return result.ToArray();
            var center = new MoteMotion.Point();
            foreach (var p in points) { center.X += p.X / points.Count; center.Y += p.Y / points.Count; center.Z += p.Z / points.Count; }
            int first = 0; float best = float.MaxValue;
            for (int i = 0; i < points.Count; i++) { float d = Distance2(points[i], center); if (d < best) { best = d; first = i; } }
            result.Add(first);
            while (result.Count < budget && result.Count < points.Count)
            {
                int next = -1; float farthest = spacing * spacing;
                for (int i = 0; i < points.Count; i++)
                {
                    if (result.Contains(i)) continue;
                    float nearest = float.MaxValue;
                    foreach (int selected in result) nearest = Math.Min(nearest, Distance2(points[i], points[selected]));
                    if (nearest > farthest) { farthest = nearest; next = i; }
                }
                if (next < 0) break; result.Add(next);
            }
            return result.ToArray();
        }
        // Radius of the largest uncovered gap when using the first count selected anchors.
        internal static float CoverRadius(IReadOnlyList<MoteMotion.Point> points, int[] selected, int count)
        {
            count = Math.Min(count, selected.Length); if (count <= 0) return 0;
            float gap = 0;
            foreach (var p in points)
            {
                float nearest = float.MaxValue;
                for (int i = 0; i < count; i++) nearest = Math.Min(nearest, Distance2(p, points[selected[i]]));
                gap = Math.Max(gap, nearest);
            }
            return (float)Math.Sqrt(gap);
        }
        internal static int[] Allocate(int[] requested, int budget)
        {
            var assigned = new int[requested.Length];
            // Give each nearby group one light before filling large models' remaining coverage.
            for (int pass = 0; pass < 4 && budget > 0; pass++)
                for (int i = 0; i < requested.Length && budget > 0; i++)
                    if (assigned[i] < requested[i]) { assigned[i]++; budget--; }
            return assigned;
        }
    }
}
