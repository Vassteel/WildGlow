using UnityEngine;

namespace WildGlow
{
    internal struct SurfaceAnchor
    {
        internal Collider Collider;
        internal Renderer Renderer;
        internal bool RequiresRenderer;
        internal Transform Transform;
        internal Vector3 Local, Normal;
        internal Vector3 World => Transform ? Transform.TransformPoint(Local) : Local;
        internal Vector3 WorldNormal => Transform ? Transform.TransformDirection(Normal).normalized : Vector3.up;
        internal bool Valid => Transform && Transform.gameObject.activeInHierarchy && (!Collider || (Collider.enabled && !Collider.isTrigger))
            && (!RequiresRenderer || (Renderer && Renderer.enabled && !Renderer.forceRenderingOff));
        internal static SurfaceAnchor At(Transform transform, Vector3 world, Collider collider = null, Vector3 normal = default) =>
            new SurfaceAnchor { Transform = transform, Collider = collider, Local = transform.InverseTransformPoint(world),
                Normal = transform.InverseTransformDirection(normal.sqrMagnitude > 0 ? normal : Vector3.up) };
    }
}
