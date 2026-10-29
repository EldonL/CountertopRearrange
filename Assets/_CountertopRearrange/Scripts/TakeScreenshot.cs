
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
        public void TakePicture()
        {
            StartCoroutine(CaptureScreenshotRoutine());
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
