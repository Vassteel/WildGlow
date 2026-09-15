using System;
using System.Collections.Generic;
using WildGlow;

int checks = 0;
void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
float Axis(MoteMotion.Point p, int a) => a == 0 ? p.X : a == 1 ? p.Y : p.Z;
foreach (float size in new[] { .1f, 1f, 16f }) foreach (float offset in new[] { -2000f, 0, 2000f })
{
    var min = new MoteMotion.Point(offset - size, -3 * size, offset);
    var max = new MoteMotion.Point(offset + size, 2 * size, offset + 4 * size);
    var origins = new HashSet<string>();
    var directions = new HashSet<string>();
    for (int face = 0; face < SurfaceSampling.FaceCount; face++) for (int i = 0; i < SurfaceSampling.RaysPerFace; i++)
    {
        var ray = SurfaceSampling.Ray(min, max, face, i);
        origins.Add($"{ray.Origin.X},{ray.Origin.Y},{ray.Origin.Z}");
        directions.Add($"{ray.Direction.X},{ray.Direction.Y},{ray.Direction.Z}");
        // Independent slab intersection: every ray enters the box, traverses it, and exits before its limit.
        float enter = float.NegativeInfinity, exit = float.PositiveInfinity;
        for (int a = 0; a < 3; a++)
        {
            float origin = Axis(ray.Origin, a), direction = Axis(ray.Direction, a), low = Axis(min, a), high = Axis(max, a);
            if (direction == 0) { Check(origin > low && origin < high, "Ray misses face interior"); continue; }
            float near = (low - origin) / direction, far = (high - origin) / direction;
            enter = Math.Max(enter, Math.Min(near, far)); exit = Math.Min(exit, Math.Max(near, far));
        }
        Check(enter > 0 && exit > enter && exit < ray.Reach, "Ray cannot traverse the whole deposit");
    }
    Check(origins.Count == 216 && directions.Count == 6, "Missing face coverage or duplicate rays");
}

// Uniform triangle sampling must remain on the model, including its edges, at every tested seed.
for (int i = 0; i <= 100; i++) for (int j = 0; j <= 100; j++)
{
    var w = CoverageLayout.Barycentric(i / 100f, j / 100f);
    Check(w.X >= 0 && w.Y >= 0 && w.Z >= 0 && Math.Abs(w.X+w.Y+w.Z-1) < .00001f, "Mesh sample leaves its triangle");
}
// Light budgeting counts individual Light components, including when groups want no lights.
for (int budget = 0; budget <= 16; budget++) for (int n = 0; n <= 32; n++)
{
    var wanted = new int[n]; for (int i = 0; i < n; i++) wanted[i] = i % 5;
    var actual = CoverageLayout.Allocate(wanted, budget); int total = 0, demand = 0;
    for (int i = 0; i < n; i++) { total += actual[i]; demand += wanted[i]; Check(actual[i] >= 0 && actual[i] <= wanted[i], "Group light budget exceeded"); }
    Check(total == Math.Min(demand, budget), "Global light budget leaked or left unused slots");
}
foreach (float size in new[] { .2f, 2f, 8f })
{
    var points = new List<MoteMotion.Point>();
    for (int x = -4; x <= 4; x++) for (int z = -4; z <= 4; z++) points.Add(new MoteMotion.Point(x*size, (float)Math.Sin(x)*size, z*size));
    var selected = CoverageLayout.Select(points, 4, .01f);
    Check(selected.Length == 4 && new HashSet<int>(selected).Count == 4, "Duplicate coverage lights");
    float previous = float.MaxValue;
    for (int count = 1; count <= 4; count++)
    {
        float radius = CoverageLayout.CoverRadius(points, selected, count);
        Check(radius <= previous + .00001f, "Additional lights worsen coverage"); previous = radius;
        foreach (var p in points)
        {
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++) nearest = Math.Min(nearest, CoverageLayout.Distance2(p, points[selected[i]]));
            Check(nearest <= radius*radius + .001f, "A model surface point lies outside coverage");
        }
    }
}
var duplicates = new List<MoteMotion.Point> { default, default, default };
Check(CoverageLayout.Select(duplicates, 32, .1f).Length == 1, "Tiny models accumulate coincident lights");
Check(CoverageLayout.Select(duplicates, 0, .1f).Length == 0, "Zero light budget still selects points");
Check(CoverageLayout.Select(new List<MoteMotion.Point>(), 4, 0).Length == 0, "Empty model gets floating lights");
foreach (string name in new[] { "Beech1", "Beech_small1", "beech_small2", "Beech_Sapling", "FirTree_small", "FirTree_small_dead", "PineTree", "PineTree_Sapling", "Pinetree_Snow", "SnowFirTree_small", "shrub_2", "shrub_2_heath" })
    Check(TreeFilter.Excluded(name), "Tree/shrub still eligible: " + name);
