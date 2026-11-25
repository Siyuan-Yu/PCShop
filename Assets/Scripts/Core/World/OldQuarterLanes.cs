using System;

namespace PCShopSell.Core.World
{
    // Surveyed relative to the starter door. Movement and paving share the same centre lines.
    public static class OldQuarterLanes
    {
        public const float Heading = -21f;
        public static readonly (float x1, float z1, float x2, float z2, float width, float top)[] Paths =
        {
            (-136,34,-70,34,1.50f,.050f), (-70,34,-64,36.4f,1.50f,.052f), (-64,36.4f,31,36.4f,1.50f,.050f),
            (-110,10.1f,-110,58.2f,1.38f,.056f), (-70,10.1f,-70,58.2f,1.38f,.056f),
            (-26,10.4f,-26,36.4f,1.5f,.056f), (-33.2f,36.4f,-33.2f,58.4f,1.38f,.056f),
            (8.75f,10.9f,8.75f,36.4f,1.38f,.056f), (2,36.4f,2,58.8f,1.38f,.056f),
            (-12,-4,-12,.5f,1.55f,.050f), (-61,-4.7f,-61,.1f,1.9f,.050f),
            (-89.5f,10.4f,-89.5f,17.8f,1.2f,.052f),
            (5.35f,1.9f,5.6f,-16.7f,1.28f,.082f), (5.6f,-16.7f,-44,-17.3f,1.4f,.083f),
            (-43,-17.3f,-59,-12,1.4f,.082f), (-60,-12,-60,-9.3f,1.38f,.081f)
        };

        public static MapPoint ToWorld(MapPoint origin, float x, float z)
        {
            double yaw = Heading * Math.PI / 180;
            return new MapPoint(origin.X + Math.Cos(yaw)*x + Math.Sin(yaw)*z,
                origin.Y - Math.Sin(yaw)*x + Math.Cos(yaw)*z);
        }
    }
}
