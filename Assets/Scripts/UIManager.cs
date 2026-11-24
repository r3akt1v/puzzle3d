using UnityEngine;
using UnityEngine.UI;

namespace Puzzle3D
{
    /// <summary>All UI built in code: main menu, level select, HUD, win screen.</summary>
    public class UIManager : MonoBehaviour
    {
        public System.Action<int> OnLevelSelected;
        public System.Action OnRestart;
        public System.Action OnMenu;
        public System.Action OnNext;

        Canvas canvas;
        GameObject menuPanel, hudPanel, winPanel;
        Text timerText, levelText, winTimeText, winBestText;
        Button hintButton;
        Text hintLabel;
        Font font;

        static readonly Color BgColor = new Color(0.09f, 0.11f, 0.14f, 0.92f);
        static readonly Color PanelColor = new Color(0.14f, 0.17f, 0.22f, 0.95f);
        static readonly Color Accent = new Color(0.94f, 0.62f, 0.32f);
        static readonly Color AccentDim = new Color(0.94f, 0.62f, 0.32f, 0.25f);
        static readonly Color TextColor = new Color(0.93f, 0.9f, 0.85f);
        static readonly Color TextDim = new Color(0.6f, 0.62f, 0.66f);

        public Button HintButton => hintButton;
        public Text HintLabel => hintLabel;

        void Awake()
        {
            font = GetFont();
            BuildCanvas();
        }

