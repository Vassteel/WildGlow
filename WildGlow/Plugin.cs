using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace WildGlow
{
    [BepInPlugin(Guid, "WildGlow", "0.4.5")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "local.valheim.wildglow";
        internal static Plugin Instance;
        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> IncludeBuilt;
        internal static HashSet<string> Excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private ConfigEntry<bool> enabledMod;
        private ConfigEntry<string> variantMode;
        private ConfigEntry<float> range, density, height, radius, brightness, speed, dust, trailLength, twist, areaGlow, spillIntensity, spillRange, mergeDistance;
        private ConfigEntry<int> maxEffects, maxSpillLights;
        private ConfigEntry<string> exclusions;
        private readonly Dictionary<int, GameObject> pending = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Target> targets = new Dictionary<int, Target>();
        private readonly Dictionary<int, MoteEffect> active = new Dictionary<int, MoteEffect>();
        private readonly List<int> dead = new List<int>();
        private readonly List<Target> near = new List<Target>();
        private readonly Dictionary<int, TargetGroup> groups = new Dictionary<int, TargetGroup>();
        private readonly Dictionary<int, string> activeStyles = new Dictionary<int, string>();
        private Harmony harmony;
        private float nextRefresh;
        private Player lastPlayer;
        private bool configDirty;
        private bool materialsFailed;
        private MoteEffect demonstration;
        private Vector3 demonstrationPosition;
        private float demonstrationUntil;
        private static readonly Type[] Types = { typeof(Pickable), typeof(PickableItem), typeof(MineRock), typeof(MineRock5), typeof(Beehive), typeof(Container), typeof(DropOnDestroyed), typeof(Destructible) };

        private void Awake()
        {
            Instance = this; Log = Logger;
            enabledMod = Config.Bind("General", "Enabled", true, "Enable local collectible effects.");
            IncludeBuilt = Config.Bind("General", "IncludePlayerBuilt", false, "Also mark player-built chests, hives and planted crops.");
            exclusions = Config.Bind("General", "ExcludedPrefabs", "Wood,Stone,RoundLog,FineWood,ElderBark,Pickable_Branch,Pickable_Branch_Snow,Pickable_Stone,Pickable_StoneRock,Pickable_HardRockOffspring", "Comma-separated exact prefab names; evaluated before styles. Add common items here.");
            range = Config.Bind("Performance", "ViewDistance", 45f, new ConfigDescription("Maximum distance in meters; effects fade over the last 10 meters.", new AcceptableValueRange<float>(10, 100)));
            maxEffects = Config.Bind("Performance", "MaxEffects", 32, new ConfigDescription("Nearest collectible groups visible at once.", new AcceptableValueRange<int>(1, 96)));
            maxSpillLights = Config.Bind("Performance", "MaxSpillLights", 24, new ConfigDescription("Total real spill lights across nearest visible models; 0 disables surrounding illumination. Large models request up to four broad overlapping lights, small models one.", new AcceptableValueRange<int>(0, 64)));
            spillIntensity = Config.Bind("Lighting", "SpillIntensity", .6f, new ConfigDescription("Total surrounding light per collectible group, shared across its sources; 0 disables light spill without changing model glow or motes.", new AcceptableValueRange<float>(0, 2)));
            spillRange = Config.Bind("Lighting", "SpillRange", 3f, new ConfigDescription("Extra light reach beyond the model coverage in meters. This expands falloff, not particle spread.", new AcceptableValueRange<float>(1, 8)));
            density = Config.Bind("Appearance", "Density", 0.6f, new ConfigDescription("Mote density multiplier (64 motes per effect at 1).", new AcceptableValueRange<float>(0.25f, 2f)));
            height = Config.Bind("Appearance", "Height", 2f, new ConfigDescription("Beam height multiplier; 2 doubles the original columns.", new AcceptableValueRange<float>(0.3f, 4f)));
            radius = Config.Bind("Appearance", "Radius", 1f, new ConfigDescription("Local mote spread multiplier.", new AcceptableValueRange<float>(0.3f, 2f)));
            brightness = Config.Bind("Appearance", "Brightness", 1f, new ConfigDescription("Mote opacity multiplier; model glow and light spill have independent controls.", new AcceptableValueRange<float>(0.2f, 2f)));
            speed = Config.Bind("Appearance", "Speed", 0.9f, new ConfigDescription("Rise speed multiplier; 1 gives a slow 6.5 second journey.", new AcceptableValueRange<float>(0.25f, 2f)));
            dust = Config.Bind("Appearance", "Dust", 1f, new ConfigDescription("Fine dust amount; 0 disables it.", new AcceptableValueRange<float>(0, 2)));
            trailLength = Config.Bind("Appearance", "TrailLength", 2f, new ConfigDescription("Short trail length; 1 caps each tail at 22 cm, 0 disables it.", new AcceptableValueRange<float>(0, 2)));
            twist = Config.Bind("Appearance", "Twist", 1.45f, new ConfigDescription("Slow orbit and individual spin; 0 produces straight rising motes.", new AcceptableValueRange<float>(0, 2)));
            areaGlow = Config.Bind("Appearance", "AreaGlow", 1.2f, new ConfigDescription("Dim emission from the original model material; 0 restores original materials. Independent of Lighting.SpillIntensity.", new AcceptableValueRange<float>(0, 2)));
            mergeDistance = Config.Bind("General", "MergeDistance", 2.5f, new ConfigDescription("Maximum separation in meters between any two objects sharing one effect. 0 disables merging. Mixed groups use the most common style.", new AcceptableValueRange<float>(0, 8)));
            variantMode = Config.Bind("Appearance", "VariantMode", "Auto", new ConfigDescription("Auto blends daylight/night appearances with the game clock. Day or Night locks the look for comparison.", new AcceptableValueList<string>("Auto", "Day", "Night")));
            Styles.Load();
            // Color/shape overrides are generated for every authored proposal.
            foreach (Style style in Styles.ById.Values)
            {
                string section = "Style." + style.Id;
                Config.Bind(section, "Enabled", true, "Show the " + style.Label + " effect. False removes its particles, trails, model glow and surrounding illumination. Fallback also controls unlisted/modded collectibles.");
                Config.Bind(section, "SurfaceGlow", LightingProfile.GlowScale(style.Id), new ConfigDescription("Model self-illumination scale, independent of motes and light spill. Mushroom defaults are brighter; bushes and ores are subdued.", new AcceptableValueRange<float>(0, 2)));
                Config.Bind(section, "LightSpill", 1f, new ConfigDescription("Multiplier for surrounding illumination from this collectible style; 0 disables spill for this style.", new AcceptableValueRange<float>(0, 2)));
                Config.Bind(section, "Color", ColorUtility.ToHtmlStringRGB(style.Color), "Mote RGB hex.");
                Config.Bind(section, "Accent", ColorUtility.ToHtmlStringRGB(style.Accent), "Glint and rim RGB hex.");
                Config.Bind(section, "Shape", style.Shape, new ConfigDescription("Mote silhouette.", new AcceptableValueList<string>("round", "star", "seed", "leaf", "shard", "hex")));
            }
            ApplyConfig();
            Config.SettingChanged += (_, __) => configDirty = true;
            harmony = new Harmony(Guid);
            var postfix = new HarmonyMethod(typeof(Plugin), nameof(Register));
            foreach (Type type in Types)
            {
                var method = AccessTools.DeclaredMethod(type, type == typeof(MineRock) ? "Start" : "Awake");
                if (method != null) harmony.Patch(method, postfix: postfix);
                else Logger.LogWarning("No registration hook for " + type.Name);
            }
            new Terminal.ConsoleCommand("wildglow_preview", "wildglow_preview <style> — show a local effect for 30 seconds; use stop to clear.", args =>
            {
                if (args.Length < 2) { args.Context.AddString("Styles: " + string.Join(", ", Styles.ById.Keys)); return; }
                if (args[1] == "stop") { ClearDemonstration(); return; }
                if (!Player.m_localPlayer) { args.Context.AddString("Load a world first."); return; }
                if (!Styles.ById.TryGetValue(args[1].ToLowerInvariant(), out var selected)) { args.Context.AddString("Unknown style. Run wildglow_preview for the list."); return; }
                if (!enabledMod.Value || !selected.Enabled) { args.Context.AddString("This effect is disabled in the WildGlow config."); return; }
                ClearDemonstration();
                try
                {
                    demonstration = new MoteEffect(selected, 11);
                    demonstrationPosition = Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 2f;
                    demonstrationUntil = Time.time + 30;
                    nextRefresh = 0;
                    args.Context.AddString("Previewing " + selected.Label + " for 30 seconds, two meters ahead. No item spawned.");
                }
                catch (Exception ex) { Logger.LogError(ex); args.Context.AddString("Particle shader unavailable; see the BepInEx log."); }
            });
            new Terminal.ConsoleCommand("wildglow_reload", "Reload WildGlow's local configuration.", args =>
            {
                Config.Reload(); configDirty = true; args.Context.AddString("WildGlow configuration reloaded.");
            });
            new Terminal.ConsoleCommand("wildglow_status", "Show the loaded version and nearby model emission diagnostics.", args =>
            {
                string status = "WildGlow 0.4.5; enabled=" + enabledMod.Value + "; renderer failed=" + materialsFailed + "; active effects=" + active.Count;
                args.Context.AddString(status); Logger.LogInfo(status);
                foreach (var g in groups.Values.OrderBy(g => g.Distance).Take(5))
                {
                    string info = g.Style.Label + ": " + g.Anchors.Count + " surface anchors, " +
                        (active.TryGetValue(g.Id, out var fx) ? fx.EmissiveMaterials : 0) + " emissive materials, " + (active.TryGetValue(g.Id, out var lit) ? lit.SpillLights : 0) + " spill lights; " +
                        string.Join(", ", g.Members.Select(t => t.Root ? t.Root.name : "removed"));
                    args.Context.AddString(info); Logger.LogInfo(info);
                }
            });
            Logger.LogInfo("WildGlow 0.4.5 loaded. " + Styles.ById.Count + " styles. Client-only visuals; no save or network data changes.");
        }

        private static void Register(Component __instance)
        {
            if (Instance && __instance && __instance.gameObject.scene.IsValid())
                Instance.pending[__instance.gameObject.GetInstanceID()] = __instance.gameObject;
        }

        private void ApplyConfig()
        {
            Excluded = new HashSet<string>(exclusions.Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0), StringComparer.OrdinalIgnoreCase);
            // Retired common collectibles stay excluded even when an older config is retained.
            Excluded.UnionWith(new[] { "Flint", "Pickable_Flint", "SurtlingCore", "Pickable_SurtlingCoreStand", "FirTree", "FirTree_log", "FirTree_log_half", "FirTree_Stub" });
            foreach (Style style in Styles.ById.Values)
            {
                string section = "Style." + style.Id;
                if (Config.TryGetEntry<bool>(section, "Enabled", out var enabled)) style.Enabled = enabled.Value;
                if (Config.TryGetEntry<float>(section, "SurfaceGlow", out var glow)) style.SurfaceGlow = glow.Value;
                if (Config.TryGetEntry<float>(section, "LightSpill", out var spill)) style.LightSpill = spill.Value;
                if (Config.TryGetEntry<string>(section, "Color", out var color) && ColorUtility.TryParseHtmlString("#" + color.Value.TrimStart('#'), out Color c)) style.Color = c;
                if (Config.TryGetEntry<string>(section, "Accent", out var accent) && ColorUtility.TryParseHtmlString("#" + accent.Value.TrimStart('#'), out Color a)) style.Accent = a;
                if (Config.TryGetEntry<string>(section, "Shape", out var shape)) style.Shape = shape.Value;
            }
            Styles.ClearAutomatic();
        }

        private void Update()
        {
            Player player = Player.m_localPlayer;
            if (configDirty)
            {
                configDirty = false; ApplyConfig(); ClearEffects(); ClearDemonstration();
                foreach (var pair in targets) if (pair.Value.Root) pending[pair.Key] = pair.Value.Root;
                targets.Clear();
                lastPlayer = null; materialsFailed = false;
            }
            if (!player || !enabledMod.Value || materialsFailed)
            {
                if (active.Count > 0) ClearEffects();
                ClearDemonstration();
                if (!player && lastPlayer) { targets.Clear(); pending.Clear(); lastPlayer = null; }
                return;
            }
            var appearance = new Appearance { Density = density.Value, Radius = radius.Value, Height = height.Value,
                Brightness = brightness.Value, Speed = speed.Value, Dust = dust.Value, TrailLength = trailLength.Value,
                Twist = twist.Value, AreaGlow = areaGlow.Value, SpillIntensity = spillIntensity.Value, SpillRange = spillRange.Value, Night = variantMode.Value == "Night" ? 1 : variantMode.Value == "Day" ? 0 : EnvMan.instance ? EffectBehavior.Night(EnvMan.instance.GetDayFraction()) : 1 };
            Camera camera = Camera.main;
            if (demonstration != null)
            {
                if (Time.time >= demonstrationUntil) ClearDemonstration();
                else demonstration.Tick(demonstrationPosition, Time.time, appearance, 1, camera);
            }
            if (player != lastPlayer)
            {
                lastPlayer = player;
                // One catch-up pass handles objects that awoke before this plugin or local player.
                foreach (Type type in Types)
                    foreach (var obj in UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None)) Register((Component)obj);
                nextRefresh = 0;
            }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 0.6f;
                Refresh(player.transform.position);
            }
            foreach (var pair in active)
            {
                var group = groups[pair.Key];
                if (!group.TryCenter(out var center)) { pair.Value.SetVisible(false); continue; }
                try { pair.Value.BindSources(group); }
                catch (Exception ex)
                {
                    Logger.LogError("Surface renderer unavailable; disabling effects safely: " + ex);
                    materialsFailed = true; break;
                }
                pair.Value.SetVisible(true);
                float distance = Vector3.Distance(player.transform.position, center);
                float fade = Mathf.Clamp01((range.Value - distance) / Mathf.Min(10f, range.Value * 0.3f));
                pair.Value.Tick(center, Time.time, appearance, fade, camera);
            }
        }

        private void Refresh(Vector3 player)
        {
            foreach (var p in pending)
            {
                if (!p.Value || targets.ContainsKey(p.Key)) continue;
                try { var target = Target.Create(p.Value); if (target != null) targets[p.Key] = target; }
                catch (Exception ex) { Logger.LogDebug("Skipped " + p.Value.name + ": " + ex.Message); }
            }
            pending.Clear(); near.Clear(); dead.Clear();
            foreach (var pair in targets)
            {
                var t = pair.Value;
                if (!t.Root) { dead.Add(pair.Key); continue; }
                // Filter before grouping so disabled pickups cannot seed or shift a shared effect.
                if (!t.Style.Enabled) continue;
                t.Distance = (t.Center - player).sqrMagnitude;
                if (t.Distance > range.Value * range.Value) continue;
                try { if (t.Available()) near.Add(t); }
                catch (Exception ex) { Logger.LogDebug("Availability: " + ex.Message); }
            }
            // Group before applying budgets so nearby pickups share both a column and a light.
            var mergeable = near.Where(t => !t.IsDeposit).ToList();
            var points = mergeable.Select(t => { var c = t.Center; return new Clusterer.Point {
                Id = t.Root.GetInstanceID(), X = c.x, Y = c.y, Z = c.z }; }).ToList();
            groups.Clear();
            foreach (var members in Clusterer.Build(points, mergeDistance.Value))
            {
                var group = new TargetGroup(members.Select(i => mergeable[i]).ToList());
                if (!group.TryCenter(out var center)) continue;
                group.Distance = (center - player).sqrMagnitude;
                groups.Add(group.Id, group);
            }
            // A deposit owns its entire surface. Nearby bushes must not shift its column into the air.
            foreach (var deposit in near.Where(t => t.IsDeposit))
            {
                var group = new TargetGroup(new List<Target> { deposit }) { Distance = deposit.Distance };
                groups.Add(group.Id, group);
            }
            var selected = groups.Values.OrderBy(g => g.Distance).ThenBy(g => g.Id).Take(maxEffects.Value).ToList();
            foreach (var group in selected) group.PrepareSurface();
            var assigned = CoverageLayout.Allocate(selected.Select(g => g.HasLightBounds && g.Style.LightSpill > 0
                ? SpillLayout.Requested(g.LightBounds.extents.x, g.LightBounds.extents.z) : 0).ToArray(),
                spillIntensity.Value > 0 ? maxSpillLights.Value : 0);
            for (int i = 0; i < selected.Count; i++) selected[i].SpillBudget = assigned[i];
            var chosen = new HashSet<int>(selected.Select(g => g.Id));
            foreach (int id in active.Keys.Where(id => !chosen.Contains(id) || activeStyles[id] != groups[id].Style.Id).ToArray())
            { active[id].Dispose(); active.Remove(id); activeStyles.Remove(id); }
            foreach (int id in dead) targets.Remove(id);
            foreach (int id in chosen)
            {
                if (active.ContainsKey(id)) continue;
                try { active[id] = new MoteEffect(groups[id].Style, id); activeStyles[id] = groups[id].Style.Id; }
                catch (Exception ex)
                {
                    Logger.LogError("Particle renderer unavailable; disabling effects safely: " + ex);
                    materialsFailed = true; ClearEffects(); break;
                }
            }
        }

        private void ClearEffects() { foreach (var fx in active.Values) fx.Dispose(); active.Clear(); activeStyles.Clear(); groups.Clear(); }
        private void ClearDemonstration() { demonstration?.Dispose(); demonstration = null; nextRefresh = 0; }
        private void OnDestroy()
        {
            harmony?.UnpatchSelf(); ClearEffects(); ClearDemonstration(); MoteEffect.ReleaseMaterials();
            if (Instance == this) Instance = null;
        }
    }
}
