using System.Text.Json;
using System.Text.Json.Serialization;

namespace PearHonorific.Pear;

public sealed class SongInfo
{
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("artist")] public string Artist { get; set; } = string.Empty;
    [JsonPropertyName("album")] public string? Album { get; set; }
    [JsonPropertyName("songDuration")] public double SongDuration { get; set; }
    [JsonPropertyName("elapsedSeconds")] public double? ElapsedSeconds { get; set; }
    [JsonPropertyName("isPaused")] public bool? IsPaused { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("videoId")] public string VideoId { get; set; } = string.Empty;
}

/// <summary>Superset of the api-server websocket messages (PLAYER_INFO, VIDEO_CHANGED, ...).</summary>
public sealed class PearMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("song")] public SongInfo? Song { get; set; }
    [JsonPropertyName("isPlaying")] public bool? IsPlaying { get; set; }
    [JsonPropertyName("position")] public double? Position { get; set; }
}

public sealed class AuthResponse
{
    [JsonPropertyName("accessToken")] public string? AccessToken { get; set; }
}

public static class PearJson
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
}
