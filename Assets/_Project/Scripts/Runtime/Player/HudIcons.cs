using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lockdown.Player
{
    internal enum HudIcon { Fist, Pistol, Sledgehammer, Infinity }

    /// <summary>
    /// Bakes the weapon panel's icons from simple shapes on a 48-unit grid (y down), as white
    /// shapes on transparency, so IMGUI can tint them. No image assets to import or lose.
    /// </summary>
    internal static class HudIcons
    {
        private const int Size = 128;
        private const float Grid = 48f;
        private static readonly Dictionary<HudIcon, Texture2D> Baked = new Dictionary<HudIcon, Texture2D>();

        private enum Op { Fill, Stroke, Erase }

        private readonly struct Layer
        {
            public readonly Func<Vector2, float> Distance;
            public readonly Op Op;
            public readonly float HalfWidth;
            public Layer(Func<Vector2, float> distance, Op op, float halfWidth = 0f)
            { Distance = distance; Op = op; HalfWidth = halfWidth; }
        }

        public static Texture2D Get(HudIcon icon)
        {
            if (Baked.TryGetValue(icon, out var texture) && texture != null) return texture;
            texture = Bake(Layers(icon));
            Baked[icon] = texture;
            return texture;
        }

        private static List<Layer> Layers(HudIcon icon)
        {
            var layers = new List<Layer>();
            switch (icon)
            {
                case HudIcon.Fist:
                    // Wrist, back of the hand, four knuckles, then the thumb across them; each part is
                    // outlined by a thin cut so the fingers read separately.
                    foreach (var part in new[]
                    {
                        RoundedRect(16f, 35f, 16f, 9f, 2f), RoundedRect(11f, 17f, 27f, 21f, 7f),
                        RoundedRect(11f, 9f, 7.5f, 13f, 3.75f), RoundedRect(18f, 8f, 7.5f, 14f, 3.75f),
                        RoundedRect(25f, 8.5f, 7.5f, 13.5f, 3.75f), RoundedRect(32f, 10f, 7f, 12f, 3.5f),
                        RoundedRect(9f, 22f, 19f, 7f, 3.5f),
                    })
                    {
                        layers.Add(new Layer(part, Op.Fill));
                        layers.Add(new Layer(p => Mathf.Abs(part(p)), Op.Erase, 0.8f));
                    }
                    break;

                case HudIcon.Pistol:
                    layers.Add(new Layer(RoundedRect(7f, 10.5f, 3f, 2.5f, 0f), Op.Fill));
                    layers.Add(new Layer(RoundedRect(39.5f, 10.5f, 2f, 2.5f, 0f), Op.Fill));
                    layers.Add(new Layer(RoundedRect(5f, 13f, 38f, 8f, 1.5f), Op.Fill));
                    layers.Add(new Layer(Polygon(new Vector2(12f, 20f), new Vector2(30f, 20f), new Vector2(30f, 24f),
                        new Vector2(21f, 24f), new Vector2(18.5f, 39f), new Vector2(10f, 39f), new Vector2(12f, 24f)), Op.Fill));
                    layers.Add(new Layer(RoundedRect(23.5f, 24f, 2f, 3.5f, 1f), Op.Fill));
                    layers.Add(new Layer(Polyline(Cubic(new Vector2(21f, 24f), new Vector2(21f, 30f),
                        new Vector2(30f, 30f), new Vector2(30f, 24f))), Op.Stroke, 1.2f));
                    foreach (float x in new[] { 9f, 11f, 13f })
                        layers.Add(new Layer(Polyline(new List<Vector2> { new Vector2(x, 15f), new Vector2(x, 19f) }), Op.Erase, 0.5f));
                    break;

                case HudIcon.Sledgehammer:
                    var pivot = new Vector2(24f, 26f);
                    layers.Add(new Layer(Rotated(RoundedRect(21.5f, 20f, 5f, 25f, 2f), -25f, pivot), Op.Fill));
                    layers.Add(new Layer(Rotated(RoundedRect(7f, 8f, 34f, 13f, 2.5f), -25f, pivot), Op.Fill));
                    foreach (float x in new[] { 12.5f, 35.5f })
                        layers.Add(new Layer(Rotated(Polyline(new List<Vector2> { new Vector2(x, 8f), new Vector2(x, 21f) }), -25f, pivot),
                            Op.Erase, 0.8f));
                    break;

                case HudIcon.Infinity:
                    var loop = new List<Vector2>();
                    var c = new[]
                    {
                        new Vector2(24f, 24f), new Vector2(21f, 19f), new Vector2(17f, 17.5f), new Vector2(14f, 17.5f),
                        new Vector2(10f, 17.5f), new Vector2(8f, 20.5f), new Vector2(8f, 24f),
                        new Vector2(8f, 27.5f), new Vector2(10f, 30.5f), new Vector2(14f, 30.5f),
                        new Vector2(17f, 30.5f), new Vector2(21f, 29f), new Vector2(24f, 24f),
                        new Vector2(27f, 19f), new Vector2(31f, 17.5f), new Vector2(34f, 17.5f),
                        new Vector2(38f, 17.5f), new Vector2(40f, 20.5f), new Vector2(40f, 24f),
                        new Vector2(40f, 27.5f), new Vector2(38f, 30.5f), new Vector2(34f, 30.5f),
                        new Vector2(31f, 30.5f), new Vector2(27f, 29f), new Vector2(24f, 24f),
                    };
                    for (int i = 0; i + 3 < c.Length; i += 3) loop.AddRange(Cubic(c[i], c[i + 1], c[i + 2], c[i + 3]));
                    layers.Add(new Layer(Polyline(loop), Op.Stroke, 1.1f));
                    break;
            }
            return layers;
        }

        private static Texture2D Bake(List<Layer> layers)
        {
            float unit = Grid / Size;
            var pixels = new Color32[Size * Size];
            for (int row = 0; row < Size; row++)
            for (int column = 0; column < Size; column++)
            {
                // Texture rows run bottom-up; the grid runs top-down like the SVG mockups.
                var p = new Vector2((column + 0.5f) * unit, (Size - row - 0.5f) * unit);
                float alpha = 0f;
                foreach (var layer in layers)
                {
                    float d = layer.Distance(p) - layer.HalfWidth;
                    float coverage = Mathf.Clamp01(0.5f - d / unit);
                    alpha = layer.Op == Op.Erase ? Mathf.Lerp(alpha, 0f, coverage) : Mathf.Lerp(alpha, 1f, coverage);
                }
                pixels[row * Size + column] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Func<Vector2, float> RoundedRect(float x, float y, float width, float height, float radius)
        {
            var centre = new Vector2(x + width * 0.5f, y + height * 0.5f);
            var half = new Vector2(width * 0.5f, height * 0.5f);
            return p =>
            {
                var q = new Vector2(Mathf.Abs(p.x - centre.x), Mathf.Abs(p.y - centre.y)) - half + Vector2.one * radius;
                return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
            };
        }

        // Signed distance to a closed polygon (negative inside).
        private static Func<Vector2, float> Polygon(params Vector2[] v)
        {
            return p =>
            {
                float d = Vector2.Dot(p - v[0], p - v[0]), sign = 1f;
                for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
                {
                    Vector2 e = v[j] - v[i], w = p - v[i];
                    Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                    d = Mathf.Min(d, Vector2.Dot(b, b));
                    bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) sign = -sign;
                }
                return sign * Mathf.Sqrt(d);
            };
        }

        // Unsigned distance to an open polyline, for strokes and cut lines.
        private static Func<Vector2, float> Polyline(List<Vector2> points)
        {
            return p =>
            {
                float best = float.MaxValue;
                for (int i = 0; i + 1 < points.Count; i++)
                {
                    Vector2 a = points[i], e = points[i + 1] - a;
                    float t = e.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, e) / e.sqrMagnitude) : 0f;
                    best = Mathf.Min(best, (p - (a + e * t)).magnitude);
                }
                return best;
            };
        }

        private static List<Vector2> Cubic(Vector2 a, Vector2 b, Vector2 c, Vector2 d, int steps = 24)
        {
            var points = new List<Vector2>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps, u = 1f - t;
                points.Add(u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d);
            }
            return points;
        }

        // Rotates a shape by degrees about a pivot, matching SVG's rotate(angle cx cy).
        private static Func<Vector2, float> Rotated(Func<Vector2, float> shape, float degrees, Vector2 pivot)
        {
            float radians = -degrees * Mathf.Deg2Rad, cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            return p =>
            {
                Vector2 r = p - pivot;
                return shape(new Vector2(r.x * cos - r.y * sin, r.x * sin + r.y * cos) + pivot);
            };
        }
    }
}