        static Font GetFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f != null) return f;
            f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f != null) return f;
            return Font.CreateDynamicFontFromOSFont("Helvetica", 32);
        }

        void BuildCanvas()
        {
            var go = new GameObject("Canvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
        }

        // ================= MENU =================
        public void ShowMenu(string[] levelNames, string[] bestTimes)
        {
            if (menuPanel == null) BuildMenu();
            menuPanel.SetActive(true);
            hudPanel?.SetActive(false);
            winPanel?.SetActive(false);

            var container = menuPanel.transform.Find("Levels");
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i).gameObject;
                // keep template? we rebuild simply:
                Destroy(child);
            }
            for (int i = 0; i < levelNames.Length; i++)
            {
                int idx = i;
                string best = bestTimes[i];
                var btn = CreateButton(container, $"{levelNames[i]}   {(best.Length > 0 ? "·  " + best : "")}",
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 74),
                    i == 0 ? Accent : PanelColor, () => OnLevelSelected?.Invoke(idx));
                var t = btn.GetComponentInChildren<Text>();
                t.fontSize = 30;
                t.alignment = TextAnchor.MiddleLeft;
                var rt = (RectTransform)btn.transform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0, 140 - i * 92);
                rt.sizeDelta = new Vector2(460, 74);
            }
        }

        void BuildMenu()
        {
            menuPanel = NewPanel("Menu", BgColor);
            var title = CreateText(menuPanel.transform, "ПАЗЛ 3D", 84, Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            var trt = (RectTransform)title.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(0, 330);
            trt.sizeDelta = new Vector2(800, 110);

            var sub = CreateText(menuPanel.transform, "собери картинку из объёмных деталей", 26, TextDim, TextAnchor.MiddleCenter);
            var srt = (RectTransform)sub.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(0, 255);
            srt.sizeDelta = new Vector2(900, 40);

            var levels = new GameObject("Levels", typeof(RectTransform));
            levels.transform.SetParent(menuPanel.transform, false);

            var hint = CreateText(menuPanel.transform,
                "ЛКМ — взять и таскать   ·   скролл или ПКМ — крутить   ·   R — выровнять   ·   Esc — меню",
                22, TextDim, TextAnchor.MiddleCenter);
            var hrt = (RectTransform)hint.transform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0);
            hrt.pivot = new Vector2(0.5f, 0);
            hrt.anchoredPosition = new Vector2(0, 40);
            hrt.sizeDelta = new Vector2(1400, 40);
        }

        // ================= HUD =================
        public void ShowHUD()
        {
            if (hudPanel == null) BuildHUD();
            hudPanel.SetActive(true);
            menuPanel?.SetActive(false);
            winPanel?.SetActive(false);
        }

        void BuildHUD()
        {
            hudPanel = NewPanel("HUD", new Color(0, 0, 0, 0));
            hudPanel.GetComponent<Image>().raycastTarget = false;

            // top-left: level + timer
            var tl = NewPanel("TopLeft", PanelColor);
            tl.transform.SetParent(hudPanel.transform, false);
            var tlrt = (RectTransform)tl.transform;
            tlrt.anchorMin = new Vector2(0, 1); tlrt.anchorMax = new Vector2(0, 1);
            tlrt.pivot = new Vector2(0, 1);
            tlrt.anchoredPosition = new Vector2(24, -24);
            tlrt.sizeDelta = new Vector2(320, 110);

            levelText = CreateText(tl.transform, "Уровень 1", 34, TextColor, TextAnchor.UpperLeft, FontStyle.Bold);
            var lrt = (RectTransform)levelText.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(20, 30); lrt.offsetMax = new Vector2(-10, -10);

            timerText = CreateText(tl.transform, "00:00", 40, Accent, TextAnchor.LowerLeft, FontStyle.Bold);
            var tt = (RectTransform)timerText.transform;
            tt.anchorMin = Vector2.zero; tt.anchorMax = Vector2.one;
            tt.offsetMin = new Vector2(20, 8); tt.offsetMax = new Vector2(-10, -55);

            // top-right buttons
            hintButton = CreateButton(hudPanel.transform, "Подсказка (3)", Vector2.zero, Vector2.zero,
                new Vector2(210, 64), AccentDim, () => { });
            WireButton(hintButton, new Vector2(1, 1), new Vector2(-140, -56), new Vector2(210, 64));
            hintLabel = hintButton.GetComponentInChildren<Text>();
            hintLabel.fontSize = 26;

            var restartBtn = CreateButton(hudPanel.transform, "Заново", Vector2.zero, Vector2.zero,
                new Vector2(160, 64), PanelColor, () => OnRestart?.Invoke());
            WireButton(restartBtn, new Vector2(1, 1), new Vector2(-370, -56), new Vector2(160, 64));
            restartBtn.GetComponentInChildren<Text>().fontSize = 26;

            var menuBtn = CreateButton(hudPanel.transform, "Меню", Vector2.zero, Vector2.zero,
                new Vector2(130, 64), PanelColor, () => OnMenu?.Invoke());
            WireButton(menuBtn, new Vector2(1, 1), new Vector2(-530, -56), new Vector2(130, 64));
            menuBtn.GetComponentInChildren<Text>().fontSize = 26;

            // bottom controls hint
            var hint = CreateText(hudPanel.transform,
                "ЛКМ — таскать · скролл/ПКМ — крутить · R — выровнять · Esc — меню",
                22, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter);
            var hrt = (RectTransform)hint.transform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0);
            hrt.anchoredPosition = new Vector2(0, 30);
            hrt.sizeDelta = new Vector2(1500, 40);
        }

        static void WireButton(Button b, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public void SetLevelLabel(string s) { if (levelText != null) levelText.text = s; }
        public void SetTimer(string s) { if (timerText != null) timerText.text = s; }

        // ================= WIN =================
        public void ShowWin(string timeStr, string bestStr, bool hasNext)
        {
            if (winPanel == null) BuildWin();
            winPanel.SetActive(true);
            hudPanel?.SetActive(false);
            menuPanel?.SetActive(false);
            winTimeText.text = "Время: " + timeStr;
            winBestText.text = "Рекорд: " + bestStr;
            var nextBtn = winPanel.transform.Find("NextBtn");
            if (nextBtn != null) nextBtn.gameObject.SetActive(hasNext);
        }

        void BuildWin()
        {
            winPanel = NewPanel("Win", BgColor);
            var inner = NewPanel("Card", PanelColor);
            inner.transform.SetParent(winPanel.transform, false);
            var irt = (RectTransform)inner.transform;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(560, 460);

            var title = CreateText(inner.transform, "Уровень пройден!", 52, Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1);
            trt.anchoredPosition = new Vector2(0, -50);
            trt.sizeDelta = new Vector2(500, 80);

            winTimeText = CreateText(inner.transform, "", 36, TextColor, TextAnchor.MiddleCenter);
            var wtrt = (RectTransform)winTimeText.transform;
            wtrt.anchorMin = new Vector2(0, 1); wtrt.anchorMax = new Vector2(1, 1);
            wtrt.pivot = new Vector2(0.5f, 1);
            wtrt.anchoredPosition = new Vector2(0, -150);
            wtrt.sizeDelta = new Vector2(500, 50);

            winBestText = CreateText(inner.transform, "", 28, TextDim, TextAnchor.MiddleCenter);
            var wbrt = (RectTransform)winBestText.transform;
            wbrt.anchorMin = new Vector2(0, 1); wbrt.anchorMax = new Vector2(1, 1);
            wbrt.pivot = new Vector2(0.5f, 1);
            wbrt.anchoredPosition = new Vector2(0, -205);
            wbrt.sizeDelta = new Vector2(500, 40);

            float y = -290;
            var nextBtn = CreateButton(inner.transform, "Следующий уровень", Vector2.zero, Vector2.zero,
                new Vector2(380, 72), Accent, () => OnNext?.Invoke());
            nextBtn.name = "NextBtn";
            WireButton(nextBtn, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(380, 72));
            nextBtn.GetComponentInChildren<Text>().fontSize = 30;
            y -= 95;

            var againBtn = CreateButton(inner.transform, "Ещё раз", Vector2.zero, Vector2.zero,
                new Vector2(380, 72), PanelColor, () => OnRestart?.Invoke());
            WireButton(againBtn, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(380, 72));
            againBtn.GetComponentInChildren<Text>().fontSize = 30;
            y -= 95;

            var menuBtn = CreateButton(inner.transform, "В меню", Vector2.zero, Vector2.zero,
                new Vector2(380, 72), PanelColor, () => OnMenu?.Invoke());
            WireButton(menuBtn, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(380, 72));
            menuBtn.GetComponentInChildren<Text>().fontSize = 30;
        }

        // ================= HELPERS =================
        GameObject NewPanel(string name, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = bg;
            return go;
        }

        Text CreateText(Transform parent, string text, int size, Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        Button CreateButton(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, System.Action onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = bg;
            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(bg.r * 1.2f, bg.g * 1.2f, bg.b * 1.2f, 1f);
            colors.pressedColor = new Color(bg.r * 0.8f, bg.g * 0.8f, bg.b * 0.8f, 1f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var txt = CreateText(go.transform, label, 30, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            var trt = (RectTransform)txt.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            return btn;
        }
    }
}
