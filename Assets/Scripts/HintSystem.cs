using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Puzzle3D
{
    /// <summary>3 hint charges per level: highlight a random unplaced piece + ghost at its target.</summary>
    public class HintSystem : MonoBehaviour
    {
        public int Charges = 3;
        public const int MaxCharges = 3;

        Board board;
        List<PieceController> pieces;
        Text hintButtonLabel;
        Button hintButton;

        public void Init(Board b, List<PieceController> p, Button btn, Text label)
        {
            board = b; pieces = p;
            hintButton = btn; hintButtonLabel = label;
            hintButton.onClick.RemoveAllListeners();
            hintButton.onClick.AddListener(UseHint);
            RefreshUI();
        }

        public void Reset(int charges = MaxCharges)
        {
            Charges = charges;
            RefreshUI();
        }

        public void SetPieces(List<PieceController> p) => pieces = p;

        void RefreshUI()
        {
            if (hintButtonLabel != null)
                hintButtonLabel.text = Charges > 0 ? $"Подсказка ({Charges})" : "Подсказки нет";
            if (hintButton != null) hintButton.interactable = Charges > 0;
        }

        void UseHint()
        {
            if (Charges <= 0 || pieces == null || board == null) return;
            var candidates = new List<PieceController>();
            foreach (var p in pieces)
                if (p.State == PieceState.OnTable) candidates.Add(p);
            if (candidates.Count == 0) return;

            var pick = candidates[Random.Range(0, candidates.Count)];
            Charges--;
            pick.SetHighlight(true);
            board.ShowHintGhost(pick, 4f);
            AudioManager.I.PlayHint();
            StartCoroutine(StopHighlightAfter(pick, 4f));
            RefreshUI();
        }

        System.Collections.IEnumerator StopHighlightAfter(PieceController p, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (p != null && p.State != PieceState.Placed) p.SetHighlight(false);
        }

        public void ClearHighlights()
        {
            if (pieces == null) return;
            foreach (var p in pieces) p.SetHighlight(false);
        }
    }
}
