using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CounterTopRearrange
{
    /// <summary>
    /// Full-screen 3-2-1 countdown overlay with a draining circle (ring) timer.
    /// TakeScreenshot plays it after the Take Picture press and captures only
    /// when it reaches zero. Setup (scene mode): wire Overlay, Ring Fill and
    /// Number to the hierarchy objects. When empty, the overlay is built at
    /// runtime instead (or let TakeScreenshot create one on demand).
    /// </summary>
    public class CaptureCountdown : MonoBehaviour
    {
        [Header("Style")]
        [Tooltip("Color of the draining ring.")]
        [SerializeField] Color _ringColor = new Color(0.3f, 0.8f, 1f, 1f);
        [Tooltip("Dim color behind the countdown.")]
        [SerializeField] Color _dimColor = new Color(0f, 0f, 0f, 0.35f);
        [Tooltip("Countdown number size.")]
        [SerializeField] float _numberFontSize = 160f;
        [Tooltip("Ring diameter in pixels.")]
        [SerializeField] float _ringDiameter = 320f;

        [Header("Scene Wiring (optional)")]
        [Tooltip("Overlay root in the hierarchy. When Overlay, Ring Fill and Number are all assigned, they are used instead of runtime-built UI.")]
        [SerializeField] GameObject _overlay;
        [Tooltip("Ring image drained by the countdown.")]
        [SerializeField] Image _ringFill;
        [Tooltip("Countdown number label.")]
        [SerializeField] TextMeshProUGUI _number;
        Coroutine _running;
        Action _onFinished;
        bool _built;

        /// <summary>True while the countdown is visible and ticking.</summary>
        public bool IsPlaying
        {
            get { return _running != null; }
        }

        void Awake()
        {
            EnsureBuilt();
        }

        /// <summary>Shows the countdown for <paramref name="seconds"/> then invokes <paramref name="onFinished"/> once.</summary>
        public void Play(float seconds, Action onFinished)
        {
            if (IsPlaying)
                return;
            EnsureBuilt();
            _onFinished = onFinished;
            _overlay.SetActive(true);
            _running = StartCoroutine(CountdownRoutine(Mathf.Max(0.01f, seconds)));
        }

        /// <summary>Hides the overlay without invoking the pending callback.</summary>
        public void Cancel()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }
            _onFinished = null;
            if (_overlay != null)
                _overlay.SetActive(false);
        }

        IEnumerator CountdownRoutine(float seconds)
        {
            float remaining = seconds;
            while (remaining > 0f)
            {
                ShowFrame(remaining, seconds);
                yield return null;
                remaining -= Time.unscaledDeltaTime;
            }
            ShowFrame(0f, seconds);
            // Let the final "0 / empty ring" frame render before capturing.
            yield return null;

            _running = null;
            if (_overlay != null)
                _overlay.SetActive(false);
            Action finished = _onFinished;
            _onFinished = null;
            if (finished != null)
                finished.Invoke();
        }

        void ShowFrame(float remaining, float total)
        {
            if (_number != null)
                _number.text = Mathf.CeilToInt(Mathf.Max(remaining, 0f)).ToString();
            if (_ringFill != null)
                _ringFill.fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
        }

        void EnsureBuilt()
        {
            if (_built)
                return;
            _built = true;

            // Scene mode: overlay, ring and number already exist in the
            // hierarchy. Enforce the radial-fill setup in code so a plain
            // Image in the scene still becomes a draining circle timer.
            if (_overlay != null && _ringFill != null && _number != null)
            {
                _ringFill.type = Image.Type.Filled;
                _ringFill.fillMethod = Image.FillMethod.Radial360;
                _ringFill.fillOrigin = (int)Image.Origin360.Top;
                _ringFill.fillClockwise = true;
                _ringFill.fillAmount = 1f;
                _overlay.SetActive(false);
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGo = new GameObject("CountdownCanvas", typeof(RectTransform));
                canvasGo.transform.SetParent(transform, false);
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            _overlay = new GameObject("CountdownOverlay", typeof(RectTransform));
            _overlay.transform.SetParent(canvas.transform, false);
            RectTransform overlayRect = (RectTransform)_overlay.transform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image dim = _overlay.AddComponent<Image>();
            dim.sprite = CountertopUi.WhiteSprite();
            dim.color = _dimColor;
            dim.raycastTarget = true; // swallow taps while counting down

            GameObject center = new GameObject("Center", typeof(RectTransform));
            center.transform.SetParent(_overlay.transform, false);
            RectTransform centerRect = (RectTransform)center.transform;
            centerRect.anchorMin = new Vector2(0.5f, 0.5f);
            centerRect.anchorMax = new Vector2(0.5f, 0.5f);
            centerRect.sizeDelta = new Vector2(_ringDiameter, _ringDiameter);

            Sprite ring = BuildRingSprite();
            Image track = new GameObject("RingTrack", typeof(RectTransform)).AddComponent<Image>();
            track.transform.SetParent(center.transform, false);
            StretchFull((RectTransform)track.transform);
            track.sprite = ring;
            track.color = new Color(1f, 1f, 1f, 0.25f);
            track.raycastTarget = false;

            _ringFill = new GameObject("RingFill", typeof(RectTransform)).AddComponent<Image>();
            _ringFill.transform.SetParent(center.transform, false);
            StretchFull((RectTransform)_ringFill.transform);
            _ringFill.sprite = ring;
            _ringFill.color = _ringColor;
            _ringFill.type = Image.Type.Filled;
            _ringFill.fillMethod = Image.FillMethod.Radial360;
            _ringFill.fillOrigin = (int)Image.Origin360.Top;
            _ringFill.fillClockwise = true;
            _ringFill.fillAmount = 1f;
            _ringFill.raycastTarget = false;

            _number = CountertopUi.MakeLabel(center.transform, "Number", _numberFontSize, Color.white);
            StretchFull((RectTransform)_number.transform);
            _number.alignment = TextAlignmentOptions.Center;
            _number.fontStyle = FontStyles.Bold;

            _overlay.SetActive(false);
        }

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static Sprite _ringSprite;

        static Sprite BuildRingSprite()
        {
            if (_ringSprite != null)
                return _ringSprite;
            const int px = 256;
            Texture2D texture = new Texture2D(px, px, TextureFormat.RGBA32, false);
            Color[] output = new Color[px * px];
            for (int y = 0; y < px; y++)
            {
                float v = ((y + 0.5f) / px) * 2f - 1f;
                for (int x = 0; x < px; x++)
                {
                    float u = ((x + 0.5f) / px) * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float alpha = 0f;
                    if (r < 1f && r > 0.72f)
                        alpha = Mathf.Clamp01(Mathf.Min(1f - r, r - 0.72f) * px * 0.25f);
                    output[y * px + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(output);
            texture.Apply();
            texture.hideFlags = HideFlags.DontSave;
            _ringSprite = Sprite.Create(texture, new Rect(0f, 0f, px, px), new Vector2(0.5f, 0.5f), px);
            _ringSprite.hideFlags = HideFlags.DontSave;
            return _ringSprite;
        }
    }
}
