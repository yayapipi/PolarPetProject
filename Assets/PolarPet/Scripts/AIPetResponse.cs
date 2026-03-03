using System;
using Newtonsoft.Json;

[Serializable]
public sealed class AIPetResponse
{
    [JsonProperty("content")]
    public string Content { get; set; }

    [JsonProperty("emotion")]
    public string Emotion { get; set; }
}