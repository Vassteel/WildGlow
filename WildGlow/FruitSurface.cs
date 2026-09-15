using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildGlow
{
    // Valharvest 3.3.4 combines nine apples in one non-readable mesh. These are
    // the top vertices of its nine connected fruit components, in mesh space.
    // See assets/compatibility/valharvest-apples.md for provenance and validation.
    internal static class FruitSurface
    {
        internal static bool IsTree(string name) =>
            string.Equals(name, "apple_tree", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "apple_tree_1", StringComparison.OrdinalIgnoreCase);

        private static readonly Vector3[] Tops = {
            new Vector3(-0.2694197f, 1.9088567f, 0.3961818f),
            new Vector3(-0.2231439f, 2.4969592f, 0.2076921f),
            new Vector3(0.1994570f, 2.8139510f, -0.1562577f),
            new Vector3(0.2300830f, 2.2654240f, -0.0358363f),
            new Vector3(0.1607674f, 1.6506805f, -0.3321521f),
            new Vector3(-0.1815086f, 1.5798448f, -0.2563672f),
            new Vector3(-0.1948867f, 1.6882041f, 0.2664113f),
            new Vector3(0.4171014f, 1.8242480f, -0.1024285f),
            new Vector3(-0.1076642f, 2.0107777f, -0.0973235f),
        };

        internal static bool Prepare(GameObject fruit, List<SurfaceAnchor> anchors, out Bounds bounds)
        {
            anchors.Clear(); bounds = default;
            if (!fruit || !fruit.activeInHierarchy) return false;
            foreach (var filter in fruit.GetComponentsInChildren<MeshFilter>(false))
            {
                var mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!mesh || !renderer || !renderer.enabled || renderer.forceRenderingOff) continue;
                // Fail closed for a changed asset instead of attaching known points to another model.
                if (mesh.name != "apple" || mesh.vertexCount != 1792 ||
                    (mesh.bounds.center - new Vector3(.07420105f, 2.10389733f, .02755487f)).sqrMagnitude > .000001f ||
                    (mesh.bounds.extents - new Vector3(.41979003f, .71005362f, .41924551f)).sqrMagnitude > .000001f) continue;
                if (anchors.Count == 0) bounds = renderer.bounds; else bounds.Encapsulate(renderer.bounds);
                foreach (var top in Tops)
                {
                    var anchor = SurfaceAnchor.At(filter.transform,
                        filter.transform.TransformPoint(top + Vector3.up * .01f), null,
                        filter.transform.TransformDirection(Vector3.up));
                    anchor.Renderer = renderer; anchor.RequiresRenderer = true;
                    anchors.Add(anchor);
                }
            }
            return anchors.Count > 0;
        }
    }
}
