using TMPro;
using UnityEngine;

namespace CounterTopRearrange
{
    /// <summary>
    /// Small runtime-UI helpers shared by the spawn menu and the capture
    /// countdown so both can build their interface in code without extra
    /// scene wiring or imported sprites.
    /// </summary>
    public static class CountertopUi
    {
        static Sprite _whiteSprite;
        static TMP_FontAsset _font;

        /// <summary>Plain white sprite used for panel/button backgrounds.</summary>
        public static Sprite WhiteSprite()
        {
            if (_whiteSprite == null)
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                texture.Apply();
                texture.hideFlags = HideFlags.DontSave;
                _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
                _whiteSprite.hideFlags = HideFlags.DontSave;
            }
            return _whiteSprite;
        }

        /// <summary>Best available TMP font: project default, TMP package default, then any scene label.</summary>
        public static TMP_FontAsset ResolveFont()
        {
            if (_font == null)
            {
                try
                {
                    _font = TMP_Settings.defaultFontAsset;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("CountertopUi: TMP settings unavailable: " + e.Message);
                }
            }
            if (_font == null)
                _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (_font == null)
            {
                TextMeshProUGUI anyLabel = Object.FindAnyObjectByType<TextMeshProUGUI>();
                if (anyLabel != null)
                    _font = anyLabel.font;
            }
            return _font;
        }

        public static TextMeshProUGUI MakeLabel(Transform parent, string name, float fontSize, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.font = ResolveFont();
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        public static RectTransform FullStretch(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
