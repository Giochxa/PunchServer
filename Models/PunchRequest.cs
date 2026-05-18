using System;
using System.Text.Json.Serialization;

public class PunchRequest
{
    [JsonPropertyName("personalId")]
    public string? PersonalId { get; set; }

    [JsonPropertyName("photoData")]
    public string? ImageBase64 { get; set; }

    [JsonPropertyName("punchTime")]
    public DateTime PunchTime { get; set; }
}
