using System;

namespace WildGlow
{
    // Engine-independent motion also exercised by tools/MotionTests. Distances are meters.
    internal static class MoteMotion
    {
        internal const float Lifetime = 6.5f;
        internal struct Point
        {
            internal float X, Y, Z;
            internal Point(float x, float y, float z) { X = x; Y = y; Z = z; }
        }
        internal static float Hash(int n)
        {
            unchecked
            {
                uint x = (uint)n;
                x ^= x >> 16; x *= 0x7feb352d;
                x ^= x >> 15; x *= 0x846ca68b;
                x ^= x >> 16;
                return (x & 0x00ffffff) / 16777216f;
            }
        }
        internal static float Phase(float time, float speed, int key)
        {
            float p = time * speed / Lifetime + Hash(key);
            return p - (float)Math.Floor(p);
        }
        internal static Point Position(float phase, float time, int key, float radius, float height, float twist)
        {
            float g = Math.Min(1, Math.Max(0, phase / 0.65f));
            g = g * g * (3 - 2 * g);
            float r = radius * (0.12f + 0.88f * (1 - g)) * (0.3f + 0.7f * (float)Math.Sqrt(Hash(key + 191)));
            float a = Hash(key + 43) * (float)Math.PI * 2 + (phase * 4.2f + time * 0.16f) * twist;
            return new Point((float)Math.Cos(a) * r, 0.05f + phase * height, (float)Math.Sin(a) * r);
        }
        internal static Point TrailPoint(float phase, float time, int key, float radius, float height, float speed, float twist, float length, float fraction)
        {
            Point head = Position(phase, time, key, radius, height, twist);
            float delay = Math.Min(0.28f * length, phase * Lifetime / Math.Max(0.01f, speed)) * fraction;
            Point tail = Position(Math.Max(0, phase - delay * speed / Lifetime), time - delay, key, radius, height, twist);
            float dx = tail.X - head.X, dy = tail.Y - head.Y, dz = tail.Z - head.Z;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            float limit = 0.22f * length;
            if (distance > limit && distance > 0)
            {
                float factor = limit / distance;
                return new Point(head.X + dx * factor, head.Y + dy * factor, head.Z + dz * factor);
            }
            return tail;
        }
        internal static float Spin(float time, int key, float twist) => key * 71 + time * (10 + 10 * Hash(key + 77)) * twist;
    }
}
