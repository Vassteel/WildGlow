using System;

namespace WildGlow
{
    // Broad overlapping sources above the model, never small lamps embedded in its surface.
    // Pure geometry shared with the coverage tests; coordinates are relative to bounds center.
    internal static class SpillLayout
    {
        internal static int Requested(float x, float z) => Math.Max(x, z) > 2 ? 4 : 1;
        internal static MoteMotion.Point Position(float x, float y, float z, int index, int count)
        {
            float lift = Math.Max(.65f, Math.Max(x, z) * .65f);
            if (count <= 1) return new MoteMotion.Point(0, y + lift, 0);
            // Opposite corners first; a partial budget still covers both halves.
            float px = (index == 0 || index == 3 ? -1 : 1) * x * .5f;
            float pz = (index % 2 == 0 ? -1 : 1) * z * .5f;
            return new MoteMotion.Point(px, y + lift, pz);
        }
        internal static float Range(float x, float y, float z, float padding)
        {
            float lift = Math.Max(.65f, Math.Max(x, z) * .65f);
            // Enclose the opposite edge and space beyond it, including the bottom of the bounds.
            float span = (float)Math.Sqrt(x*x*2.25f + z*z*2.25f + (2*y+lift)*(2*y+lift));
            return Math.Max(2, span + padding);
        }
        internal static float Intensity(float amount, float night, int count) =>
            count <= 0 ? 0 : Math.Max(0, Math.Min(2, amount)) * (.18f + .82f * night) / count;
    }
}
