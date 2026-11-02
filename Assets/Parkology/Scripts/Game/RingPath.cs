using UnityEngine;

namespace Parkology
{
    /// <summary>
    /// Clockwise rounded-rectangle road around the parking lot, parameterised by arc length.
    /// s = 0 is the left end of the top straight; travel goes right along the top, down the right side,
    /// left along the bottom and up the left side.
    /// </summary>
    public sealed class RingPath
    {
        public readonly float HalfW, HalfH, Radius, Length;
        readonly float top, arc, side;
        public readonly Vector3 Center;

        public RingPath(Vector3 center, float halfW, float halfH, float radius)
        {
            Center = center;
            HalfW = halfW;
            HalfH = halfH;
            Radius = radius;
            top = 2f * (halfW - radius);
            side = 2f * (halfH - radius);
            arc = Mathf.PI * radius * 0.5f;
            Length = 2f * top + 2f * side + 4f * arc;
        }

        public float Wrap(float s) => Mathf.Repeat(s, Length);

        /// <summary>Parameter of the point on the top straight at world x.</summary>
        public float TopAt(float x) => Mathf.Clamp(x - Center.x + HalfW - Radius, 0f, top);

        /// <summary>Parameter of the point on the bottom straight at world x.</summary>
        public float BottomAt(float x) => top + arc + side + arc + Mathf.Clamp(HalfW - Radius - (x - Center.x), 0f, top);

        /// <summary>End of the right-hand straight; departing trains leave the loop here, heading down.</summary>
        public float ExitParam => top + arc + side;

        public Vector3 Evaluate(float s, out Vector3 tangent)
        {
            s = Wrap(s);
            float hw = HalfW, hh = HalfH, r = Radius;
            Vector3 p;
            if (s < top)
            {
                p = new Vector3(-hw + r + s, 0, hh);
                tangent = Vector3.right;
            }
            else if ((s -= top) < arc)
            {
                p = Corner(new Vector2(hw - r, hh - r), 90f, -90f * s / arc, out tangent);
            }
            else if ((s -= arc) < side)
            {
                p = new Vector3(hw, 0, hh - r - s);
                tangent = Vector3.back;
            }
            else if ((s -= side) < arc)
            {
                p = Corner(new Vector2(hw - r, -hh + r), 0f, -90f * s / arc, out tangent);
            }
            else if ((s -= arc) < top)
            {
                p = new Vector3(hw - r - s, 0, -hh);
                tangent = Vector3.left;
            }
            else if ((s -= top) < arc)
            {
                p = Corner(new Vector2(-hw + r, -hh + r), -90f, -90f * s / arc, out tangent);
            }
            else if ((s -= arc) < side)
            {
                p = new Vector3(-hw, 0, -hh + r + s);
                tangent = Vector3.forward;
            }
            else
            {
                s -= side;
                p = Corner(new Vector2(-hw + r, hh - r), 180f, -90f * Mathf.Min(s, arc) / arc, out tangent);
            }
            return Center + p;
        }

        Vector3 Corner(Vector2 c, float startDeg, float deltaDeg, out Vector3 tangent)
        {
            float a = (startDeg + deltaDeg) * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            tangent = new Vector3(Mathf.Sin(a), 0, -Mathf.Cos(a)); // clockwise
            return new Vector3(c.x, 0, c.y) + dir * Radius;
        }

        /// <summary>Forward distance from a to b along the loop (0..Length).</summary>
        public float Ahead(float from, float to) => Mathf.Repeat(to - from, Length);
    }
}
