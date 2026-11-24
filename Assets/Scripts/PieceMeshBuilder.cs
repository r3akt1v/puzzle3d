using System;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle3D
{
    /// <summary>
    /// Builds a single jigsaw piece mesh procedurally.
    /// Piece lies flat in XZ plane, thickness along +Y, pivot at bottom-center.
    /// </summary>
    public static class PieceMeshBuilder
    {
        public const int EdgeSamples = 14;

        /// <summary>
        /// out-signs: +1 = tab protrudes outward, -1 = inward, 0 = flat.
        /// uvBoardPos: world XZ position of the piece center on the board (for UV mapping).
        /// boardSize: total board size in world units.
        /// </summary>
        public static Mesh Build(
            float size, float thickness, float tabHeight, float bevel,
            int left, int right, int bottom, int top,
            Vector2 uvBoardPos, Vector2 boardSize)
        {
            float half = size * 0.5f;
            var centroid = Vector2.zero;

            // ---- Build boundary loop (CCW in XZ: x right, z "up" in plan view) ----
            // Traversal: bottom edge L->R, right edge B->T, top edge R->L, left edge T->B.
            var boundary = new List<Vector2>(EdgeSamples * 4 + 8);

            // bottom edge: from (-h,-h) to (+h,-h), outward = -z
            AddEdge(boundary, -half, -half, +half, -half, new Vector2(0, -1), bottom, tabHeight);
            // right edge: (+h,-h) -> (+h,+h), outward = +x
            AddEdge(boundary, +half, -half, +half, +half, new Vector2(1, 0), right, tabHeight);
            // top edge: (+h,+h) -> (-h,+h), outward = +z
            AddEdge(boundary, +half, +half, -half, +half, new Vector2(0, 1), top, tabHeight);
            // left edge: (-h,+h) -> (-h,-h), outward = -x
            AddEdge(boundary, -half, +half, -half, -half, new Vector2(-1, 0), left, tabHeight);

            foreach (var p in boundary) centroid += p;
            centroid /= boundary.Count;

            // ---- Inset polygon for the top face (fake bevel) ----
            float inset = Mathf.Min(bevel * 2.5f, size * 0.08f);
            var topPoly = new List<Vector2>(boundary.Count);
            foreach (var p in boundary)
            {
                Vector2 dir = (p - centroid);
                float len = dir.magnitude;
                topPoly.Add(len > 1e-5f ? p - dir / len * inset : p);
            }

            float yBot = 0f;
            float ySideTop = thickness - bevel;
            float yTop = thickness;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var trisTop = new List<int>();
            var trisSide = new List<int>();
            var trisBottom = new List<int>();

            // ---- Top face (inset polygon at yTop) ----
            var topIdx = new List<int>();
            foreach (var p in topPoly)
            {
                topIdx.Add(verts.Count);
                verts.Add(new Vector3(p.x, yTop, p.y));
                uvs.Add(BoardUV(p, uvBoardPos, boardSize));
            }
            var topTris = EarClipper.Triangulate(topPoly);
            foreach (var t in topTris) trisTop.Add(topIdx[t]); // map polygon idx -> verts idx
            EnsureWindingUp(trisTop, verts, Vector3.up);

            // ---- Bevel ring + side walls (boundary polygon) ----
            int n = boundary.Count;
            // side verts at yBot and ySideTop
            var sideBotIdx = new int[n];
            var sideTopIdx = new int[n];
            for (int i = 0; i < n; i++)
            {
                var p = boundary[i];
                sideBotIdx[i] = verts.Count;
                verts.Add(new Vector3(p.x, yBot, p.y));
                uvs.Add(new Vector2(0, 0));

                sideTopIdx[i] = verts.Count;
                verts.Add(new Vector3(p.x, ySideTop, p.y));
                uvs.Add(new Vector2(0, 1));
            }
            // side quads between consecutive boundary verts
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 mid = (verts[sideBotIdx[i]] + verts[sideBotIdx[j]]) * 0.5f;
                Vector3 outward = new Vector3(mid.x - centroid.x, 0, mid.z - centroid.y);
                if (outward.sqrMagnitude < 1e-6f) outward = Vector3.up;
                outward.Normalize();
                AddQuadAuto(trisSide, verts, uvs,
                    sideBotIdx[i], sideBotIdx[j], sideTopIdx[j], sideTopIdx[i], outward);
            }
            // bevel quads: boundary@ySideTop -> topPoly@yTop
            // topPoly vertex i corresponds to boundary vertex i (same order/count)
            if (bevel > 0.001f)
            {
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    Vector2 b0 = boundary[i]; var b1 = boundary[j];
                    Vector2 mid = (b0 + b1) * 0.5f;
                    Vector3 outward = new Vector3(mid.x - centroid.x, 0.6f, mid.y - centroid.y).normalized;
                    AddQuadAuto(trisSide, verts, uvs,
                        sideTopIdx[i], sideTopIdx[j], topIdx[j], topIdx[i], outward);
                }
            }

            // ---- Bottom face (boundary polygon at yBot, facing down) ----
            var botIdx = new List<int>();
            foreach (var p in boundary)
            {
                botIdx.Add(verts.Count);
                verts.Add(new Vector3(p.x, yBot, p.y));
                uvs.Add(BoardUV(p, uvBoardPos, boardSize));
            }
            var botTris = EarClipper.Triangulate(boundary);
            foreach (var t in botTris) trisBottom.Add(botIdx[t]); // map polygon idx -> verts idx
            EnsureWindingUp(trisBottom, verts, Vector3.down);

            var mesh = new Mesh { name = "piece" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(trisTop, 0);
            mesh.SetTriangles(trisSide, 1);
            mesh.SetTriangles(trisBottom, 2);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddEdge(List<Vector2> pts, float x0, float z0, float x1, float z1, Vector2 outward, int sign, float tabHeight)
        {
            // samples i=0..EdgeSamples-1 (last point equals next edge's first -> skipped)
            for (int i = 0; i < EdgeSamples; i++)
            {
                float u = (float)i / EdgeSamples;
                float h = sign * JigsawProfile(u) * tabHeight;
                pts.Add(new Vector2(
                    Mathf.Lerp(x0, x1, u) + outward.x * h,
                    Mathf.Lerp(z0, z1, u) + outward.y * h));
            }
        }

        /// <summary>Jigsaw knob profile: 0 at ends, ~1 at center. Catmull-Rom through controls.</summary>
        static float JigsawProfile(float u)
        {
            // control points (u, height)
            // Classic jigsaw profile: flat -> steep neck -> wide rounded head -> flat.
            // u=0.5 is edge midpoint; height 1 = full tab depth.
            ReadOnlySpan<Vector2> cp = stackalloc Vector2[]
            {
                new Vector2(0.00f, 0.00f),
                new Vector2(0.15f, 0.01f),
                new Vector2(0.27f, 0.05f),
                new Vector2(0.34f, 0.35f),   // steep neck
                new Vector2(0.40f, 0.62f),
                new Vector2(0.46f, 0.85f),
                new Vector2(0.50f, 1.00f),   // head center
                new Vector2(0.54f, 0.85f),
                new Vector2(0.60f, 0.62f),
                new Vector2(0.66f, 0.35f),   // steep neck
                new Vector2(0.73f, 0.05f),
                new Vector2(0.85f, 0.01f),
                new Vector2(1.00f, 0.00f),
            };
            if (u <= 0f) return 0f;
            if (u >= 1f) return 0f;
            // find segment
            int seg = 0;
            for (int i = 0; i < cp.Length - 1; i++)
            {
                if (u >= cp[i].x && u <= cp[i + 1].x) { seg = i; break; }
                seg = i;
            }
            // Catmull-Rom
            Vector2 p0 = cp[Mathf.Max(0, seg - 1)];
            Vector2 p1 = cp[seg];
            Vector2 p2 = cp[seg + 1];
            Vector2 p3 = cp[Mathf.Min(cp.Length - 1, seg + 2)];
            float t = (u - p1.x) / Mathf.Max(1e-5f, p2.x - p1.x);
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1.y) +
                (-p0.y + p2.y) * t +
                (2f * p0.y - 5f * p1.y + 4f * p2.y - p3.y) * t2 +
                (-p0.y + 3f * p1.y - 3f * p2.y + p3.y) * t3);
        }

        static Vector2 BoardUV(Vector2 localXZ, Vector2 boardPos, Vector2 boardSize)
        {
            float bx = boardPos.x + localXZ.x;
            float bz = boardPos.y + localXZ.y;
            return new Vector2(
                Mathf.Clamp01((bx + boardSize.x * 0.5f) / boardSize.x),
                Mathf.Clamp01((bz + boardSize.y * 0.5f) / boardSize.y));
        }

        /// <summary>Adds a quad (v0,v1,v2,v3 order) auto-oriented so its normal faces `desired`.</summary>
        static void AddQuadAuto(List<int> tris, List<Vector3> verts, List<Vector2> uvs,
            int a, int b, int c, int d, Vector3 desired)
        {
            // tri1: a,b,c ; tri2: a,c,d
            Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(n, desired) < 0f)
            {
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(a); tris.Add(d); tris.Add(c);
            }
            else
            {
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(d);
            }
        }

        static void EnsureWindingUp(List<int> tris, List<Vector3> verts, Vector3 desired)
        {
            if (tris.Count < 3) return;
            Vector3 n = Vector3.zero;
            for (int i = 0; i < tris.Count; i += 3)
            {
                n += Vector3.Cross(verts[tris[i + 1]] - verts[tris[i]], verts[tris[i + 2]] - verts[tris[i]]);
            }
            if (Vector3.Dot(n, desired) < 0f)
            {
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int tmp = tris[i + 1];
                    tris[i + 1] = tris[i + 2];
                    tris[i + 2] = tmp;
                }
            }
        }
    }
}
