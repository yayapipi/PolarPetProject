namespace PolarPet.Scripts.AICore.Gemini
{
    
using System;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Gemini.API;

public class GeminiCore : MonoBehaviour
{
    [Header("Gemini Settings")]
    [Tooltip("Your Gemini API Key")]
    [SerializeField] private string apiKey;

    [Tooltip("gemini-2.5-flash-image or gemini-3-pro-image-preview")]
    [SerializeField] private string model = "gemini-2.5-flash-image";

    private string Endpoint =>
        $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

    public void GenerateImage(string prompt, Action<Texture2D> onDone, Action<string> onError = null)
    {
        var request = BuildTextToImageRequest(prompt);
        string json = JsonConvert.SerializeObject(request);

        StartCoroutine(Post(json, onDone, onError));
    }

    public void EditImage(string instruction, Texture2D image, Action<Texture2D> onDone, Action<string> onError = null)
    {
        if (image == null)
        {
            const string nullImageMessage = "Gemini EditImage failed: input image is null.";
            Debug.LogError(nullImageMessage);
            onError?.Invoke(nullImageMessage);
            return;
        }

        string base64 = EncodeTextureToBase64PngSafe(image);
        if (string.IsNullOrEmpty(base64))
        {
            const string encodeFailedMessage = "Gemini EditImage failed: cannot encode input image to PNG.";
            Debug.LogError(encodeFailedMessage);
            onError?.Invoke(encodeFailedMessage);
            return;
        }

        var request = BuildEditImageRequest(instruction, base64);
        string json = JsonConvert.SerializeObject(request);

        StartCoroutine(Post(json, onDone, onError));
    }

    /// <summary>
    /// 合成兩個圖片：將衣服圖片合成到北極熊圖片上
    /// </summary>
    public void CompositeImages(string instruction, Texture2D baseImage, Texture2D clothingImage, Action<Texture2D> onDone, Action<string> onError = null)
    {
        if (baseImage == null || clothingImage == null)
        {
            const string nullImageMessage = "Gemini CompositeImages failed: base image or clothing image is null.";
            Debug.LogError(nullImageMessage);
            onError?.Invoke(nullImageMessage);
            return;
        }

        string base64Base = EncodeTextureToBase64PngSafe(baseImage);
        string base64Clothing = EncodeTextureToBase64PngSafe(clothingImage);
        if (string.IsNullOrEmpty(base64Base) || string.IsNullOrEmpty(base64Clothing))
        {
            const string encodeFailedMessage = "Gemini CompositeImages failed: cannot encode one or more images to PNG.";
            Debug.LogError(encodeFailedMessage);
            onError?.Invoke(encodeFailedMessage);
            return;
        }

        var request = BuildCompositeImageRequest(instruction, base64Base, base64Clothing);
        string json = JsonConvert.SerializeObject(request);

        StartCoroutine(Post(json, onDone, onError));
    }

    string EncodeTextureToBase64PngSafe(Texture2D source)
    {
        if (source == null)
            return null;

        Texture2D encodableTexture = null;
        try
        {
            encodableTexture = ConvertToRgba32Texture(source);
            byte[] pngBytes = encodableTexture.EncodeToPNG();
            if (pngBytes == null || pngBytes.Length == 0)
                return null;

            return Convert.ToBase64String(pngBytes);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Gemini texture encode failed: {ex.Message}");
            return null;
        }
        finally
        {
            if (encodableTexture != null)
                Destroy(encodableTexture);
        }
    }

    Texture2D ConvertToRgba32Texture(Texture source)
    {
        int width = source.width;
        int height = source.height;

        RenderTexture previous = RenderTexture.active;
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);

        Graphics.Blit(source, rt);
        RenderTexture.active = rt;

        var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply(false, false);

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }

    GenerateContentRequest BuildTextToImageRequest(string prompt)
    {
        return new GenerateContentRequest
        {
            contents = new()
            {
                new Content
                {
                    role = "user",
                    parts = new()
                    {
                        new Part { text = prompt }
                    }
                }
            },
            generationConfig = new GenerationConfig
            {
                responseModalities = new() { "IMAGE" },
                imageConfig = new ImageConfig
                {
                    aspectRatio = "1:1"
                }
            }
        };
    }

    GenerateContentRequest BuildEditImageRequest(string instruction, string base64)
    {
        return new GenerateContentRequest
        {
            contents = new()
            {
                new Content
                {
                    role = "user",
                    parts = new()
                    {
                        new Part { text = instruction },
                        new Part
                        {
                            inlineData = new InlineData
                            {
                                mimeType = "image/png",
                                data = base64
                            }
                        }
                    }
                }
            },
            generationConfig = new GenerationConfig
            {
                responseModalities = new() { "IMAGE" }
            }
        };
    }

    GenerateContentRequest BuildCompositeImageRequest(string instruction, string base64Base, string base64Clothing)
    {
        return new GenerateContentRequest
        {
            contents = new()
            {
                new Content
                {
                    role = "user",
                    parts = new()
                    {
                        new Part { text = instruction },
                        new Part
                        {
                            inlineData = new InlineData
                            {
                                mimeType = "image/png",
                                data = base64Base
                            }
                        },
                        new Part
                        {
                            inlineData = new InlineData
                            {
                                mimeType = "image/png",
                                data = base64Clothing
                            }
                        }
                    }
                }
            },
            generationConfig = new GenerationConfig
            {
                responseModalities = new() { "IMAGE" }
            }
        };
    }


    IEnumerator Post(string json, Action<Texture2D> onDone, Action<string> onError)
    {
        var req = new UnityWebRequest(Endpoint, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();

        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("x-goog-api-key", apiKey);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            string errorMessage = $"Gemini Error: {req.error}\n{req.downloadHandler.text}";
            Debug.LogError(errorMessage);
            onError?.Invoke(errorMessage);
            yield break;
        }

        var response = JsonConvert.DeserializeObject<GenerateContentResponse>(req.downloadHandler.text);

        if (response.candidates == null || response.candidates.Count == 0)
        {
            const string noCandidateMessage = "Gemini returned no candidates. Check API key or model availability.";
            Debug.LogWarning(noCandidateMessage);
            onError?.Invoke(noCandidateMessage);
            yield break;
        }

        var candidate = response.candidates[0];

        if (candidate.finishReason != "STOP" && !string.IsNullOrEmpty(candidate.finishReason))
        {
            Debug.LogWarning($"Gemini generation finished with reason: {candidate.finishReason}");
        }

        var parts = candidate.content.parts;
        foreach (var p in parts)
        {
            if (p.inlineData != null)
            {
                byte[] bytes = Convert.FromBase64String(p.inlineData.data);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                onDone?.Invoke(tex);
                yield break;
            }
            else if (!string.IsNullOrEmpty(p.text))
            {
                Debug.Log($"Gemini Text Response: {p.text}");
            }
        }

        const string noImageMessage = "Gemini returned no image data in parts.";
        Debug.LogWarning(noImageMessage);
        onError?.Invoke(noImageMessage);
    }
}

}