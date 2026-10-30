using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.InputSystem;
using TMPro;
/// <summary>
/// Sends an image plus a question to Muse Spark (Meta Model API, Responses API)
/// and surfaces the text answer.
///
/// Flow (same as the curl calls): the PNG bytes are uploaded to POST /v1/files
/// with purpose "user_data", the returned file_id is referenced from an
/// input_image block, and POST /v1/responses runs non-streaming.
///
/// Setup: attach to a GameObject, assign Image (or Image Path), make sure the
/// MODEL_API_KEY environment variable is visible to Unity, press Play, then
/// call Ask() - e.g. from a UI Button OnClick.
/// </summary>
public class MuseSparkVision : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Image to ask about. Must be marked Read/Write Enabled to encode.")]
    public Texture2D image;
    [Tooltip("Used only when Image (above) is empty.")]
    public string imagePath;
    [TextArea(2, 4)]
    public string question = "You are placing a book on the countertop in this photo. The photo may contain overlaid UI text; ignore it. Reply with exactly one JSON object and nothing else - {\"x\": <0-1>, \"y\": <0-1>, \"reason\": \"<short reason>\"}. x runs left-to-right, y runs top-to-bottom, pointing at a clear flat spot on the counter where a book fits.";

    [Header("Model (Meta Model API)")]
    public string model = "muse-spark-1.3";
    public string reasoningEffort = "medium";
    public int maxOutputTokens = 4096;
    public float temperature = 1f;
    [Tooltip("Only for builds where MODEL_API_KEY is unavailable. Never commit a real key.")]
    public string apiKeyOverride;
    private string apiKeyFilePath = @"D:\modalapikey_metamuse.txt";

    [Header("Output (read-only)")]
    [TextArea(4, 12)]
    public string lastAnswer;
    public UnityEvent<string> onAnswer;

    const string BaseUrl = "https://api.meta.ai/v1";

    [SerializeField]  public TextMeshProUGUI _displayText; 

    /// <summary>UI-Button-compatible entry point. Must be called in Play mode.</summary>


    public void SetImage(string imagePath)
    {
        this.imagePath = imagePath;
        Ask();
    }

    /// <summary>
    /// Retargets the vision question at the selected spawnable object.
    /// Blank names keep the legacy book question. Called by TakeScreenshot
    /// before the capture reaches Ask(); API-key handling is untouched.
    /// </summary>
    public void SetTargetObjectName(string displayName)
    {
        question = CounterTopRearrange.SpawnablePrompt.BuildQuestion(displayName);
    }

    [ContextMenu("Ask Muse Spark")]
    public void Ask()
    {
        StartCoroutine(AskRoutine());
    }

    IEnumerator AskRoutine()
    {
        string apiKey = !string.IsNullOrEmpty(apiKeyOverride)
            ? apiKeyOverride
            : ReadApiKeyFromFile();//Environment.GetEnvironmentVariable("MODEL_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("MuseSparkVision: MODEL_API_KEY is not set. Launch Unity from a console where it is set, or fill Api Key Override for local testing.");
             _displayText.text = "MuseSparkVision: MODEL_API_KEY is not set.";
            yield break;
        }

        if (!TryGetPngBytes(out byte[] png, out string fileName))
        {
             _displayText.text = "error with getting png";
            yield break;
        }

        string fileId = null;
        _displayText.text = "uploading file...";
        yield return UploadFile(png, fileName, apiKey, id => fileId = id);
        if (string.IsNullOrEmpty(fileId))
        {
            _displayText.text = "error: fileId isNullOrEmpty";
            yield break; // Error already logged.
        }


        string answer = null;
         _displayText.text =  "loading answer...";
        yield return SendResponsesRequest(fileId, apiKey, text => answer = text);
        if (answer == null)
        {
            _displayText.text = "error with answer";
            yield break; // Error already logged.
        }


        lastAnswer = answer;
        Debug.Log("Muse Spark: " + answer);
        _displayText.text = "Muse Spark:" + answer;
        onAnswer?.Invoke(answer);
    }

    // bool TryGetPngBytes(out byte[] png, out string fileName)
    // {
    //     png = null;
    //     fileName = "image.png";
    //     if (image != null)
    //     {
    //         try
    //         {
    //             png = image.EncodeToPNG();
    //         }
    //         catch (Exception e)
    //         {
    //             Debug.LogError("MuseSparkVision: cannot encode Image (enable Read/Write in its import settings). " + e.Message);
    //             return false;
    //         }
    //         if (!string.IsNullOrEmpty(image.name))
    //             fileName = image.name + ".png";
    //         return true;
    //     }
    //     if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
    //     {
    //         png = File.ReadAllBytes(imagePath);
    //         fileName = Path.GetFileName(imagePath);
    //         return true;
    //     }
    //     Debug.LogError("MuseSparkVision: assign Image or a valid Image Path.");
    //     return false;
    // }
