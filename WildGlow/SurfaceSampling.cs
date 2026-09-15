using System;

namespace WildGlow
{
    // Engine-independent ray layout, shared by the runtime collider sampler and coverage tests.
    internal static class SurfaceSampling
    {
        internal const int Grid = 6, FaceCount = 6, RaysPerFace = Grid * Grid;
        internal struct Cast
        {
            internal MoteMotion.Point Origin, Direction;
            internal float Reach;
        }
        internal static Cast Ray(MoteMotion.Point min, MoteMotion.Point max, int face, int index)
        {
            if (face < 0 || face >= FaceCount || index < 0 || index >= RaysPerFace) throw new ArgumentOutOfRangeException();
            float u = (index % Grid + .5f) / Grid, v = (index / Grid + .5f) / Grid;
            if (face < 2) return new Cast {
                Origin = new MoteMotion.Point(face == 0 ? max.X + 1 : min.X - 1, min.Y + (max.Y - min.Y) * u, min.Z + (max.Z - min.Z) * v),
                Direction = new MoteMotion.Point(face == 0 ? -1 : 1, 0, 0), Reach = max.X - min.X + 2 };
            if (face < 4) return new Cast {
                Origin = new MoteMotion.Point(min.X + (max.X - min.X) * u, face == 2 ? max.Y + 1 : min.Y - 1, min.Z + (max.Z - min.Z) * v),
                Direction = new MoteMotion.Point(0, face == 2 ? -1 : 1, 0), Reach = max.Y - min.Y + 2 };
            return new Cast {
                Origin = new MoteMotion.Point(min.X + (max.X - min.X) * u, min.Y + (max.Y - min.Y) * v, face == 4 ? max.Z + 1 : min.Z - 1),
                Direction = new MoteMotion.Point(0, 0, face == 4 ? -1 : 1), Reach = max.Z - min.Z + 2 };
        }
    }
}
