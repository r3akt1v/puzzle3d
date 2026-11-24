using System.Collections.Generic;
using UnityEngine;

namespace Puzzle3D
{
    /// <summary>
    /// Generates a full puzzle: edge-tab layout, piece meshes, image texture,
    /// materials and scattered placement on the table.
    /// </summary>
    public class PuzzleGenerator : MonoBehaviour
    {
        public struct PieceSpec
        {
            public int col, row;
            public int left, right, bottom, top; // -1 inward, 0 flat, +1 outward
        }

        public List<PieceController> Generate(
            int cols, int rows, int level,
            Board board, Transform parent,
            Material sideMat, Material bottomMat, Material topMatTemplate,
            Vector3 scatterCenter, Vector2 scatterSize)
        {
            var rng = new System.Random(level * 1000003 + Random.Range(0, 99999));
            int seed = rng.Next();

            // ---- tab layout ----
            // sharedV[r][c]: edge between col c-1 and c, for c in 1..cols-1.
            // +1 = tab protrudes into right cell.
            var sharedV = new int[rows, cols + 1]; // index c=1..cols-1 used
            var sharedH = new int[rows + 1, cols]; // index r=1..rows-1 used
            for (int r = 0; r < rows; r++)
                for (int c = 1; c < cols; c++)
                    sharedV[r, c] = rng.Next(2) == 0 ? 1 : -1;
            for (int r = 1; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    sharedH[r, c] = rng.Next(2) == 0 ? 1 : -1;

            var specs = new List<PieceSpec>(cols * rows);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    specs.Add(new PieceSpec
                    {
                        col = c, row = r,
                        // vertical edges: sharedV[r,c] = edge between col c-1 and c,
                        // +1 means tab protrudes into col c (+x direction)
                        left   = c > 0     ? -sharedV[r, c]     : 0,
                        right  = c < cols-1 ?  sharedV[r, c + 1] : 0,
                        // horizontal edges: sharedH[r,c] = edge between row r-1 and r.
                        // Row 0 sits at max +z (image top). +1 protrudes into row r (-z side).
                        // cell(r) top edge (+z side) borders row r-1 -> sharedH[r,c], inward when +1
                        // cell(r) bottom edge (-z side) borders row r+1 -> sharedH[r+1,c], outward when +1
                        top    = r > 0      ? -sharedH[r, c]     : 0,
                        bottom = r < rows-1 ?  sharedH[r + 1, c] : 0,
                    });
                }
            }

            // ---- image texture: real photo from Resources, procedural fallback ----
            Texture2D tex = LoadPuzzleImage(level, cols, rows, seed);

            float pieceSize = board.S;
            float tabH = pieceSize * 0.24f;
            float bevel = 0f; // classic jigsaw: flat parallelepiped, no chamfer
            Vector2 boardSize = board.BoardSizeXZ;

            var pieces = new List<PieceController>(specs.Count);
            var placed = new List<Vector3>();

            foreach (var s in specs)
            {
                Vector3 target = board.CellWorldPos(s.col, s.row);
                Vector2 uvPos = new Vector2(target.x - board.Origin.x, target.z - board.Origin.z);

                Mesh mesh = PieceMeshBuilder.Build(
                    pieceSize, board.Thickness, tabH, bevel,
                    s.left, s.right, s.bottom, s.top,
                    uvPos, boardSize);

                var go = new GameObject($"piece_{s.col}_{s.row}");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;

                // materials: [top(image), side(edge), bottom(back)]
                var topMat = new Material(topMatTemplate) { name = "piece_top_mat", mainTexture = tex };
                mr.sharedMaterials = new[] { topMat, sideMat, bottomMat };

                var pc = go.AddComponent<PieceController>();
                pc.Col = s.col; pc.Row = s.row;
                pc.TargetPos = target;
                pc.Init(board);
                pc.TopRenderer = mr;
                pc.TopMaterial = topMat;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;

                // scatter position with min-distance rejection
                Vector3 pos = FindScatterPos(rng, scatterCenter, scatterSize, placed, pieceSize);
                placed.Add(pos);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(0, rng.Next(0, 360), 0);

                pieces.Add(pc);
            }
            return pieces;
        }

        /// <summary>Loads a real puzzle image (animal photo) from Resources/PuzzleImages.
        /// Falls back to procedural generation if none found.</summary>
        static Texture2D LoadPuzzleImage(int level, int cols, int rows, int seed)
        {
            int lvl = level + 1;
            var rng = new System.Random(seed);
            // try both variants in random order
            var variants = new List<string> { "a", "b" };
            for (int i = variants.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (variants[i], variants[j]) = (variants[j], variants[i]);
            }
            foreach (var v in variants)
            {
                var t = Resources.Load<Texture2D>($"PuzzleImages/puz_l{lvl}_{v}");
                if (t != null)
                {
                    Debug.Log($"[Puzzle] level {level}: photo '{t.name}' {t.width}x{t.height}");
                    return t; // shared asset - must NOT be destroyed later
                }
            }
            Debug.LogWarning($"[Puzzle] level {level}: no photo found, using procedural");
            var proc = ImageGenerator.Generate(level, cols, rows, seed);
            proc.name = "proc_puzzle"; // marker so ClearAll can destroy it safely
            return proc;
        }

        Vector3 FindScatterPos(System.Random rng, Vector3 center, Vector2 size, List<Vector3> existing, float pieceSize)
        {
            float minDist = pieceSize * 0.75f;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                Vector3 p = new Vector3(
                    (float)(rng.NextDouble() * 2 - 1) * size.x * 0.5f + center.x,
                    0f,
                    (float)(rng.NextDouble() * 2 - 1) * size.y * 0.5f + center.z);
                bool ok = true;
                foreach (var e in existing)
                {
                    Vector3 d = e - p; d.y = 0;
                    if (d.sqrMagnitude < minDist * minDist) { ok = false; break; }
                }
                if (ok) return p;
            }
            // fallback: random anyway
            return new Vector3(
                (float)(rng.NextDouble() * 2 - 1) * size.x * 0.5f + center.x,
                0f,
                (float)(rng.NextDouble() * 2 - 1) * size.y * 0.5f + center.z);
        }

        public void ClearAll(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                var mf = child.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
                var mr = child.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    foreach (var m in mr.sharedMaterials)
                    {
                        if (m == null) continue;
                        // only per-piece instances are destroyed; shared side/bottom/top templates survive
                        if (m.name == "piece_top_mat")
                        {
                            // only destroy procedurally generated textures; Resources assets are shared
                            if (m.mainTexture != null && m.mainTexture.name == "proc_puzzle")
                                Destroy(m.mainTexture);
                            Destroy(m);
                        }
                    }
                }
                Destroy(child.gameObject);
            }
        }
    }
}