foreach (string name in new[] { "RaspberryBush", "BlueberryBush", "Pickable_Dandelion", "BeechSeeds", "FirCone", "PineCone", "Mushroom" })
    Check(!TreeFilter.Excluded(name), "Tree filter caught a collectible: " + name);
// Sources must stay clear of the model and reach all bounds corners plus a 3 m apron.
foreach (float x in new[] { .15f, 1f, 6f, 12f }) foreach (float y in new[] { .1f, 1f, 4f })
foreach (float z in new[] { .15f, 2f, 8f }) foreach (int count in new[] { 1, 2, 3, 4 })
{
    float range = SpillLayout.Range(x, y, z, 3);
    float total = 0;
    for (int i = 0; i < count; i++)
    {
        var p = SpillLayout.Position(x, y, z, i, count);
        Check(p.Y > y + .6f, "A spill light sits in or against the model");
        foreach (int sx in new[] { -1, 1 }) foreach (int sy in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
        {
            var corner = new MoteMotion.Point(sx*x, sy*y, sz*z);
            Check(Math.Sqrt(CoverageLayout.Distance2(p, corner)) + 2.99f < range, "Spill fails to cover the whole model and its surroundings");
        }
        total += SpillLayout.Intensity(.6f, 1, count);
    }
    Check(Math.Abs(total - .6f) < .00001f, "Adding sources multiplies total brightness");
}
Check(SpillLayout.Intensity(0,1,4)==0 && SpillLayout.Intensity(1,1,0)==0, "Disabled light spill remains active");
Check(SpillLayout.Intensity(.6f,0,1)<SpillLayout.Intensity(.6f,1,1), "Day spill is not quieter");
Check(LightingProfile.GlowScale("mushroom")>1 && LightingProfile.GlowScale("mushroomyellow")>1, "Mushrooms not brighter than 0.4.3");
Check(LightingProfile.GlowScale("blueberry")<.1f && LightingProfile.GlowScale("copper")<.1f, "Bush/ore emission not reduced");
Check(LightingProfile.ExcludedSource("Spawner_GreydwarfNest") && LightingProfile.ExcludedSource("spawner_greydwarfnest_broken"), "Greydwarf nest not excluded");
Check(!LightingProfile.ExcludedSource("Beehive") && !LightingProfile.ExcludedSource("AncientSeed"), "Nest exclusion caught other collectibles");
// Structures must be rejected before their resource drops can seed a fallback effect.
foreach (string name in new[] { "goblin_roof_45d", "goblin_roof_45d_corner", "goblin_roof_cap", "goblin_woodwall_2m_ribs", "goblin_fence", "goblin_pole", "goblin_totempole", "goblin_strawpile", "lox_ribs", "lox_ribs_broken", "wood_door", "dungeon_sunkencrypt_irongate", "dvergrtown_wood_wall", "Modded_Hut", "stone_pillar2" })
    Check(StructureFilter.Excluded(name,false,false,false), "Structural drop source is still eligible: " + name);
Check(StructureFilter.Excluded("modded_building",true,false,false), "Building component ignored");
Check(StructureFilter.Excluded("modded_portaldoor",false,true,false), "Door component ignored");
Check(StructureFilter.Excluded("goblin_roof_45d",false,false,true), "Known architecture bypassed exclusion with a collectible component");
foreach (string name in new[] { "TreasureChest_plains", "Beehive", "Pickable_Mushroom", "Pickable_GoblinTotem", "RaspberryBush", "MineRock_Tin" })
    Check(!StructureFilter.Excluded(name,true,false,true), "Collectible inside a building was excluded: " + name);
foreach (string name in new[] { "rock4_copper", "rock4_copper_frac", "silvervein", "mudpile", "Pickable_SeedCarrot", "AncientSeed", "GoblinTotem" })
    Check(!StructureFilter.Excluded(name,false,false,false), "Resource source was excluded: " + name);
Console.WriteLine($"Surface tests passed: {checks} checks for six-face ore rays, triangle attachment, distributed coverage and global light budgets.");
