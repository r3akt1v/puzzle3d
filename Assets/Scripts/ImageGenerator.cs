using UnityEngine;

namespace Puzzle3D
{
    /// <summary>
    /// Procedural puzzle images. Each level gets a distinct visual style,
    /// later levels are more detailed.
    /// </summary>
    public static class ImageGenerator
    {
        public static Texture2D Generate(int level, int cols, int rows, int seed)
        {
            int w = Mathf.Max(64, cols * 144);
            int h = Mathf.Max(64, rows * 144);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var rng = new System.Random(seed);
            var n1 = new ValueNoise(seed);
            var n2 = new ValueNoise(seed * 7 + 13);
            var n3 = new ValueNoise(seed * 31 + 101);

            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1); // 0 bottom, 1 top
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1);
                    Color c = Style(level, u, v, n1, n2, n3, rng);
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
            return tex;
        }

        static Color Style(int level, float u, float v, ValueNoise n1, ValueNoise n2, ValueNoise n3, System.Random rng)
        {
            switch (level)
            {
                case 0: return Candy(u, v, n1, rng);
                case 1: return Landscape(u, v, n1, n2, rng);
                case 2: return Geometric(u, v, n2, rng);
                case 3: return Aurora(u, v, n1, n2, rng);
                default: return Detailed(u, v, n1, n2, n3, rng);
            }
        }

        // ---------- Level 1: soft candy blobs, high contrast ----------
        static Color Candy(float u, float v, ValueNoise n, System.Random rng)
        {
            // stable blobs via hash grid
            float baseN = n.Fbm(u * 3f, v * 3f, 4);
            Color bg = Color.Lerp(new Color(0.95f, 0.9f, 0.8f), new Color(0.85f, 0.75f, 0.85f), v);
            float blob = n.Fbm(u * 6f + 9f, v * 6f + 4f, 3);
            Color accent = blob > 0.55f
                ? new Color(0.85f, 0.35f, 0.3f)
                : blob > 0.45f
                    ? new Color(0.2f, 0.55f, 0.6f)
                    : new Color(0.95f, 0.8f, 0.3f);
            float t = Mathf.SmoothStep(0.35f, 0.65f, blob + (baseN - 0.5f) * 0.3f);
            Color c = Color.Lerp(bg, accent, t * 0.85f);
            // vignette-ish darkening at edges for contrast
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            c *= 1f - Mathf.Clamp01(0.25f - edge * 2f) * 0.15f;
            return c;
        }

        // ---------- Level 2: landscape ----------
        static Color Landscape(float u, float v, ValueNoise n1, ValueNoise n2, System.Random rng)
        {
            Color c;
            if (v > 0.62f)
            {
                // sky
                c = Color.Lerp(new Color(0.55f, 0.75f, 0.9f), new Color(0.85f, 0.9f, 0.95f), (v - 0.62f) / 0.38f);
                // sun
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.72f, 0.85f));
                if (d < 0.09f) c = Color.Lerp(c, new Color(1f, 0.9f, 0.6f), 0.9f);
            }
            else
            {
                // mountains + hills
                float ridge1 = 0.5f + 0.28f * n1.Fbm1(u * 4f, 3);
                float ridge2 = 0.32f + 0.2f * n2.Fbm1(u * 7f + 40f, 4);
                float m1 = Mathf.SmoothStep(ridge1, ridge1 + 0.02f, v);
                float m2 = Mathf.SmoothStep(ridge2, ridge2 + 0.02f, v);
                Color far = new Color(0.45f, 0.55f, 0.7f);
                Color near = new Color(0.25f, 0.45f, 0.35f);
                Color ground = new Color(0.3f, 0.55f, 0.3f);
                c = Color.Lerp(ground, near, m2);
                c = Color.Lerp(c, far, m1 * 0.8f);
                // snow caps on far ridge
                float snow = Mathf.SmoothStep(ridge1 + 0.06f, ridge1 + 0.1f, v) * m1;
                c = Color.Lerp(c, new Color(0.95f, 0.97f, 1f), snow * 0.8f);
                // water band near horizon
                float water = Mathf.SmoothStep(0.66f, 0.63f, v) * (1f - m1);
                c = Color.Lerp(c, new Color(0.35f, 0.6f, 0.75f), water * 0.7f);
            }
            c *= 0.95f + 0.1f * n2.Fbm(u * 12f, v * 12f, 2);
            return c;
        }

        // ---------- Level 3: geometric composition ----------
        static Color Geometric(float u, float v, ValueNoise n, System.Random rng)
        {
            Color bg = new Color(0.13f, 0.15f, 0.22f);
            Color c = bg;
            // warped grid of circles
            float warp = n.Fbm(u * 3f, v * 3f, 3) * 0.08f;
            float gx = (u + warp) * 6f, gy = (v - warp) * 6f;
            float cx = gx - Mathf.Floor(gx), cy = gy - Mathf.Floor(gy);
            float d = Vector2.Distance(new Vector2(cx, cy), new Vector2(0.5f, 0.5f));
            int cell = Mathf.FloorToInt(gx) * 31 + Mathf.FloorToInt(gy) * 17;
            var r2 = new System.Random(cell * 7919 + 3);
            Color[] palette = {
                new Color(0.9f, 0.45f, 0.35f), new Color(0.95f, 0.75f, 0.3f),
                new Color(0.35f, 0.7f, 0.65f), new Color(0.55f, 0.5f, 0.9f),
                new Color(0.95f, 0.55f, 0.7f)
            };
            Color col = palette[Mathf.Abs(cell) % palette.Length];
            float rad = 0.18f + 0.12f * (float)r2.NextDouble();
            if (d < rad) c = Color.Lerp(c, col, 0.9f);
            // diagonal accent lines
            float line = Mathf.Abs(Mathf.Repeat(u * 2f + v * 2f + warp * 4f, 1f) - 0.5f);
            if (line < 0.015f && v > 0.3f) c = Color.Lerp(c, new Color(0.9f, 0.9f, 0.95f), 0.5f);
            // big soft glow blobs
            float g = n.Fbm(u * 2f + 11f, v * 2f + 5f, 3);
            c += new Color(0.1f, 0.08f, 0.15f) * g;
            return c;
        }

        // ---------- Level 4: aurora bands ----------
        static Color Aurora(float u, float v, ValueNoise n1, ValueNoise n2, System.Random rng)
        {
            Color c = Color.Lerp(new Color(0.03f, 0.04f, 0.1f), new Color(0.08f, 0.05f, 0.16f), v);
            // stars
            float star = n3Hash(u * 90f, v * 90f);
            if (star > 0.985f) c += new Color(0.8f, 0.85f, 1f) * ((star - 0.985f) / 0.015f) * 2f;
            // aurora ribbons
            for (int i = 0; i < 3; i++)
            {
                float bandU = n1.Fbm(u * 2.2f + i * 7.3f, v * 1.4f + i * 3.1f, 3);
                float center = 0.55f + 0.25f * Mathf.Sin(u * 5f + i * 2.1f) + (bandU - 0.5f) * 0.5f;
                float d = Mathf.Abs(v - center);
                float band = Mathf.Exp(-d * d * 18f);
                Color bandCol = i == 0 ? new Color(0.2f, 0.9f, 0.6f)
                    : i == 1 ? new Color(0.4f, 0.5f, 1f)
                    : new Color(0.9f, 0.4f, 0.8f);
                c += bandCol * band * 0.55f;
            }
            // mountains silhouette bottom
            float ridge = 0.18f + 0.12f * n2.Fbm1(u * 5f, 8);
            if (v < ridge) c = Color.Lerp(c, new Color(0.02f, 0.03f, 0.06f), 0.9f);
            return c;
        }

        // ---------- Level 5: detailed combo ----------
        static Color Detailed(float u, float v, ValueNoise n1, ValueNoise n2, ValueNoise n3, System.Random rng)
        {
            // base: rich landscape
            Color c = Landscape(u, v, n1, n2, rng);
            // geometric overlay: rings + dots, subtle
            float warp = n3.Fbm(u * 3f, v * 3f, 3) * 0.05f;
            float gx = (u + warp) * 8f, gy = (v - warp) * 8f;
            float fx = gx - Mathf.Floor(gx) - 0.5f, fy = gy - Mathf.Floor(gy) - 0.5f;
            float d = Mathf.Sqrt(fx * fx + fy * fy);
            int cell = Mathf.FloorToInt(gx) * 13 + Mathf.FloorToInt(gy) * 29;
            var r2 = new System.Random(cell * 104729 + 7);
            if (d < 0.08f + 0.06f * (float)r2.NextDouble())
            {
                Color dot = Color.HSVToRGB((cell % 7) / 7f, 0.55f, 0.9f);
                c = Color.Lerp(c, dot, 0.35f);
            }
            // fine grain
            c *= 0.92f + 0.16f * n3.Fbm(u * 30f, v * 30f, 3);
            // vignette
            float ex = Mathf.Min(u, 1f - u), ey = Mathf.Min(v, 1f - v);
            float vig = Mathf.Min(ex, ey);
            c *= 1f - Mathf.Clamp01(0.18f - vig * 2f) * 0.3f;
            return c;
        }

        static float n3Hash(float x, float y)
        {
            float s = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }
    }

    /// <summary>Small seeded Perlin-style value noise with fBm.</summary>
    public class ValueNoise
    {
        readonly int[] perm = new int[512];
        readonly float offsetX, offsetY;

        public ValueNoise(int seed)
        {
            var rng = new System.Random(seed);
            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }
            for (int i = 0; i < 512; i++) perm[i] = p[i & 255];
            offsetX = (float)rng.NextDouble() * 100f;
            offsetY = (float)rng.NextDouble() * 100f;
        }

        float Hash(int x, int y)
        {
            return perm[(perm[x & 255] + y) & 255] / 255f;
        }

        float Fade(float t) => t * t * (3f - 2f * t);

        public float Noise(float x, float y)
        {
            x += offsetX; y += offsetY;
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = Fade(xf), v = Fade(yf);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi);
            float c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        public float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x * freq, y * freq) * amp;
                norm += amp;
                amp *= 0.5f; freq *= 2f;
            }
            return sum / norm;
        }

        /// <summary>1D-ish noise: samples along x with fixed y.</summary>
        public float Fbm1(float x, int octaves) => Fbm(x, 0.37f, octaves);
    }
}
