using System.Collections.Generic;
using UnityEngine;

namespace Puzzle3D
{
    /// <summary>
    /// Ear-clipping triangulation for a simple (non-self-intersecting) polygon.
    /// Input/output are 2D points (x, z used as x, y).
    /// </summary>
    public static class EarClipper
    {
        public static int[] Triangulate(List<Vector2> poly)
        {
            int n = poly.Count;
            var tris = new List<int>();
            if (n < 3) return tris.ToArray();

            // Ensure CCW winding in the (x, y) plane.
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % n];
                area += a.x * b.y - b.x * a.y;
            }
            var idx = new List<int>(n);
            for (int i = 0; i < n; i++) idx.Add(i);
            if (area < 0f) idx.Reverse();

            int guard = 0;
            while (idx.Count > 3 && guard++ < 10000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count];
                    int i1 = idx[i];
                    int i2 = idx[(i + 1) % idx.Count];
                    var a = poly[i0]; var b = poly[i1]; var c = poly[i2];
                    if (Cross(a, b, c) <= 1e-7f) continue; // reflex or degenerate
                    if (PointInTriangle(a, b, c, poly, idx, i0, i1, i2)) continue;
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break; // shouldn't happen for valid polygons
            }
            if (idx.Count == 3)
            {
                tris.Add(idx[0]); tris.Add(idx[1]); tris.Add(idx[2]);
            }
            return tris.ToArray();
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b)
        {
            return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        }

        static bool PointInTriangle(Vector2 a, Vector2 b, Vector2 c, List<Vector2> poly, List<int> idx, int i0, int i1, int i2)
        {
            for (int k = 0; k < idx.Count; k++)
            {
                int ii = idx[k];
                if (ii == i0 || ii == i1 || ii == i2) continue;
                var p = poly[ii];
                if (PointInTri(a, b, c, p)) return true;
            }
            return false;
        }

        static bool PointInTri(Vector2 a, Vector2 b, Vector2 c, Vector2 p)
        {
            float d1 = Cross(a, b, p);
            float d2 = Cross(b, c, p);
            float d3 = Cross(c, a, p);
            bool neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool pos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(neg && pos);
        }
    }
}
