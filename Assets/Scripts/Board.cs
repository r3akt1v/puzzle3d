using System.Collections.Generic;
using UnityEngine;

namespace Puzzle3D
{
    /// <summary>
    /// Board logic: grid layout, target positions, ghost preview, frame visuals.
    /// </summary>
    public class Board : MonoBehaviour
    {
        public int Cols, Rows;
        public float S = 1f;          // piece cell size
        public float Thickness = 0.12f;
        public float SnapRadius = 0.45f;
        public float PieceRestY => 0.001f; // mesh pivot is at bottom face

        public Vector3 Origin; // world center of board
        public float BoardW => Cols * S;
        public float BoardH => Rows * S;

        // ghost previews
        GameObject assistGhost;
        GameObject hintGhost;
        Material ghostMatAssist;
        Material ghostMatHint;
        MeshRenderer assistRenderer, hintRenderer;
        Bounds lastBounds;

        public void Setup(int cols, int rows, float pieceSize, float thickness, Vector3 origin)
        {
            Cols = cols; Rows = rows; S = pieceSize; Thickness = thickness; Origin = origin;
            SnapRadius = S * 0.5f;
        }

        public Vector3 CellWorldPos(int col, int row)
        {
            // row 0 = image top = far side (+z from camera at -z)
            float x = Origin.x + (col - (Cols - 1) * 0.5f) * S;
            float z = Origin.z + ((Rows - 1) * 0.5f - row) * S;
            return new Vector3(x, PieceRestY, z);
        }

        public Vector2 BoardSizeXZ => new Vector2(BoardW, BoardH);

        public void NotifyPiecePlaced(PieceController p) => GameManager.I.OnPiecePlaced(p);

        // ---------- Ghost ----------
        public void EnsureGhosts()
        {
            if (assistGhost == null) assistGhost = CreateGhost(out assistRenderer);
            if (hintGhost == null) hintGhost = CreateGhost(out hintRenderer);
            if (ghostMatAssist == null)
            {
                ghostMatAssist = MakeGhostMaterial(new Color(0.4f, 0.9f, 1f, 0.35f));
                ghostMatHint = MakeGhostMaterial(new Color(1f, 0.85f, 0.3f, 0.4f));
            }
        }

        GameObject CreateGhost(out MeshRenderer r)
        {
            var go = new GameObject("ghost");
            go.transform.SetParent(transform, false);
            r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return go;
        }

        Material MakeGhostMaterial(Color c)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = c;
            m.SetFloat("_Mode", 3f); // transparent
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = 3000;
            return m;
        }

        /// <summary>Show ghost at the piece's target pose (assist while dragging).</summary>
        public void ShowAssistGhost(PieceController p)
        {
            EnsureGhosts();
            var mf = assistGhost.GetComponent<MeshFilter>();
            if (mf == null) mf = assistGhost.AddComponent<MeshFilter>();
            mf.sharedMesh = p.GetComponent<MeshFilter>().sharedMesh;
            assistRenderer.sharedMaterial = ghostMatAssist;
            assistGhost.transform.SetPositionAndRotation(p.TargetPos, Quaternion.identity);
            assistGhost.SetActive(true);
        }

        public void HideAssistGhost()
        {
            if (assistGhost != null) assistGhost.SetActive(false);
        }

        /// <summary>Show timed hint ghost at piece's target.</summary>
        public void ShowHintGhost(PieceController p, float seconds)
        {
            EnsureGhosts();
            CancelInvoke(nameof(HideHintGhost));
            var mf = hintGhost.GetComponent<MeshFilter>();
            if (mf == null) mf = hintGhost.AddComponent<MeshFilter>();
            mf.sharedMesh = p.GetComponent<MeshFilter>().sharedMesh;
            hintRenderer.sharedMaterial = ghostMatHint;
            hintGhost.transform.SetPositionAndRotation(p.TargetPos, Quaternion.identity);
            hintGhost.SetActive(true);
            Invoke(nameof(HideHintGhost), seconds);
        }

        public void HideHintGhost()
        {
            if (hintGhost != null) hintGhost.SetActive(false);
        }
    }
}
