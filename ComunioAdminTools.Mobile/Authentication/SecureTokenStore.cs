using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Localization;

namespace ComunioAdminTools.Mobile.Authentication;

public sealed class SecureTokenStore(AppLocalization text) : ITokenStore
{
    private const string Key = "comunio.access-token.v1";

    public async Task<string?> GetTokenAsync()
    {
        try { return await SecureStorage.Default.GetAsync(Key); }
        catch (Exception ex)
        {
            throw new LocalizedInvalidOperationException(text, TextKey.ReadTokenStoreError, ex);
        }
    }

    public async Task SetTokenAsync(string? token)
    {
        try
        {
            if (token is null) SecureStorage.Default.Remove(Key);
            else await SecureStorage.Default.SetAsync(Key, token);
        }
        catch (Exception ex)
        {
            throw new LocalizedInvalidOperationException(text, TextKey.WriteTokenStoreError, ex);
        }
    }
}
