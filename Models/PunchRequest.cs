using System;
using System.Text.Json.Serialization;

public class PunchRequest
{
    [JsonPropertyName("employeeId")]
    public string? UniqueId { get; set; }

    [JsonPropertyName("photoData")]
    public string? ImageBase64 { get; set; }

    [JsonPropertyName("punchTime")]
    public DateTime PunchTime { get; set; }
}
