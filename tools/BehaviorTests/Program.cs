using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using WildGlow;

int checks = 0;
void Check(bool b, string text) { checks++; if (!b) throw new Exception(text); }
Check(EffectBehavior.Night(.5f) == 0 && EffectBehavior.Night(0) == 1 && EffectBehavior.Night(.95f) == 1, "Noon/midnight incorrect");
Check(Math.Abs(EffectBehavior.Night(.25f) - .5f) < .0001f, "Dawn transition not smooth");
Check(Math.Abs(EffectBehavior.Night(.75f) - .5f) < .0001f, "Dusk transition not smooth");
Check(EffectBehavior.Family("copper") == EffectFamily.Mineral && EffectBehavior.Family("moltencore") == EffectFamily.Magic, "Material categories wrong");
Check(EffectBehavior.Family("carrot") == EffectFamily.Plant && EffectBehavior.Family("mushroom") == EffectFamily.Spore, "Natural categories wrong");
// Regression: mushroom near-model spores and column must have the same rise pace.
for (int i = 0; i < 100; i++)
{
    float p = i / 100f; var anchor = new MoteMotion.Point(.4f, -.2f, .1f);
    var local = EffectBehavior.Path(EffectFamily.Spore, false, anchor, p, 3, 17, .16f, 3.6f, 1.45f, 1);
    var column = EffectBehavior.Path(EffectFamily.Spore, true, anchor, p, 3, 17, .32f, 3.6f, 1.45f, 1);
    Check(Math.Abs(local.Y - column.Y) < .00001f, "Mushroom spores bunch below faster column");
}
var dandelion = EffectBehavior.Position(EffectFamily.Plant, .5f, 3, 17, .16f, 3.6f, 1.45f, 1);
Check(Math.Abs(dandelion.Y - (.025f + .5f * 3.6f * .38f)) < .00001f, "Dandelion rise changed");
var signatures = new HashSet<string>();
var fixtures = new List<object>();
foreach (EffectFamily f in Enum.GetValues<EffectFamily>())
{
    var sample = EffectBehavior.Position(f, .43f, 12.4f, 17, .5f, 4, 1.45f, 1);
    signatures.Add($"{sample.X:F3}/{sample.Y:F3}/{sample.Z:F3}");
    foreach (float n in new[]{0f,.5f,1f}) foreach (bool column in new[]{false,true}) foreach (float p in new[]{0f,.1f,.43f,.8f,.999f})
    foreach(float twist in new[]{0f,1.45f,2f}) foreach(float speed in new[]{.25f,.9f,2f})
    {
        var anchor = new MoteMotion.Point(3,-1,2);
        var head = EffectBehavior.Path(f,column,anchor,p,12.4f,17,.5f,4,twist,n);
        Check(float.IsFinite(head.X) && float.IsFinite(head.Y) && float.IsFinite(head.Z), "Non-finite path");
        float alpha = EffectBehavior.Alpha(f,p,12.4f,17,n);
        Check(alpha >= 0 && alpha <= 1.001f, "Invalid alpha");
        foreach(float length in new[]{0f,1f,2f}) foreach(float fraction in new[]{0f,.5f,1f})
        {
            var tail = EffectBehavior.TrailPoint(f,column,anchor,p,12.4f,17,.5f,4,speed,twist,length,fraction,n);
            double d = Math.Sqrt(Math.Pow(head.X-tail.X,2)+Math.Pow(head.Y-tail.Y,2)+Math.Pow(head.Z-tail.Z,2));
            Check(d <= .22f * length + .00001f, "Trail exceeds cap");
            if(p==0) Check(d < .00001f, "Birth crosses previous particle cycle");
        }
        if(column && p > .99f) Check(head.Y > 3.9f, "Family lost its full-height vertical column");
    }
    foreach (float n in new[]{0f,1f}) foreach(bool column in new[]{false,true})
    {
        var anchor = new MoteMotion.Point(1,-.3f,.5f);
        var p = EffectBehavior.Path(f,column,anchor,.43f,12.4f,17,.5f,4,1.45f,n);
        var tail = EffectBehavior.TrailPoint(f,column,anchor,.43f,12.4f,17,.5f,4,.9f,1.45f,2,1,n);
        fixtures.Add(new { family=f.ToString().ToLowerInvariant(), night=n, column, position=new[]{p.X,p.Y,p.Z}, tail=new[]{tail.X,tail.Y,tail.Z} });
    }
}
Check(signatures.Count == 10, "Families have indistinguishable motion");
Console.WriteLine($"PASS: {checks:N0} behavior assertions across 10 families, day/night transitions, retained columns and bounded trails.");
if(args.Length > 0)
{
    var rows=File.ReadLines(Path.Combine(args[0],"effects.tsv")).Skip(1).Select(l=>l.Split('\t')).ToArray();
    foreach (var row in rows) Check(LightingProfile.GlowScale(row[0]) > 0, "Eligible style has no lighting defaults: " + row[0]);
    File.WriteAllText(Path.Combine(args[0],"previews/lighting-profiles.json"),JsonSerializer.Serialize(rows.ToDictionary(r=>r[0],r=>LightingProfile.GlowScale(r[0]))));
    Console.WriteLine("PASS: lighting defaults for all " + rows.Length + " catalog styles, plus automatic fallback.");
    File.WriteAllText(Path.Combine(args[0],"previews/behaviors.json"),JsonSerializer.Serialize(rows.ToDictionary(r=>r[0],r=>EffectBehavior.Family(r[0]).ToString().ToLowerInvariant())));
    File.WriteAllText(Path.Combine(args[0],"previews/behavior-fixtures.json"),JsonSerializer.Serialize(fixtures));
}
