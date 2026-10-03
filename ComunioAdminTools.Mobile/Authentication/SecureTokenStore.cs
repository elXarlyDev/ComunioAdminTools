using ComunioAdminTools.Core.Authentication;
using Newtonsoft.Json;

namespace ComunioAdminTools.Mobile.Authentication;

public sealed class SecureTokenStore : ITokenStore
{
    private const string Key = "comunio.session.v1";

    public async Task<TokenSession?> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = await SecureStorage.Default.GetAsync(Key);
        if (json is null) return null;
        try { return JsonConvert.DeserializeObject<TokenSession>(json); }
        catch (JsonException)
        {
            SecureStorage.Default.Remove(Key);
            return null;
        }
    }

    public async Task WriteAsync(TokenSession? session, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (session is null) SecureStorage.Default.Remove(Key);
        else await SecureStorage.Default.SetAsync(Key, JsonConvert.SerializeObject(session));
    }
}
