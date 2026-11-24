using System.Collections;
using UnityEngine;

namespace Puzzle3D
{
    public enum PieceState { OnTable, Held, Placing, Placed }

    /// <summary>One physical puzzle piece: mesh, state, target cell.</summary>
    public class PieceController : MonoBehaviour
    {
        public int Col, Row;
        public Vector3 TargetPos;
        public PieceState State = PieceState.OnTable;

        public MeshRenderer TopRenderer;
        public Material TopMaterial; // instance, for tint/highlight
        Color baseTint = Color.white;

        Board board;
        Coroutine highlightRoutine;

        public void Init(Board b) => board = b;

        public Vector3 TargetWorld => TargetPos;

        public bool IsCloseToTarget(float radius)
        {
            Vector3 d = transform.position - TargetPos;
            d.y = 0f;
            return d.magnitude < radius && transform.up.y > 0.3f;
        }

        /// <summary>Try to snap into place. Returns true if placement started.</summary>
        public bool TryPlace()
        {
            if (State != PieceState.Held) return false;
            float radius = board.SnapRadius;
            Vector3 d = transform.position - TargetPos;
            d.y = 0f;
            if (d.magnitude < radius && transform.up.y > 0.3f)
            {
                StartCoroutine(PlaceRoutine());
                return true;
            }
            return false;
        }

        IEnumerator PlaceRoutine()
        {
            State = PieceState.Placing;
            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;
            float t = 0f;
            const float dur = 0.18f;
            AudioManager.I.PlayPlace();
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                transform.position = Vector3.Lerp(fromPos, TargetPos, k);
                transform.rotation = Quaternion.Slerp(fromRot, Quaternion.identity, k);
                yield return null;
            }
            transform.position = TargetPos;
            transform.rotation = Quaternion.identity;
            State = PieceState.Placed;
            board.NotifyPiecePlaced(this);
        }

        /// <summary>Return to lying flat on the table at current XZ.</summary>
        public void Settle()
        {
            StartCoroutine(SettleRoutine());
        }

        IEnumerator SettleRoutine()
        {
            State = PieceState.OnTable;
            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;
            // keep current yaw, flatten tilt
            Vector3 fwd = transform.forward; fwd.y = 0f;
            Quaternion flatRot = fwd.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(fwd.normalized, Vector3.up)
                : Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            float targetY = board.PieceRestY;
            Vector3 toPos = new Vector3(fromPos.x, targetY, fromPos.z);
            float t = 0f;
            const float dur = 0.22f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                transform.position = Vector3.Lerp(fromPos, toPos, k);
                transform.rotation = Quaternion.Slerp(fromRot, flatRot, k);
                yield return null;
            }
            transform.position = toPos;
            transform.rotation = flatRot;
        }

        public void SetHighlight(bool on)
        {
            if (highlightRoutine != null) StopCoroutine(highlightRoutine);
            highlightRoutine = null;
            if (TopMaterial == null) return;
            if (on)
            {
                highlightRoutine = StartCoroutine(HighlightPulse());
            }
            else
            {
                TopMaterial.color = baseTint;
            }
        }

        IEnumerator HighlightPulse()
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime;
                float k = 0.5f + 0.5f * Mathf.Sin(t * 6f);
                TopMaterial.color = Color.Lerp(baseTint, new Color(1f, 0.85f, 0.3f), k);
                yield return null;
            }
        }

        public void ResetTint()
        {
            if (highlightRoutine != null) { StopCoroutine(highlightRoutine); highlightRoutine = null; }
            if (TopMaterial != null) TopMaterial.color = baseTint;
        }
    }
}
