using System;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WildGlow
{
    internal sealed class Target
    {
        private static readonly FieldInfo PickedItem = AccessTools.Field(typeof(PickableItem), "m_picked");
        private static readonly FieldInfo Rock5Destroyed = AccessTools.Field(typeof(MineRock5), "m_allDestroyed");
        private static readonly MethodInfo RockDestroyed = AccessTools.Method(typeof(MineRock), "AllDestroyed");
        internal readonly GameObject Root;
        internal readonly Style Style;
        internal readonly string Category;
        internal float Distance;
        private readonly Pickable pickable;
        private readonly PickableItem pickableItem;
        private readonly Container container;
        private readonly MineRock rock;
        private readonly MineRock5 rock5;
        private readonly Piece piece;
        private readonly ZNetView nview;
        private readonly Collider[] colliders;
        private Vector3 localCenter;
        private readonly List<SurfaceAnchor> surface = new List<SurfaceAnchor>();
        private int surfaceSignature, modelSignature;
        internal int SurfaceRevision;
        internal float SurfaceRadius;
        internal bool IsDeposit => EffectBehavior.Geological(EffectBehavior.Family(Style.Id));
        internal bool IsFruitTree => FruitSurface.IsTree(Styles.Clean(Root.name));
        internal GameObject VisualRoot => IsFruitTree ? (pickable ? pickable.m_hideWhenPicked : null) : Root;
        internal IReadOnlyList<SurfaceAnchor> Surface => surface;


        private Target(GameObject root, Style style, string category)
        {
            Root = root; Style = style; Category = category;
            pickable = root.GetComponent<Pickable>(); pickableItem = root.GetComponent<PickableItem>();
            container = root.GetComponent<Container>(); rock = root.GetComponent<MineRock>(); rock5 = root.GetComponent<MineRock5>();
            piece = root.GetComponent<Piece>(); nview = root.GetComponent<ZNetView>();
            colliders = root.GetComponentsInChildren<Collider>(true);
            // Use physical bounds rather than an object's possibly buried/offset origin. Ignore trigger volumes.
            bool found = false; Bounds bounds = default;
            foreach (var c in colliders)
            {
                if (!c || !c.enabled || c.isTrigger || !c.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = c.bounds; found = true; } else bounds.Encapsulate(c.bounds);
            }
            Vector3 center = found ? bounds.center : root.transform.position;
            if (found) center.y = category == "Minerals" ? bounds.max.y - Mathf.Min(bounds.size.y * 0.15f, 0.6f) : bounds.min.y + Mathf.Min(bounds.size.y * 0.45f, 0.65f);
            localCenter = root.transform.InverseTransformPoint(center);
            if (IsFruitTree) PrepareSurface();
        }

        internal Vector3 Center => Root.transform.TransformPoint(localCenter);

        internal void PrepareSurface()
        {
            if (!Root) return;
            if (IsFruitTree)
            {
                // Only the harvestable apples may seed particles, emission and spill.
                // Never fall back to the trunk collider when this mesh is unavailable.
                SurfaceRevision++;
                SurfaceRadius = 0;
                if (FruitSurface.Prepare(VisualRoot, surface, out var fruitBounds))
                {
                    localCenter = Root.transform.InverseTransformPoint(fruitBounds.center);
                    SurfaceRadius = fruitBounds.extents.magnitude;
                }
                return;
            }
            if (!IsDeposit && ModelSurface.Prepare(Root, surface, ref modelSignature, out var modelBounds, out var changed))
            {
                if (changed) SurfaceRevision++;
                localCenter = Root.transform.InverseTransformPoint(modelBounds.center);
                SurfaceRadius = modelBounds.extents.magnitude;
                return;
            }
            bool found = false; Bounds bounds = default;
            int signature = 17;
            foreach (var c in colliders)
            {
                if (!Usable(c)) continue;
                if (!found) { bounds = c.bounds; found = true; } else bounds.Encapsulate(c.bounds);
                unchecked { signature = signature * 31 + c.GetInstanceID(); signature = signature * 31 + c.bounds.GetHashCode(); }
            }
            if (!found) { surface.Clear(); SurfaceRevision++; return; }
            if (signature == surfaceSignature && surface.Count > 0) return;
            surfaceSignature = signature; SurfaceRevision++; surface.Clear();
            SurfaceRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            // Sample all six sides of the physical node, including newly exposed walls/undersides.
            // Each ray only tests this deposit; adjacent trees/rocks cannot move its effects.
            var min = new MoteMotion.Point(bounds.min.x, bounds.min.y, bounds.min.z);
            var max = new MoteMotion.Point(bounds.max.x, bounds.max.y, bounds.max.z);
            for (int face = 0; face < SurfaceSampling.FaceCount; face++) for (int i = 0; i < SurfaceSampling.RaysPerFace; i++)
            {
                var cast = SurfaceSampling.Ray(min, max, face, i);
                var ray = new Ray(TrailMesh.ToVector(cast.Origin), TrailMesh.ToVector(cast.Direction));
                float reach = cast.Reach;
                Collider selected = null; RaycastHit best = default; float distance = float.MaxValue;
                foreach (var c in colliders)
                {
                    if (!Usable(c)) continue;
                    if (c.Raycast(ray, out var hit, reach) && hit.distance < distance)
                    { selected = c; best = hit; distance = hit.distance; }
                }
                if (selected) surface.Add(SurfaceAnchor.At(selected.transform, best.point + best.normal * .015f, selected, best.normal));
            }
            if (surface.Count > 0)
            {
                var nearest = surface[0]; float distance = float.MaxValue;
                foreach (var anchor in surface)
                {
                    if (anchor.WorldNormal.y < .25f) continue;
                    var d = anchor.World - bounds.center; d.y = 0;
                    if (d.sqrMagnitude < distance) { distance = d.sqrMagnitude; nearest = anchor; }
                }
                // The column and light start on a sampled rock surface, not the union-bounds ceiling.
                localCenter = Root.transform.InverseTransformPoint(nearest.World);
            }
        }
        private static bool Usable(Collider c) => c && c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy;

        internal bool Available()
        {
            if (!Root || !Root.activeInHierarchy || !nview || !nview.IsValid()) return false;
            if (!Plugin.IncludeBuilt.Value && piece && piece.GetCreator() != 0) return false;
            // Grown crops lose their Piece/creator in vanilla. Cultivated soil remains the reliable local signal.
            if (!Plugin.IncludeBuilt.Value && pickable && !Styles.Clean(Root.name).EndsWith("_Wild", StringComparison.OrdinalIgnoreCase))
            {
                var ground = Heightmap.FindHeightmap(Root.transform.position);
                if (ground && ground.IsCultivated(Root.transform.position)) return false;
            }
            if (pickable && (pickable.GetPicked() || !pickable.CanBePicked())) return false;
            if (pickableItem && PickedItem != null && (bool)PickedItem.GetValue(pickableItem)) return false;
            if (container && (container.GetInventory() == null || container.GetInventory().NrOfItems() == 0)) return false;
            if (rock5 && Rock5Destroyed != null && (bool)Rock5Destroyed.GetValue(rock5)) return false;
            if (rock && RockDestroyed != null && (bool)RockDestroyed.Invoke(rock, null)) return false;
            if (rock || rock5)
            {
                foreach (var c in colliders) if (c && c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy) return true;
                return false;
            }
            return true;
        }

        internal static Target Create(GameObject root)
        {
            string name = Styles.Clean(root.name);
            if (Plugin.Excluded.Contains(name) || LightingProfile.ExcludedSource(name) || TreeFilter.Excluded(name) || root.GetComponent<TreeBase>() || root.GetComponent<TreeLog>()) return null;
            var view = root.GetComponent<ZNetView>();
            if (!view || !view.IsValid()) return null;
            // Dropped items already have vanilla VFX. Exclude before any other classification.
            if (root.GetComponentInParent<ItemDrop>()) return null;
            // Displayed equipment / item stands are not loose pickups.
            if (IsWildlife(root) || root.GetComponentInParent<ItemStand>()) return null;
            var p = root.GetComponent<Pickable>();
            var pi = root.GetComponent<PickableItem>();
            var mr = root.GetComponent<MineRock>();
            var mr5 = root.GetComponent<MineRock5>();
            var hive = root.GetComponent<Beehive>();
            var chest = root.GetComponent<Container>();
            bool collectible = p || pi || mr || mr5 || hive || chest || name.Equals("Beehive", StringComparison.OrdinalIgnoreCase);
            if (StructureFilter.Excluded(name, root.GetComponentInParent<Piece>() || root.GetComponentInParent<WearNTear>(),
                root.GetComponentInParent<Door>(), collectible)) return null;
            if (p) return FromItem(root, p.m_itemPrefab, "Plants & pickups");
            if (pi) return FromItem(root, pi.m_itemPrefab ? pi.m_itemPrefab.gameObject : null, "Plants & pickups");
            if (mr || mr5)
            {
                var item = BestDrop(mr ? mr.m_dropItems : mr5.m_dropItems);
                if (!item) return null; // Ordinary stone and wood structures never become ore beacons.
                return FromItem(root, item, "Minerals");
            }
            if (hive || name.Equals("Beehive", StringComparison.OrdinalIgnoreCase)) return new Target(root, Styles.ById["honey"], "Beehives");
            if (chest)
            {
                if (chest.m_wagon || root.GetComponentInParent<Ship>() || root.GetComponent<TombStone>()) return null;
                return new Target(root, Styles.ById["chest"], "Chests");
            }
            var drops = root.GetComponent<DropOnDestroyed>();
            // Includes breakable wild beehives, mineral nodes, amber and dungeon loot; structures were excluded above.
            if (drops) return FromItem(root, BestDrop(drops.m_dropWhenDestroyed), "Breakables");
            var intact = root.GetComponent<Destructible>();
            if (intact && intact.m_spawnWhenDestroyed)
            {
                // Copper, silver, scrap piles and skulls start as a Destructible, then spawn a MineRock5.
                // Inspect the replacement prefab without instantiating it or rolling its loot table.
                var item = ReplacementDrop(intact.m_spawnWhenDestroyed, 0);
                if (item) return FromItem(root, item, "Minerals");
            }
            return null;
        }

        private static bool IsWildlife(GameObject root)
        {
            // Ambient birds are not Characters. Check both controller ancestors and child models
            // before DropOnDestroyed can mistake their feathers/meat for a collectible deposit.
            return root.GetComponentInParent<Character>() || root.GetComponentInChildren<Character>(true)
                || root.GetComponentInParent<BaseAI>() || root.GetComponentInChildren<BaseAI>(true)
                || root.GetComponentInParent<RandomFlyingBird>() || root.GetComponentInChildren<RandomFlyingBird>(true)
                || root.GetComponentInParent<Fish>() || root.GetComponentInChildren<Fish>(true)
                || root.GetComponent<Leviathan>();
        }

        private static GameObject ReplacementDrop(GameObject prefab, int depth)
        {
            if (!prefab || depth > 3) return null;
            var rock = prefab.GetComponent<MineRock>();
            if (rock) return BestDrop(rock.m_dropItems);
            var rock5 = prefab.GetComponent<MineRock5>();
            if (rock5) return BestDrop(rock5.m_dropItems);
            var drop = prefab.GetComponent<DropOnDestroyed>();
            if (drop) return BestDrop(drop.m_dropWhenDestroyed);
            var destructible = prefab.GetComponent<Destructible>();
            return destructible ? ReplacementDrop(destructible.m_spawnWhenDestroyed, depth + 1) : null;
        }

        private static Target FromItem(GameObject root, GameObject item, string category)
        {
            if (!item || Plugin.Excluded.Contains(Styles.Clean(item.name))) return null;
            return new Target(root, Styles.Resolve(item, root.name), category);
        }

        private static GameObject BestDrop(DropTable table)
        {
            if (table?.m_drops == null) return null;
            GameObject best = null; float score = -1;
            foreach (var d in table.m_drops)
            {
                if (!d.m_item || Plugin.Excluded.Contains(Styles.Clean(d.m_item.name))) continue;
                // Prefer an ore/resource over incidental creature loot in mixed deposits.
                string n = d.m_item.name;
                float weight = d.m_weight + (n.IndexOf("Ore", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Scrap", StringComparison.OrdinalIgnoreCase) >= 0 ? 10000 : 0);
                if (weight > score) { best = d.m_item; score = weight; }
            }
            return best;
        }
    }
}
