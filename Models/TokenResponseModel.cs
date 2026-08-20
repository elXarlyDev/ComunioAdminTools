
using Newtonsoft.Json;

namespace ComunioAdminTools.Models;

public class TokenResponse
{
    [JsonProperty("access_token")]
    public required string AccessToken { get; set; }

    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonProperty("token_type")]
    public string TokenType { get; set; } = default!;

    [JsonProperty("scope")]
    public string Scope { get; set; } = default!;

    [JsonProperty("refresh_token")]
    public string RefreshToken { get; set; } = default!;
}