bool TryGetPngBytes(out byte[] png, out string fileName)
{
    png = null;
    fileName = "image.png";

    if (image != null)
    {
        Texture2D readableCopy = null;
        RenderTexture renderTexture = null;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            // Create a temporary render texture.
            renderTexture = RenderTexture.GetTemporary(
                image.width,
                image.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default
            );

            // Copy the source texture to the render texture.
            Graphics.Blit(image, renderTexture);

            // Make the render texture active for reading.
            RenderTexture.active = renderTexture;

            // Create an uncompressed, readable Texture2D.
            readableCopy = new Texture2D(
                image.width,
                image.height,
                TextureFormat.RGBA32,
                false
            );

            // Copy the pixels into the new Texture2D.
            readableCopy.ReadPixels(
                new Rect(0, 0, image.width, image.height),
                0,
                0
            );

            readableCopy.Apply();

            // Encode the uncompressed copy as PNG.
            png = readableCopy.EncodeToPNG();

            if (png == null || png.Length == 0)
            {
                Debug.LogError("MuseSparkVision: PNG encoding returned no data.");
                png = null;
                return false;
            }

            if (!string.IsNullOrEmpty(image.name))
                fileName = image.name + ".png";

            Debug.Log($"MuseSparkVision: PNG encoded successfully ({png.Length} bytes).");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(
                "MuseSparkVision: Failed to convert texture to PNG. " + e
            );

            png = null;
            return false;
        }
        finally
        {
            // Restore the previous active render texture.
            RenderTexture.active = previousActive;

            // Release temporary resources.
            if (renderTexture != null)
                RenderTexture.ReleaseTemporary(renderTexture);

            if (readableCopy != null)
            {
                if (Application.isPlaying)
                    Destroy(readableCopy);
                else
                    DestroyImmediate(readableCopy);
            }
        }
    }

    // If no Texture2D is assigned, try the image path.
    if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
    {
        png = File.ReadAllBytes(imagePath);
        fileName = Path.GetFileName(imagePath);
        return true;
    }

    Debug.LogError("MuseSparkVision: Assign Image or a valid Image Path.");
    return false;
}

    IEnumerator UploadFile(byte[] png, string fileName, string apiKey, Action<string> done)
    {
        var form = new WWWForm();
        form.AddField("purpose", "user_data");
        form.AddBinaryData("file", png, fileName, "image/png");
        using (var req = UnityWebRequest.Post(BaseUrl + "/files", form))
        {
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"MuseSparkVision: file upload failed (HTTP {req.responseCode}). {req.error}\n{req.downloadHandler.text}");
                done(null);
                yield break;
            }
            var parsed = JsonUtility.FromJson<FileUploadResponse>(req.downloadHandler.text);
            if (parsed == null || string.IsNullOrEmpty(parsed.id))
            {
                Debug.LogError("MuseSparkVision: upload response had no file id.\n" + req.downloadHandler.text);
                done(null);
                yield break;
            }
            done(parsed.id);
        }
    }

    IEnumerator SendResponsesRequest(string fileId, string apiKey, Action<string> done)
    {
        byte[] body = Encoding.UTF8.GetBytes(BuildResponsesJson(fileId));
        using (var req = new UnityWebRequest(BaseUrl + "/responses", UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");
            yield return req.SendWebRequest();
            string text = req.downloadHandler != null ? req.downloadHandler.text : "";
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"MuseSparkVision: responses request failed (HTTP {req.responseCode}). {req.error}\n{text}");
                done(null);
                yield break;
            }
            var apiError = JsonUtility.FromJson<ErrorResponse>(text);
            if (apiError != null && apiError.error != null && !string.IsNullOrEmpty(apiError.error.message))
            {
                Debug.LogError("MuseSparkVision: API error: " + apiError.error.message);
                done(null);
                yield break;
            }
            string answer = ExtractText(JsonUtility.FromJson<ResponsesResponse>(text));
            if (answer == null)
            {
                Debug.LogError("MuseSparkVision: no output_text in response.\n" + text);
                done(null);
                yield break;
            }
            done(answer);
        }
    }

    string BuildResponsesJson(string fileId)
    {
        var sb = new StringBuilder(1024);
        sb.Append("{\"model\":\"");
        sb.Append(Escape(model));
        sb.Append("\",\"input\":[{\"role\":\"user\",\"content\":[");
        sb.Append("{\"type\":\"input_text\",\"text\":\"");
        sb.Append(Escape(question));
        sb.Append("\"},{\"type\":\"input_image\",\"file_id\":\"");
        sb.Append(Escape(fileId));
        sb.Append("\"}]}],\"stream\":false,\"temperature\":");
        sb.Append(temperature.ToString("G", CultureInfo.InvariantCulture));
        sb.Append(",\"max_output_tokens\":");
        sb.Append(maxOutputTokens);
        sb.Append(",\"top_p\":1,\"reasoning\":{\"effort\":\"");
        sb.Append(Escape(reasoningEffort));
        sb.Append("\"}}");
        return sb.ToString();
    }

    static string ExtractText(ResponsesResponse response)
    {
        if (response == null || response.output == null)
            return null;
        var sb = new StringBuilder();
        foreach (var item in response.output)
        {
            if (item == null || item.content == null)
                continue;
            foreach (var part in item.content)
            {
                if (part != null && part.type == "output_text" && !string.IsNullOrEmpty(part.text))
                    sb.Append(part.text);
            }
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }

    private string ReadApiKeyFromFile()
    {
        try
        {
            if (!File.Exists(apiKeyFilePath))
            {
                Debug.LogError("API key file not found: " + apiKeyFilePath);
                return null;
            }

            return File.ReadAllText(apiKeyFilePath).Trim();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to read API key: " + e.Message);
            return null;
        }
    }

    static string Escape(string value)
    {
        if (value == null)
            return "";
        var sb = new StringBuilder(value.Length + 16);
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < 0x20)
                        sb.Append("\\u" + ((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    [Serializable]
    class FileUploadResponse
    {
        public string id;
    }

    [Serializable]
    class ResponsesResponse
    {
        public OutputItem[] output;
    }

    [Serializable]
    class OutputItem
    {
        public string type;
        public ContentPart[] content;
    }

    [Serializable]
    class ContentPart
    {
        public string type;
        public string text;
    }

    [Serializable]
    class ErrorResponse
    {
        public ErrorDetail error;
    }

    [Serializable]
    class ErrorDetail
    {
        public string message;
        public string type;
        public string code;
    }
}
