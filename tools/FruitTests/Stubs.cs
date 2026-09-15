using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace UnityEngine {
public class Object {public string name;public static implicit operator bool(Object o)=>o!=null;}
public struct Vector3 {
 public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
 public static Vector3 up=>new(0,1,0);public float sqrMagnitude=>x*x+y*y+z*z;
 public Vector3 normalized=>this*(1/(float)Math.Sqrt(sqrMagnitude));
 public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
 public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
 public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
}
public struct Bounds {
 public Vector3 center,extents;public void Encapsulate(Bounds b){var lo=new Vector3(Math.Min(center.x-extents.x,b.center.x-b.extents.x),Math.Min(center.y-extents.y,b.center.y-b.extents.y),Math.Min(center.z-extents.z,b.center.z-b.extents.z));var hi=new Vector3(Math.Max(center.x+extents.x,b.center.x+b.extents.x),Math.Max(center.y+extents.y,b.center.y+b.extents.y),Math.Max(center.z+extents.z,b.center.z+b.extents.z));center=(hi+lo)*.5f;extents=(hi-lo)*.5f;}
}
public class Transform:Object {
 public GameObject gameObject;public Matrix4x4 matrix=Matrix4x4.Identity;
 private Vector3 Convert(Vector3 p,Matrix4x4 m,bool normal){var v=new System.Numerics.Vector3(p.x,p.y,p.z);v=normal?System.Numerics.Vector3.TransformNormal(v,m):System.Numerics.Vector3.Transform(v,m);return new(v.X,v.Y,v.Z);}
 public Vector3 TransformPoint(Vector3 p)=>Convert(p,matrix,false);
 public Vector3 InverseTransformPoint(Vector3 p){Matrix4x4.Invert(matrix,out var inverse);return Convert(p,inverse,false);}
 public Vector3 TransformDirection(Vector3 p)=>Convert(p,matrix,true).normalized;
 public Vector3 InverseTransformDirection(Vector3 p){Matrix4x4.Invert(matrix,out var inverse);return Convert(p,inverse,true).normalized;}
}
public class Renderer:Object {public bool enabled=true,forceRenderingOff;public Bounds bounds;}
public class MeshRenderer:Renderer {}
public class Mesh:Object {public int vertexCount;public Bounds bounds;}
public class MeshFilter:Object {public Mesh sharedMesh;public Transform transform;public MeshRenderer renderer;public T GetComponent<T>() where T:class=>renderer as T;}
public class Collider:Object {public bool enabled=true,isTrigger;}
public class GameObject:Object {public bool activeInHierarchy=true;public List<MeshFilter> filters=new();public T[] GetComponentsInChildren<T>(bool includeInactive)=>filters.Where(f=>includeInactive||f.transform.gameObject.activeInHierarchy).OfType<T>().ToArray();}
}
