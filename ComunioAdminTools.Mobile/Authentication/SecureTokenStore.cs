using ComunioAdminTools.Core.Authentication;
using Newtonsoft.Json;
using ComunioAdminTools.Core.Localization;

namespace ComunioAdminTools.Mobile.Authentication;

public sealed class SecureTokenStore(AppLocalization text) : ITokenStore
{
    private const string Key = "comunio.session.v1";

    public async Task<TokenSession?> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var json = await SecureStorage.Default.GetAsync(Key);
            if (json is null) return null;
            try { return JsonConvert.DeserializeObject<TokenSession>(json); }
            catch (JsonException)
            {
                SecureStorage.Default.Remove(Key);
                return null;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new LocalizedInvalidOperationException(text, TextKey.ReadTokenStoreError, ex);
        }
    }

    public async Task WriteAsync(TokenSession? session, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (session is null) SecureStorage.Default.Remove(Key);
            else await SecureStorage.Default.SetAsync(Key, JsonConvert.SerializeObject(session));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new LocalizedInvalidOperationException(text, TextKey.WriteTokenStoreError, ex);
        }
    }
}
