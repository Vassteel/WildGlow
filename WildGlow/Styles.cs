using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace WildGlow
{
    internal sealed class Style
    {
        public string Id, Label, Shape;
        public bool Enabled = true;
        public Color Color, Accent;
        public float Radius, Height;
        public float SurfaceGlow = 1, LightSpill = 1;
        public Style Copy() => (Style)MemberwiseClone();
    }

    internal static class Styles
    {
        internal static readonly Dictionary<string, Style> ById = new Dictionary<string, Style>();
        private static readonly Dictionary<string, Style> Aliases = new Dictionary<string, Style>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Style> Automatic = new Dictionary<string, Style>(StringComparer.OrdinalIgnoreCase);
        internal static string Clean(string name) => (name ?? "").Replace("(Clone)", "").Trim();

        internal static void Load()
        {
            using var reader = new StreamReader(typeof(Styles).Assembly.GetManifestResourceStream("WildGlow.effects.tsv"));
            reader.ReadLine();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var p = line.Split('\t');
                var style = new Style { Id = p[0], Label = p[1], Color = Hex(p[2]), Accent = Hex(p[3]), Shape = p[4],
                    Radius = float.Parse(p[5], CultureInfo.InvariantCulture), Height = float.Parse(p[6], CultureInfo.InvariantCulture) };
                ById.Add(style.Id, style);
                foreach (string alias in p[7].Split(',')) if (alias.Length > 0) Aliases[alias] = style;
            }
        }

        internal static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value.TrimStart('#'), out Color color);
            return color;
        }

        internal static bool TryKnown(string name, out Style style) => Aliases.TryGetValue(Clean(name), out style);
        internal static void ClearAutomatic() => Automatic.Clear();

        internal static Style Resolve(GameObject item, string source)
        {
            // Exact source aliases preserve intentional looks for stands, wild seed plants and deposits.
            if (TryKnown(source, out Style style)) return style;
            string name = item ? Clean(item.name) : Clean(source);
            if (TryKnown(name, out style)) return style;
            if (Automatic.TryGetValue(name, out style)) return style;
            style = ById["fallback"].Copy();
            style.Id = "auto-" + name;
            style.Label = name;
            // Sample the actual item icon once, never the scene every frame. Supports new and modded items.
            var drop = item ? item.GetComponent<ItemDrop>() : null;
            var icons = drop?.m_itemData?.m_shared?.m_icons;
            if (icons != null && icons.Length > 0 && icons[0])
            {
                try { style.Color = Sample(icons[0]); style.Accent = Color.Lerp(style.Color, Color.white, 0.65f); }
                catch (Exception ex) { Plugin.Log.LogDebug("Icon color fallback for " + name + ": " + ex.Message); }
            }
            Automatic[name] = style;
            return style;
        }

        private static Color Sample(Sprite sprite)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(24, 24, 0, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(24, 24, TextureFormat.RGBA32, false);
            try
            {
                Rect rect = sprite.textureRect;
                var source = sprite.texture;
                Graphics.Blit(source, rt, new Vector2(rect.width / source.width, rect.height / source.height),
                    new Vector2(rect.x / source.width, rect.y / source.height));
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 24, 24), 0, 0);
                texture.Apply();
                Color sum = Color.black; float total = 0;
                foreach (Color pixel in texture.GetPixels())
                {
                    if (pixel.a < 0.3f) continue;
                    Color.RGBToHSV(pixel, out _, out float saturation, out float value);
                    float weight = pixel.a * (0.15f + saturation) * Mathf.Max(0.1f, value);
                    sum += pixel * weight; total += weight;
                }
                if (total < 0.01f) return ById["fallback"].Color;
                Color result = sum / total; result.a = 1;
                return result;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.Destroy(texture); }
        }
    }
}
