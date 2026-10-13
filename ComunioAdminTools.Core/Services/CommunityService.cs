using ComunioAdminTools.Core.Models;
using ComunioAdminTools.Core.Authentication;

namespace ComunioAdminTools.Core.Services;

public abstract class CommunityService : ComunioService
{
    protected readonly ISessionManager sessionManager;

    protected CommunityService(IHttpClientFactory factory, ISessionManager sessionManager) : base(factory)
    {
        this.sessionManager = sessionManager;
    }

    protected Task<SessionDataModel> GetSessionAsync() => sessionManager.GetSessionAsync();

    public async Task<StandingsModel> GetStandingsAsync()
    {
        var session = await GetSessionAsync();
        //if (!session.User.IsLeader && !session.User.IsAdmin)
        //    throw new UnauthorizedAccessException("No tienes permisos para realizar esta acción.");

        return await GetAsync<StandingsModel>($"communities/{Uri.EscapeDataString(session.Community.Id)}/standings?period=total&wpe=true");
    }
}
