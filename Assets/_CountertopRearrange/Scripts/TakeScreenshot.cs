
using System.Collections;
using System.IO;
using UnityEngine;
using TMPro;
namespace CounterTopRearrange
{
    public class TakeScreenshot : MonoBehaviour
    {
        [SerializeField]
        private SendReceiveStringEventSO _onSendTakePictureImagePath;
        [SerializeField]  public TextMeshProUGUI _displayText; 
        [Header("Spawnable Object Flow")]
        [Tooltip("Countdown shown after the Take Picture press before capturing. Created automatically when missing.")]
        [SerializeField] private CaptureCountdown _countdown;
        [Tooltip("Countdown length in seconds. Set to 0 to capture immediately (legacy behavior).")]
        [SerializeField] private float _countdownSeconds = 3f;
        [Tooltip("Vision target retargeted at the selected object. Found automatically when empty.")]
        [SerializeField] private MuseSparkVision _vision;
        [Tooltip("Placement controller receiving the selected prefab. Found automatically when empty.")]
        [SerializeField] private ObjectPlacementController _placement;

        public void TakePicture()
        {
            if (_countdown != null && _countdown.IsPlaying)
                return;
            ApplySelection();
            CaptureCountdown countdown = _countdown;
            if (countdown == null)
                countdown = FindAnyObjectByType<CaptureCountdown>();
            if (countdown == null)
                countdown = new GameObject("CaptureCountdown").AddComponent<CaptureCountdown>();
            _countdown = countdown;
            if (_countdownSeconds > 0f)
                countdown.Play(_countdownSeconds, BeginCapture);
            else
                BeginCapture();
        }

        private void BeginCapture()
        {
            StartCoroutine(CaptureScreenshotRoutine());
        }

        private void ApplySelection()
        {
            SpawnableObjectSO selected = SpawnableObjectSelection.Current;
            if (selected == null)
                return;
            if (_vision == null)
                _vision = FindAnyObjectByType<MuseSparkVision>();
            if (_vision != null)
                _vision.SetTargetObjectName(selected.displayName);
            if (_placement == null)
                _placement = FindAnyObjectByType<ObjectPlacementController>();
            if (_placement != null)
                _placement.SetSpawnableObject(selected);
        }

        private IEnumerator CaptureScreenshotRoutine()
        {
            string fileName = "screenshot.png";
            string fullPath = Path.Combine(
                Application.persistentDataPath,
                fileName
            );

            #if UNITY_EDITOR
                ScreenCapture.CaptureScreenshot(fullPath);
            #else
                ScreenCapture.CaptureScreenshot(fileName);
            #endif

            // Wait for the screenshot to be written.
            float timeout = 20f;
            float elapsed = 0f;
            _displayText.text = "Picture taken";
            while (elapsed < timeout)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                _displayText.text = "saving picture...";
                if (File.Exists(fullPath))
                {
                    // Give the filesystem time to finish writing.
                    _displayText.text = "picture found";
                    yield return new WaitForSecondsRealtime(0.2f);

                    byte[] bytes = null;

                    try
                    {
                        bytes = File.ReadAllBytes(fullPath);
                    _displayText.text = "reading picture info";
                    }
                    catch (IOException)
                    {
                        // File may still be locked or being written.
                        _displayText.text ="picture failed to save";
                    }

                    if (bytes != null && bytes.Length > 0)
                    {
                        Debug.Log(
                            $"Screenshot ready: {fullPath}, {bytes.Length} bytes"
                        );
                        _displayText.text ="Screenshot ready";
                        _onSendTakePictureImagePath.RaiseEvent(fullPath);
                        yield break;
                    }
                }
            }

            Debug.LogError(
                $"Screenshot was not saved successfully: {fullPath}"
            );
            _displayText.text ="Screenshot was not saved successfully: {fullPath}";
        }
    }
}
