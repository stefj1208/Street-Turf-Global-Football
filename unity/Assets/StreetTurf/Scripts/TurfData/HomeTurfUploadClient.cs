using System;
using System.Collections;
using StreetTurf.TurfEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace StreetTurf.TurfData
{
    public sealed class HomeTurfUploadClient : MonoBehaviour
    {
        [SerializeField] private string apiBaseUrl = "http://127.0.0.1:8000";
        [SerializeField, TextArea] private string inpaintPrompt =
            "empty urban asphalt and wall, realistic continuation of the surrounding street, consistent perspective, consistent light";
        [SerializeField] private int seed = 42;
        [SerializeField, Min(10)] private int timeoutSeconds = 180;

        public IEnumerator CreateHomeTurf(
            Texture originalTexture,
            SurfaceMaskPainter maskPainter,
            HomeTurfDraft draft,
            Action<HomeTurfManifest> onSuccess,
            Action<string> onError
        )
        {
            if (originalTexture == null || maskPainter == null || draft == null)
            {
                onError?.Invoke("Original texture, mask painter and draft are required.");
                yield break;
            }

            Texture2D maskTexture = maskPainter.MaskTexture;
            if (maskTexture == null)
            {
                onError?.Invoke("The removal mask has not been initialized.");
                yield break;
            }

            byte[] originalPng;
            byte[] maskPng;
            try
            {
                originalPng = EncodeTextureToPng(
                    originalTexture,
                    maskTexture.width,
                    maskTexture.height
                );
                maskPng = maskPainter.ExportMaskPng();
            }
            catch (Exception exception)
            {
                onError?.Invoke($"Unable to encode images: {exception.Message}");
                yield break;
            }

            WWWForm form = new WWWForm();
            form.AddBinaryData("environment", originalPng, "environment.png", "image/png");
            form.AddBinaryData("mask", maskPng, "mask.png", "image/png");
            form.AddField("manifest_json", JsonUtility.ToJson(draft));
            form.AddField("prompt", inpaintPrompt);
            form.AddField("seed", seed.ToString());

            string url = $"{apiBaseUrl.TrimEnd('/')}/v1/home-turfs";
            using UnityWebRequest request = UnityWebRequest.Post(url, form);
            request.timeout = timeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string serverBody = request.downloadHandler?.text ?? string.Empty;
                onError?.Invoke(
                    $"Upload failed ({request.responseCode}): {request.error}\n{serverBody}"
                );
                yield break;
            }

            HomeTurfManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<HomeTurfManifest>(request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                onError?.Invoke($"Invalid server response: {exception.Message}");
                yield break;
            }

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.turfId))
            {
                onError?.Invoke("The server response does not contain a turfId.");
                yield break;
            }

            onSuccess?.Invoke(manifest);
        }

        private static byte[] EncodeTextureToPng(Texture source, int width, int height)
        {
            RenderTexture temporary = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB
            );
            RenderTexture previous = RenderTexture.active;
            Texture2D readable = null;

            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                readable = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false,
                    false
                );
                readable.ReadPixels(
                    new Rect(0, 0, width, height),
                    0,
                    0,
                    false
                );
                readable.Apply(false, false);
                return readable.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
                if (readable != null)
                {
                    UnityEngine.Object.Destroy(readable);
                }
            }
        }
    }
}
