using System;
using System.Collections.Generic;
using System.Linq;
using WildGlow;

int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
Clusterer.Point P(int id, float x, float y = 0, float z = 0) => new() { Id = id, X = x, Y = y, Z = z };
string Membership(List<Clusterer.Point> points, float distance) => string.Join(";", Clusterer.Build(points, distance)
    .Select(g => string.Join(",", g.Select(i => points[i].Id).OrderBy(i => i))).OrderBy(g => g));

var pile = new List<Clusterer.Point> { P(9, 0), P(1, 0.2f), P(8, -0.3f), P(2, 0, 0.1f) };
Check(Clusterer.Build(pile, 2.5f).Count == 1, "A close mixed pickup pile must use one effect");
Check(Clusterer.Build(pile, 0).Count == 4, "Merging disabled must preserve all effects");
Check(Clusterer.Build(new[] { P(1, 0), P(2, 0, 3) }, 2.5f).Count == 2, "Separate floors must not merge");
var chain = Enumerable.Range(0, 20).Select(i => P(i, i * 2f)).ToList();
Check(Clusterer.Build(chain, 2.5f).Count == 10, "A chain of pickups must not collapse into one huge group");
var remaining = pile.Where(p => p.Id != 1).ToList();
Check(Clusterer.Build(remaining, 2.5f).Count == 1, "Harvesting the representative must retain the group's effect");
Check(Clusterer.Build(Array.Empty<Clusterer.Point>(), 2.5f).Count == 0, "Depleted groups must disappear");
var moved = new List<Clusterer.Point>(remaining) { P(1, 20) };
Check(Clusterer.Build(moved, 2.5f).Count == 2, "Moving a pickup out of its pile must split effects");
Check(Clusterer.Build(new[] { P(1, 0), P(2, 2.5f) }, 2.5f).Count == 1, "Boundary pickups must merge");

var random = new Random(145);
for (int run = 0; run < 40; run++)
{
    var points = Enumerable.Range(0, 100).Select(i => P(i,
        (float)random.NextDouble() * 20, (float)random.NextDouble() * 4, (float)random.NextDouble() * 20)).ToList();
    var groups = Clusterer.Build(points, 2.5f);
    Check(groups.SelectMany(g => g).OrderBy(i => i).SequenceEqual(Enumerable.Range(0, points.Count)), "Every target must occur exactly once");
    Check(Membership(points, 2.5f) == Membership(points.OrderBy(_ => random.Next()).ToList(), 2.5f), "Discovery/camera ordering must not change groups");
    foreach (var group in groups) foreach (int a in group) foreach (int b in group)
    {
        double d = Math.Pow(points[a].X - points[b].X, 2) + Math.Pow(points[a].Y - points[b].Y, 2) + Math.Pow(points[a].Z - points[b].Z, 2);
        Check(d <= 6.250001, "A group exceeds its configured diameter");
    }
}
Console.WriteLine($"PASS: {checks:N0} grouping assertions (merging, separation, depletion, movement, bounded extent, stable membership).");
