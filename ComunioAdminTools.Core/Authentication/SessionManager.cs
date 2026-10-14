using ComunioAdminTools.Core.Models;
using Newtonsoft.Json;
using ComunioAdminTools.Core.Localization;
using ComunioAdminTools.Core.Services;

namespace ComunioAdminTools.Core.Authentication;

public interface ISessionManager
{
    Task<SessionDataModel> GetSessionAsync(CancellationToken cancellationToken = default);
    void Clear();
}

// One instance per MAUI/WebAssembly client, never a shared server-user session.
public sealed class SessionManager(IHttpClientFactory factory, AppLocalization text) : ISessionManager
{
    private readonly object sync = new();
    private Task<SessionDataModel>? pending;

    public async Task<SessionDataModel> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task<SessionDataModel> current;
            lock (sync) current = pending ??= LoadAsync();
            SessionDataModel result;
            try { result = await current.WaitAsync(cancellationToken); }
            catch
            {
                // A cancelled caller must not discard the load shared by other callers.
                if (current.IsFaulted || current.IsCanceled)
                    lock (sync)
                        if (ReferenceEquals(pending, current)) pending = null;
                throw;
            }
            lock (sync)
                if (ReferenceEquals(pending, current)) return result;
            // Login/logout occurred while loading: never return the old account's data.
        }
    }

    public void Clear()
    {
        lock (sync) pending = null;
    }

    private async Task<SessionDataModel> LoadAsync()
    {
        using var client = factory.CreateClient("Comunio");
        using var request = new HttpRequestMessage(HttpMethod.Get, string.Empty);
        using var response = await ComunioHttp.SendAsync(client, request, text);
        ComunioHttp.EnsureSuccess(response, text);
        var result = ComunioHttp.Deserialize<SessionDataModel>(await response.Content.ReadAsStringAsync(), text, TextKey.EmptySessionResponse);
        if (result.User is null || result.Community is null || string.IsNullOrWhiteSpace(result.Community.Id))
            throw new LocalizedInvalidOperationException(text, TextKey.IncompleteSessionResponse);
        return result;
    }
}
