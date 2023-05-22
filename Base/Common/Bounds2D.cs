using Unity.Mathematics;



namespace Fundering.Common
{
    public readonly struct Bounds2D
    {
        private readonly float2 position;
        private readonly float2 extents;

        public float2 Min => position - extents;
        public float2 Max => position + extents;
        public float2 Size => extents * 2f;

        public Bounds2D(in float2 position, in float2 size)
        {
            this.position = position;
            extents = size / 2f;
        }

        public Bounds2D(in float2x2 rect)
        {
            position = math.lerp(rect.c0, rect.c1, .5f);
            extents = math.abs(rect.c1 - rect.c0) / 2f;
        }

        private static bool Equals(Bounds2D lhs, Bounds2D rhs)
        {
            return math.all(lhs.position == rhs.position)
                   && math.all(lhs.extents == rhs.extents);
        }

        public static bool operator ==(Bounds2D lhs, Bounds2D rhs)
        {
            return Equals(lhs, rhs);
        }
        
        public static bool operator !=(Bounds2D lhs, Bounds2D rhs)
        {
            return !Equals(lhs, rhs);
        }

        public bool Intersects(in Bounds2D bounds)
        {
            float2 max = Max;
            float2 min = Min;
            float2 anotherMax = bounds.Max;
            float2 anotherMin = bounds.Min;

            return min.x <= anotherMax.x && max.x >= anotherMin.x &&
                   min.y <= anotherMax.y && max.y >= anotherMin.y;
        }
    }
}