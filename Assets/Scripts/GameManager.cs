using System.Collections.Generic;
using UnityEngine;

namespace Puzzle3D
{
    /// <summary>Central game state: levels, spawning, timer, win condition.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        public enum GameState { Menu, Playing, Won }
        public GameState State = GameState.Menu;

        public struct LevelConfig
        {
            public string Name;
            public int Cols, Rows;
        }

        public static readonly LevelConfig[] Levels =
        {
            new LevelConfig { Name = "Уровень 1 · 3×3",  Cols = 3, Rows = 3 },
            new LevelConfig { Name = "Уровень 2 · 4×3",  Cols = 4, Rows = 3 },
            new LevelConfig { Name = "Уровень 3 · 5×4",  Cols = 5, Rows = 4 },
            new LevelConfig { Name = "Уровень 4 · 6×5",  Cols = 6, Rows = 5 },
            new LevelConfig { Name = "Уровень 5 · 8×6",  Cols = 8, Rows = 6 },
        };

        // components
        Board board;
        PuzzleGenerator generator;
        DragController drag;
        HintSystem hints;
        UIManager ui;
        Transform piecesRoot;

        // shared materials
        Material sideMat, bottomMat, topMatTemplate;

        int levelIndex = 0;
        int placedCount, totalCount;
        float timer;
        List<PieceController> pieces = new List<PieceController>();
        Vector3 cameraHomePos;
        Quaternion cameraHomeRot;
        float cameraDistance;

        void Awake()
        {
            if (I != null) { Destroy(gameObject); return; }
            I = this;

            EnvironmentBuilder.Ensure();
            gameObject.AddComponent<AudioManager>();

            // board logic object
            var boardGo = new GameObject("Board");
            board = boardGo.AddComponent<Board>();

            var piecesGo = new GameObject("Pieces");
            piecesRoot = piecesGo.transform;

            generator = gameObject.AddComponent<PuzzleGenerator>();
            drag = gameObject.AddComponent<DragController>();
            hints = gameObject.AddComponent<HintSystem>();
            ui = gameObject.AddComponent<UIManager>();

            // shared piece materials
            sideMat = new Material(Shader.Find("Standard")) { color = new Color(0.85f, 0.78f, 0.66f) };
            sideMat.SetFloat("_Glossiness", 0.25f);
            bottomMat = new Material(Shader.Find("Standard")) { color = new Color(0.92f, 0.88f, 0.8f) };
            bottomMat.SetFloat("_Glossiness", 0.15f);
            topMatTemplate = new Material(Shader.Find("Standard")) { color = Color.white };
            topMatTemplate.SetFloat("_Glossiness", 0.45f);

            // wire UI callbacks
            ui.OnLevelSelected += StartLevel;
            ui.OnRestart += () => StartLevel(levelIndex);
            ui.OnMenu += ToMenu;
            ui.OnNext += () => { if (levelIndex < Levels.Length - 1) StartLevel(levelIndex + 1); };

            board.EnsureGhosts();

            // camera default (recomputed per level)
            var cam = Camera.main;
            cameraHomePos = cam.transform.position;
            cameraHomeRot = cam.transform.rotation;

            drag.Cam = cam;
            drag.Board = board;

            ShowMenu();
        }

        void Update()
        {
            if (State == GameState.Playing)
            {
                timer += Time.deltaTime;
                ui.SetTimer(FormatTime(timer));
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (State != GameState.Menu) ToMenu();
            }
        }

        // ---------- flow ----------
        void ShowMenu()
        {
            State = GameState.Menu;
            drag.InputEnabled = false;
            hints.ClearHighlights();
            board.HideAssistGhost();
            board.HideHintGhost();
            ClearPieces();

            var names = new string[Levels.Length];
            var bests = new string[Levels.Length];
            for (int i = 0; i < Levels.Length; i++)
            {
                names[i] = Levels[i].Name;
                float best = PlayerPrefs.GetFloat("best_" + i, -1f);
                bests[i] = best > 0 ? FormatTime(best) : "";
            }
            ui.ShowMenu(names, bests);
        }

