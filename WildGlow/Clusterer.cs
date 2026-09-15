using System;
using System.Collections.Generic;
using System.Linq;

namespace WildGlow
{
    // Deterministic, bounded groups: a row of pickups cannot chain into a biome-wide beacon.
    internal static class Clusterer
    {
        internal struct Point
        {
            internal int Id;
            internal float X, Y, Z;
        }

        internal static List<List<int>> Build(IReadOnlyList<Point> points, float diameter)
        {
            var groups = new List<List<int>>();
            float limit = diameter * diameter;
            foreach (int index in Enumerable.Range(0, points.Count).OrderBy(i => points[i].Id))
            {
                List<int> chosen = null;
                float best = float.MaxValue;
                if (diameter > 0)
                {
                    foreach (var group in groups)
                    {
                        float furthest = 0;
                        foreach (int member in group)
                        {
                            float dx = points[index].X - points[member].X;
                            float dy = points[index].Y - points[member].Y;
                            float dz = points[index].Z - points[member].Z;
                            furthest = Math.Max(furthest, dx * dx + dy * dy + dz * dz);
                            if (furthest > limit) break;
                        }
                        if (furthest <= limit && furthest < best) { chosen = group; best = furthest; }
                    }
                }
                if (chosen == null) { chosen = new List<int>(); groups.Add(chosen); }
                chosen.Add(index);
            }
            return groups;
        }
    }
}
