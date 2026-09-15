using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WildGlow
{
    internal sealed class TargetGroup
    {
        internal readonly List<Target> Members;
        internal readonly Style Style;
        internal readonly int Id;
        internal float Distance;
        internal readonly List<SurfaceAnchor> Anchors = new List<SurfaceAnchor>();
        internal bool IsDeposit;
        internal bool IsFruitTree;
        internal readonly List<Vector3> FruitLightPositions = new List<Vector3>();
        internal int SurfaceRevision;
        internal float SurfaceRadius;
        internal Bounds LightBounds;
        internal bool HasLightBounds;
        internal int SpillBudget;
        internal void PrepareSurface()
        {
            Anchors.Clear(); FruitLightPositions.Clear(); SurfaceRevision = 17; SurfaceRadius = 0; IsDeposit = false; IsFruitTree = false;
            foreach (var member in Members)
            {
                member.PrepareSurface(); IsDeposit |= member.IsDeposit;
                IsFruitTree |= member.IsFruitTree;
                SurfaceRadius = Mathf.Max(SurfaceRadius, member.SurfaceRadius);
                unchecked { SurfaceRevision = SurfaceRevision * 31 + member.Root.GetInstanceID(); SurfaceRevision = SurfaceRevision * 31 + member.SurfaceRevision; }
                int budget = Mathf.Max(1, 100 / Members.Count);
                for (int i = 0; i < Mathf.Min(budget, member.Surface.Count); i++)
                    Anchors.Add(member.Surface[i * member.Surface.Count / Mathf.Min(budget, member.Surface.Count)]);
            }
            if (TryCenter(out var center))
                foreach (var a in Anchors) if (a.Valid) SurfaceRadius = Mathf.Max(SurfaceRadius, Vector3.Distance(center, a.World));
            RefreshLightBounds();
            if (IsFruitTree)
            {
                var points = Anchors.Where(a => a.Valid).Select(a => new MoteMotion.Point(a.World.x, a.World.y, a.World.z)).ToList();
                foreach (int index in CoverageLayout.Select(points, 4, .1f))
                    FruitLightPositions.Add(new Vector3(points[index].X, points[index].Y, points[index].Z));
            }
        }

        internal void RefreshLightBounds()
        {
            HasLightBounds = false;
            foreach (var a in Anchors)
            {
                if (!a.Valid) continue;
                // Ignore buried samples when fitting the visible deposit's light volume.
                var p = a.World;
                if (IsDeposit && Heightmap.GetHeight(p, out var ground) && p.y < ground - .15f) continue;
                if (!HasLightBounds) { LightBounds = new Bounds(p, Vector3.zero); HasLightBounds = true; }
                else LightBounds.Encapsulate(p);
            }
            // Underground rooms may be below the terrain height field. Keep their eligible nodes lit.
            if (!HasLightBounds)
                foreach (var a in Anchors)
                {
                    if (!a.Valid) continue;
                    if (!HasLightBounds) { LightBounds = new Bounds(a.World, Vector3.zero); HasLightBounds = true; }
                    else LightBounds.Encapsulate(a.World);
                }
        }

        internal TargetGroup(List<Target> members)
        {
            Members = members;
            Id = members.Min(t => t.Root.GetInstanceID());
            // Keep one readable item identity: majority style, deterministic tie break.
            Style = members.GroupBy(t => t.Style.Id).OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, System.StringComparer.Ordinal).First().First().Style;
        }

        internal bool TryCenter(out Vector3 center)
        {
            center = Vector3.zero;
            int count = 0;
            foreach (var member in Members)
            {
                if (!member.Root || !member.Root.activeInHierarchy) continue;
                center += member.Center; count++;
            }
            if (count == 0) return false;
            center /= count;
            return true;
        }
    }
}
