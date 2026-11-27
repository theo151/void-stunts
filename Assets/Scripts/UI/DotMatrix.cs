using System.Collections.Generic;
using UnityEngine;

namespace VoidStunts
{
    /// <summary>The game's own 5×7 dot face (from v0.x), rasterised into textures for UI numbers, titles and street signs.</summary>
    public static class DotMatrix
    {
        static readonly Dictionary<char, int[]> F = new()
        {
            ['0'] = new[] { 14, 17, 19, 21, 25, 17, 14 }, ['1'] = new[] { 4, 12, 4, 4, 4, 4, 14 }, ['2'] = new[] { 14, 17, 1, 2, 4, 8, 31 }, ['3'] = new[] { 31, 2, 4, 2, 1, 17, 14 }, ['4'] = new[] { 2, 6, 10, 18, 31, 2, 2 },
            ['5'] = new[] { 31, 16, 30, 1, 1, 17, 14 }, ['6'] = new[] { 6, 8, 16, 30, 17, 17, 14 }, ['7'] = new[] { 31, 1, 2, 4, 8, 8, 8 }, ['8'] = new[] { 14, 17, 17, 14, 17, 17, 14 }, ['9'] = new[] { 14, 17, 17, 15, 1, 2, 12 },
            ['A'] = new[] { 14, 17, 17, 31, 17, 17, 17 }, ['B'] = new[] { 30, 17, 17, 30, 17, 17, 30 }, ['C'] = new[] { 14, 17, 16, 16, 16, 17, 14 }, ['D'] = new[] { 28, 18, 17, 17, 17, 18, 28 }, ['E'] = new[] { 31, 16, 16, 30, 16, 16, 31 },
            ['F'] = new[] { 31, 16, 16, 30, 16, 16, 16 }, ['G'] = new[] { 14, 17, 16, 23, 17, 17, 15 }, ['H'] = new[] { 17, 17, 17, 31, 17, 17, 17 }, ['I'] = new[] { 14, 4, 4, 4, 4, 4, 14 }, ['J'] = new[] { 7, 2, 2, 2, 2, 18, 12 },
            ['K'] = new[] { 17, 18, 20, 24, 20, 18, 17 }, ['L'] = new[] { 16, 16, 16, 16, 16, 16, 31 }, ['M'] = new[] { 17, 27, 21, 21, 17, 17, 17 }, ['N'] = new[] { 17, 17, 25, 21, 19, 17, 17 }, ['O'] = new[] { 14, 17, 17, 17, 17, 17, 14 },
            ['P'] = new[] { 30, 17, 17, 30, 16, 16, 16 }, ['Q'] = new[] { 14, 17, 17, 17, 21, 18, 13 }, ['R'] = new[] { 30, 17, 17, 30, 20, 18, 17 }, ['S'] = new[] { 15, 16, 16, 14, 1, 1, 30 }, ['T'] = new[] { 31, 4, 4, 4, 4, 4, 4 },
            ['U'] = new[] { 17, 17, 17, 17, 17, 17, 14 }, ['V'] = new[] { 17, 17, 17, 17, 17, 10, 4 }, ['W'] = new[] { 17, 17, 17, 21, 21, 21, 10 }, ['X'] = new[] { 17, 17, 10, 4, 10, 17, 17 }, ['Y'] = new[] { 17, 17, 17, 10, 4, 4, 4 },
            ['Z'] = new[] { 31, 1, 2, 4, 8, 16, 31 }, [':'] = new[] { 0, 12, 12, 0, 12, 12, 0 }, ['.'] = new[] { 0, 0, 0, 0, 0, 12, 12 }, [','] = new[] { 0, 0, 0, 0, 12, 4, 8 }, ['+'] = new[] { 0, 4, 4, 31, 4, 4, 0 },
            ['-'] = new[] { 0, 0, 0, 31, 0, 0, 0 }, ['×'] = new[] { 0, 17, 10, 4, 10, 17, 0 }, ['·'] = new[] { 0, 0, 0, 4, 0, 0, 0 }, ['/'] = new[] { 1, 1, 2, 4, 8, 16, 16 }, ['∞'] = new[] { 0, 0, 10, 21, 10, 0, 0 }, [' '] = new[] { 0, 0, 0, 0, 0, 0, 0 },
        };

        public static bool Lit(char ch, int row, int col) { if (!F.TryGetValue(char.ToUpperInvariant(ch), out var r)) r = F[' ']; return ((r[row] >> (4 - col)) & 1) == 1; }

        /// <summary>Renders text as round dots; unlit dots faint when <paramref name="grid"/>. Vertical stacks letters top to bottom.</summary>
        public static Texture2D Render(string text, int dot, Color fg, Color bg, bool grid = false, bool vertical = false, int pad = 0)
        {
            text = text.ToUpperInvariant();
            int cw = 6 * dot, ch = 8 * dot, n = Mathf.Max(1, text.Length);
            int W = vertical ? 5 * dot + pad * 2 : n * cw - dot + pad * 2, H = vertical ? n * ch - dot + pad * 2 : 7 * dot + pad * 2;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H]; Color32 b = bg; for (int i = 0; i < px.Length; i++) px[i] = b;
            float r = dot * .4f;
            for (int i = 0; i < text.Length; i++)
                for (int y = 0; y < 7; y++) for (int x = 0; x < 5; x++)
                {
                    bool on = Lit(text[i], y, x); if (!on && !grid) continue;
                    float cx = pad + (vertical ? 0 : i * cw) + x * dot + dot * .5f, cy = pad + (vertical ? i * ch : 0) + y * dot + dot * .5f;
                    var col = fg; if (!on) col.a *= .07f;
                    for (int yy = (int)(cy - r - 1); yy <= (int)(cy + r + 1); yy++) for (int xx = (int)(cx - r - 1); xx <= (int)(cx + r + 1); xx++)
                    {
                        if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                        float d = Mathf.Sqrt((xx + .5f - cx) * (xx + .5f - cx) + (yy + .5f - cy) * (yy + .5f - cy)), a = Mathf.Clamp01(r - d + .5f) * col.a;
                        if (a <= 0) continue;
                        int idx = (H - 1 - yy) * W + xx; Color dst = px[idx];
                        px[idx] = Color.Lerp(dst, new Color(col.r, col.g, col.b, Mathf.Max(dst.a, a)), a);
                    }
                }
            tex.SetPixels32(px); tex.Apply(false); return tex;
        }
    }
}
