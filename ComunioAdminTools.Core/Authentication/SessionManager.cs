using ComunioAdminTools.Core.Models;
using Newtonsoft.Json;

namespace ComunioAdminTools.Core.Authentication;

public interface ISessionManager
{
    Task<SessionDataModel> GetSessionAsync(CancellationToken cancellationToken = default);
    void Clear();
}

// One instance per MAUI/WebAssembly client, never a shared server-user session.
public sealed class SessionManager(IHttpClientFactory factory) : ISessionManager
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
        using var response = await client.GetAsync(string.Empty);
        response.EnsureSuccessStatusCode();
        var result = JsonConvert.DeserializeObject<SessionDataModel>(await response.Content.ReadAsStringAsync())
            ?? throw new InvalidOperationException("Respuesta de sesión vacía.");
        if (result.User is null || result.Community is null || string.IsNullOrWhiteSpace(result.Community.Id))
            throw new InvalidOperationException("Respuesta de sesión incompleta.");
        return result;
    }
}
