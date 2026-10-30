using UnityEngine;

namespace CounterTopRearrange
{
    /// <summary>
    /// Draws a simple shape icon (matching the prefab's primitive + tint) for
    /// menu entries whose <see cref="SpawnableObjectSO.menuImage"/> is empty,
    /// so the menu always shows an image per object.
    /// </summary>
    public static class SpawnableFallbackIcon
    {
        /// <summary>Builds an icon from a spawnable prefab's primitive view, or a gray cube when unavailable.</summary>
        public static Sprite FromPrefab(GameObject prefab, int pixels = 128)
        {
            SpawnablePrimitive.Shape shape = SpawnablePrimitive.Shape.Cube;
            Color color = new Color(0.6f, 0.6f, 0.6f, 1f);
            if (prefab != null)
            {
                SpawnablePrimitive view = prefab.GetComponent<SpawnablePrimitive>();
                if (view != null)
                {
                    shape = view.shape;
                    color = view.tint;
                }
            }
            return Build(shape, color, pixels);
        }

        public static Sprite Build(SpawnablePrimitive.Shape shape, Color color, int pixels = 128)
        {
            pixels = Mathf.Clamp(pixels, 16, 512);
            Texture2D texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
            Color[] output = new Color[pixels * pixels];
            Color edge = new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, 1f);
            float outline = 2.5f / pixels;

            for (int y = 0; y < pixels; y++)
            {
                float v = (y + 0.5f) / pixels;
                for (int x = 0; x < pixels; x++)
                {
                    float u = (x + 0.5f) / pixels;
                    Color pixel = new Color(0f, 0f, 0f, 0f);
                    if (IsInside(shape, u, v, 0f))
                    {
                        pixel = color;
                        pixel.a = 1f;
                        if (!IsInside(shape, u, v, outline))
                            pixel = edge;
                        else if (v > 0.62f)
                            pixel = Color.Lerp(pixel, Color.white, 0.22f); // top highlight
                    }
                    output[y * pixels + x] = pixel;
                }
            }

            texture.SetPixels(output);
            texture.Apply();
            texture.hideFlags = HideFlags.DontSave;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, pixels, pixels), new Vector2(0.5f, 0.5f), pixels);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        static bool IsInside(SpawnablePrimitive.Shape shape, float u, float v, float shrink)
        {
            switch (shape)
            {
                case SpawnablePrimitive.Shape.Sphere:
                    return Distance(u - 0.5f, v - 0.48f) < 0.34f - shrink;
                case SpawnablePrimitive.Shape.Cylinder:
                    return Mathf.Abs(u - 0.5f) < 0.26f - shrink && v > 0.2f + shrink && v < 0.68f - shrink * 0.5f;
                case SpawnablePrimitive.Shape.Capsule:
                    return CapsuleDistance(u, v) < 0.16f - shrink;
                case SpawnablePrimitive.Shape.Cone:
                    return InTriangle(u, v, shrink);
                default: // Cube
                    return Mathf.Abs(u - 0.5f) < 0.32f - shrink && Mathf.Abs(v - 0.48f) < 0.3f - shrink;
            }
        }

        static float Distance(float x, float y)
        {
            return Mathf.Sqrt(x * x + y * y);
        }

        static float CapsuleDistance(float u, float v)
        {
            float cy = Mathf.Clamp(v, 0.3f, 0.66f);
            return Distance(u - 0.5f, v - cy);
        }

        static bool InTriangle(float u, float v, float shrink)
        {
            const float apexV = 0.8f;
            const float baseV = 0.2f;
            if (v < baseV + shrink || v > apexV - shrink)
                return false;
            float halfWidth = (apexV - v) / (apexV - baseV) * 0.32f - shrink;
            return Mathf.Abs(u - 0.5f) < halfWidth;
        }
    }
}
