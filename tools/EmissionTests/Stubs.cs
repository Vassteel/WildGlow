// Minimal material/renderer test doubles. No shader/GPU behavior is simulated.
using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine {
public class Object { public bool destroyed; public string name; public HideFlags hideFlags; public static implicit operator bool(Object o)=>o!=null&&!o.destroyed; public static void Destroy(Object o){o.destroyed=true;} }
public enum HideFlags { DontSave }
public struct Color { public float r,g,b,a; public Color(float x,float y,float z,float w=1){r=x;g=y;b=z;a=w;} public static Color Lerp(Color a,Color b,float t)=>a*(1-t)+b*t; public static Color white=>new Color(1,1,1); public static Color operator +(Color x,Color y)=>new Color(x.r+y.r,x.g+y.g,x.b+y.b,x.a+y.a); public static Color operator *(Color x,float f)=>new Color(x.r*f,x.g*f,x.b*f,x.a*f); }
public struct Vector2 { public float x,y; public Vector2(float a,float b){x=a;y=b;} }
public class Texture:Object {} public class Texture2D:Texture { public static Texture2D whiteTexture=new Texture2D(); }
public static class Mathf { public static float Clamp(float a,float b,float c)=>Math.Clamp(a,b,c); public static float Lerp(float a,float b,float t)=>a+(b-a)*t; }
public class Material:Object {
 public Dictionary<string,object> properties=new(); public Dictionary<string,Vector2> scales=new(),offsets=new(); public HashSet<string> keywords=new();
 public Material(){} public Material(Material m){properties=new(m.properties);scales=new(m.scales);offsets=new(m.offsets);keywords=new(m.keywords);}
 public bool HasProperty(string s)=>properties.ContainsKey(s); public Color GetColor(string s)=>(Color)properties[s]; public void SetColor(string s,Color c)=>properties[s]=c;
 public Texture GetTexture(string s)=>properties.TryGetValue(s,out var v)?v as Texture:null; public void SetTexture(string s,Texture t)=>properties[s]=t;
 public Vector2 GetTextureScale(string s)=>scales.TryGetValue(s,out var v)?v:new Vector2(1,1); public Vector2 GetTextureOffset(string s)=>offsets.TryGetValue(s,out var v)?v:default;
 public void SetTextureScale(string s,Vector2 v)=>scales[s]=v; public void SetTextureOffset(string s,Vector2 v)=>offsets[s]=v; public void EnableKeyword(string s)=>keywords.Add(s);
}
public class Renderer:Object { private Material[] materials; public Material[] sharedMaterials { get=>(Material[])materials.Clone(); set=>materials=(Material[])value.Clone(); } }
public class MeshRenderer:Renderer {} public class SkinnedMeshRenderer:Renderer {} public class ParticleSystemRenderer:Renderer {}
public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z); }
public struct Bounds {public Vector3 center,extents;}
public class Transform:Object { public Vector3 position; public void SetParent(Transform p,bool keepWorld){} }
public enum LightType {Point} public enum LightShadows {None} public enum LightRenderMode {ForcePixel}
public class Light:Object {public static List<Light> All=new(); public GameObject gameObject; public Transform transform=>gameObject.transform; public bool enabled; public LightType type;public LightShadows shadows;public LightRenderMode renderMode; public float bounceIntensity,range,intensity; public Color color;}
public class GameObject:Object { public Transform transform=new(); public GameObject(string n=""){name=n;} public T AddComponent<T>() where T:Light,new(){var l=new T{gameObject=this};Light.All.Add(l);return l;} public List<Renderer> renderers=new(); public T[] GetComponentsInChildren<T>(bool includeInactive)=>renderers.OfType<T>().ToArray(); }
}
namespace WildGlow { internal class Style {internal float SurfaceGlow=.085f;} internal class Target {internal UnityEngine.GameObject Root; internal Style Style=new();} internal class TargetGroup {internal List<Target> Members=new();internal bool HasLightBounds;internal int SpillBudget;internal UnityEngine.Bounds LightBounds;} }
