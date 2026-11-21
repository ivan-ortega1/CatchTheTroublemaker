using System.Collections.Generic;
using UnityEngine;

namespace CatchTheTroublemaker
{
    /// <summary>
    /// All art is generated procedurally at runtime: no external sprites or
    /// image imports are needed, so the project stays self-contained.
    /// </summary>
    public static class CTTDraw
    {
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        public static Sprite Coin()
        {
            if (Sprites.ContainsKey("coin")) return Sprites["coin"];
            var tex = NewTexture(48, 48);
            var px = new Color32[48 * 48];
            FillCircle(px, 48, 48, 24, 24, 17, CTTConfig.Parse("#a86b00"));
            FillCircle(px, 48, 48, 24, 24, 15, CTTConfig.Parse("#ffd166"));
            FillCircle(px, 48, 48, 19, 19, 4, new Color(1f, 1f, 1f, 0.9f));
            return Store("coin", tex, px);
        }

        public static Sprite Wall()
        {
            if (Sprites.ContainsKey("wall")) return Sprites["wall"];
            var tex = NewTexture(32, 32);
            var px = new Color32[32 * 32];
            FillRoundedRect(px, 32, 32, 1, 1, 30, 30, 7, CTTConfig.Parse("#334275"));
            FillRoundedRect(px, 32, 32, 3, 3, 26, 26, 5, CTTConfig.Parse("#2b3768"));
            return Store("wall", tex, px);
        }

        public static Sprite Shape(string shape)
        {
            string key = "shape-" + shape;
            if (Sprites.ContainsKey(key)) return Sprites[key];
            const int size = 48;
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            Color32 white = Color.white;

            if (shape == "square")
            {
                FillRoundedRect(px, size, size, 3, 3, 42, 42, 9, white);
            }
            else if (shape == "triangle")
            {
                FillPolygon(px, size, size, new[]
                {
                    new Vector2(24, 43), new Vector2(7, 10), new Vector2(41, 10),
                }, white);
            }
            else // star
            {
                var points = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                    float r = i % 2 == 0 ? 20f : 9f;
                    points[i] = new Vector2(24f + Mathf.Cos(a) * r, 24f + Mathf.Sin(a) * r);
                }
                FillPolygon(px, size, size, points, white);
            }
            return Store(key, tex, px);
        }

        public static Texture2D BuildMap(CTTMap map, bool highContrast)
        {
            const int cell = 16;
            int w = map.cols * cell;
            int h = map.rows * cell;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[w * h];
            Color32 floor = highContrast ? new Color32(0, 0, 0, 255) : new Color32(18, 23, 55, 255);
            Color32 dot = highContrast ? new Color32(120, 120, 120, 255) : new Color32(45, 55, 100, 255);
            Color32 wall = highContrast ? new Color32(255, 255, 255, 255) : new Color32(43, 55, 104, 255);
            for (int i = 0; i < px.Length; i++) px[i] = floor;

            for (int r = 0; r < map.rows; r++)
            {
                for (int c = 0; c < map.cols; c++)
                {
                    int y = (map.rows - 1 - r) * cell;
                    int x = c * cell;
                    FillCircle(px, w, h, x + cell / 2, y + cell / 2, 1, dot);
                    if (r * map.cols + c < map.tiles.Length && map.tiles[r * map.cols + c] == '1')
                    {
                        FillRoundedRect(px, w, h, x + 1, y + 1, cell - 2, cell - 2, 4, wall);
                    }
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D NewTexture(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

        private static Sprite Store(string key, Texture2D tex, Color32[] px)
        {
            tex.SetPixels32(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            Sprites[key] = sprite;
            return sprite;
        }

        private static void Set(Color32[] px, int w, int h, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            px[y * w + x] = color;
        }

        private static void FillCircle(Color32[] px, int w, int h, int cx, int cy, float radius, Color32 color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius));
            int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(cx + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius));
            int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(cy + radius));
            float r2 = radius * radius;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    if (dx * dx + dy * dy <= r2) Set(px, w, h, x, y, color);
                }
            }
        }

        private static void FillRoundedRect(Color32[] px, int w, int h, int x0, int y0, int rw, int rh, float radius, Color32 color)
        {
            for (int y = y0; y < y0 + rh; y++)
            {
                for (int x = x0; x < x0 + rw; x++)
                {
                    float nx = Mathf.Clamp(x, x0 + radius, x0 + rw - radius);
                    float ny = Mathf.Clamp(y, y0 + radius, y0 + rh - radius);
                    float dx = x - nx;
                    float dy = y - ny;
                    if (dx * dx + dy * dy <= radius * radius) Set(px, w, h, x, y, color);
                }
            }
        }

        private static void FillPolygon(Color32[] px, int w, int h, Vector2[] points, Color32 color)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool inside = false;
                    for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                    {
                        if ((points[i].y > y) != (points[j].y > y) &&
                            x < (points[j].x - points[i].x) * (y - points[i].y) /
                                (points[j].y - points[i].y) + points[i].x)
                        {
                            inside = !inside;
                        }
                    }
                    if (inside) Set(px, w, h, x, y, color);
                }
            }
        }
    }
}
