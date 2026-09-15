using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;

// Verify binary API linkage against the installed game without executing Unity or loading a world.
string game = args[0], plugin = args[1];
var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.Combine(game, "valheim_Data", "Managed"));
resolver.AddSearchDirectory(Path.Combine(game, "BepInEx", "core"));
resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));
var parameters = new ReaderParameters { AssemblyResolver = resolver };
using var module = ModuleDefinition.ReadModule(plugin, parameters);
int checkedCount = 0, failures = 0;
foreach (var removed in new[] { "OreSurfaceSkin", "SurfaceDetails", "SurfacePattern" })
    if (module.Types.Any(t => t.Name == removed)) { Console.WriteLine("FAIL retired surface renderer shipped: " + removed); failures++; }

if (!module.Types.Any(t => t.Name == "ModelSpill") || !module.GetTypeReferences().Any(t => t.FullName == "UnityEngine.Light")) { Console.WriteLine("FAIL real light spill missing from plugin"); failures++; }
foreach (var reference in module.GetMemberReferences())
{
    string ns = reference.DeclaringType.Namespace;
    if (!(ns == "" || ns.StartsWith("UnityEngine") || ns.StartsWith("BepInEx") || ns.StartsWith("HarmonyLib"))) continue;
    try
    {
        object resolved = reference is MethodReference method ? method.Resolve() : reference is FieldReference field ? field.Resolve() : null;
        if (resolved == null) throw new Exception("unresolved member");
        checkedCount++;
    }
    catch (Exception ex) { Console.WriteLine("FAIL " + reference.FullName + ": " + ex.Message); failures++; }
}
using var gameModule = ModuleDefinition.ReadModule(Path.Combine(game, "valheim_Data", "Managed", "assembly_valheim.dll"), parameters);
foreach (var name in new[] { "Pickable", "PickableItem", "MineRock", "MineRock5", "Beehive", "Container", "DropOnDestroyed", "Destructible" })
{
    var type = gameModule.Types.Single(t => t.Name == name);
    var hook = type.Methods.SingleOrDefault(m => m.Name == (name == "MineRock" ? "Start" : "Awake") && !m.HasParameters && !m.IsStatic);
    if (hook == null) { Console.WriteLine("FAIL missing lifecycle hook: " + name); failures++; }
    else checkedCount++;
}
foreach (var pair in new[] { ("PickableItem", "m_picked"), ("MineRock5", "m_allDestroyed") })
{
    var field = gameModule.Types.Single(t => t.Name == pair.Item1).Fields.SingleOrDefault(f => f.Name == pair.Item2 && f.FieldType.FullName == "System.Boolean");
    if (field == null) { Console.WriteLine("FAIL state field " + pair); failures++; } else checkedCount++;
}
if (!gameModule.Types.Single(t => t.Name == "MineRock").Methods.Any(m => m.Name == "AllDestroyed" && m.ReturnType.FullName == "System.Boolean" && !m.HasParameters)) failures++;
else checkedCount++;
var resource = (EmbeddedResource)module.Resources.Single(r => r.Name == "WildGlow.effects.tsv");
using var reader = new StreamReader(resource.GetResourceStream());
reader.ReadLine(); string line; var ids = new HashSet<string>(); var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
while ((line = reader.ReadLine()) != null)
{
    var p = line.Split('\t');
    if (p.Length != 8 || !ids.Add(p[0])) { failures++; continue; }
    foreach (string a in p[7].Split(',').Where(a => a.Length > 0))
    {
        if (aliases.TryGetValue(a, out string prior) && prior != p[0]) { Console.WriteLine("FAIL conflicting alias " + a); failures++; }
        aliases[a] = p[0];
    }
}
foreach (var pair in new[] { ("Pickable_SeedCarrot", "carrot"), ("MineRock_Obsidian", "obsidian"), ("rock4_copper", "copper"), ("rock4_copper_frac", "copper"), ("Beehive", "honey"), ("LingonberryBush", "lingonberry") })
    if (!aliases.TryGetValue(pair.Item1, out string id) || id != pair.Item2) { Console.WriteLine("FAIL target mapping " + pair); failures++; }
foreach (var excluded in new[] { "Wood", "Stone", "Pickable_Branch", "Pickable_Stone", "Flint", "Pickable_Flint", "SurtlingCore", "Pickable_SurtlingCoreStand" })
    if (aliases.ContainsKey(excluded)) { Console.WriteLine("FAIL common item style " + excluded); failures++; }
Console.WriteLine($"Verified {checkedCount} binary API references/hooks and {ids.Count} embedded styles, {aliases.Count} aliases. Failures: {failures}.");
Environment.ExitCode = failures == 0 ? 0 : 1;