        public void StartLevel(int idx)
        {
            levelIndex = Mathf.Clamp(idx, 0, Levels.Length - 1);
            var cfg = Levels[levelIndex];
            ClearPieces();
            hints.ClearHighlights();

            // piece size so board fits nicely; bigger levels -> slightly smaller pieces
            float pieceSize = levelIndex <= 1 ? 2.0f : levelIndex == 2 ? 1.7f : levelIndex == 3 ? 1.45f : 1.2f;
            float thickness = pieceSize * 0.11f;

            float boardW = cfg.Cols * pieceSize;
            float boardH = cfg.Rows * pieceSize;
            Vector3 origin = new Vector3(0, 0, 0.4f);
            board.Setup(cfg.Cols, cfg.Rows, pieceSize, thickness, origin);

            EnvironmentBuilder.BuildFrame(board, board.transform);

            // scatter area = around board
            float scatterW = boardW * 2.6f + 4f;
            float scatterH = boardH * 2.6f + 6f;
            EnvironmentBuilder.ResizeTable(scatterW + 8f, scatterH + 10f);

            pieces = generator.Generate(cfg.Cols, cfg.Rows, levelIndex,
                board, piecesRoot, sideMat, bottomMat, topMatTemplate,
                new Vector3(0, 0, 0.4f), new Vector2(scatterW, scatterH));
            totalCount = pieces.Count;
            placedCount = 0;

            // camera: fit board + scatter
            PositionCamera(boardW, boardH, scatterW, scatterH);

            if (!hintsWired)
            {
                ui.ShowHUD();
                hints.Init(board, pieces, ui.HintButton, ui.HintLabel);
                hintsWired = true;
            }
            else
            {
                hints.SetPieces(pieces);
                ui.ShowHUD();
            }
            hints.Reset(HintSystem.MaxCharges);
            drag.InputEnabled = true;
            State = GameState.Playing;
            timer = 0f;

            ui.ShowHUD();
            ui.SetLevelLabel(cfg.Name);
            ui.SetTimer("00:00");
        }

        bool hintsWired = false;

        public void OnPiecePlaced(PieceController p)
        {
            placedCount++;
            if (placedCount >= totalCount && State == GameState.Playing)
            {
                State = GameState.Won;
                drag.InputEnabled = false;
                float t = timer;
                string bestStr;
                float prevBest = PlayerPrefs.GetFloat("best_" + levelIndex, -1f);
                if (prevBest < 0 || t < prevBest)
                {
                    PlayerPrefs.SetFloat("best_" + levelIndex, t);
                    PlayerPrefs.Save();
                    bestStr = FormatTime(t) + "  ★ новый рекорд";
                }
                else
                {
                    bestStr = FormatTime(prevBest);
                }
                AudioManager.I.PlayWin();
                ui.ShowWin(FormatTime(t), bestStr, levelIndex < Levels.Length - 1);
            }
        }

        void ToMenu()
        {
            State = GameState.Menu;
            drag.InputEnabled = false;
            hints.ClearHighlights();
            board.HideAssistGhost();
            board.HideHintGhost();
            ClearPieces();
            ShowMenu();
        }

        void ClearPieces()
        {
            generator.ClearAll(piecesRoot);
            pieces.Clear();
            var frame = GameObject.Find("BoardFrame");
            if (frame != null) Destroy(frame);
        }

        void PositionCamera(float boardW, float boardH, float scatterW, float scatterH)
        {
            var cam = Camera.main;
            if (cam == null) return;
            float need = Mathf.Max(
                Mathf.Max(boardW * 1.9f, 8f),
                Mathf.Max(boardH * 1.9f, 8f),
                scatterW * 0.8f, scatterH * 0.8f);
            float dist = need * 1.35f + 3f;
            Vector3 pos = new Vector3(0, dist * 0.95f, -dist * 0.9f + 0.4f);
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(0, 0, 0.4f) - pos);
            drag.SetCameraBase(pos, cam.transform.rotation);
        }

        static string FormatTime(float t)
        {
            int m = (int)(t / 60f);
            int s = (int)(t % 60f);
            return $"{m:00}:{s:00}";
        }
    }
}
