using ComunioAdminTools.Core.Models;
using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Localization;

namespace ComunioAdminTools.Core.Services;

public abstract class CommunityService : ComunioService
{
    protected readonly ISessionManager sessionManager;

    protected CommunityService(IHttpClientFactory factory, ISessionManager sessionManager, AppLocalization text) : base(factory, text)
    {
        this.sessionManager = sessionManager;
    }

    protected SessionDataModel Session => sessionManager.Session
        ?? throw new AuthenticationRequiredException(text, TextKey.SignInRequired);

    public async Task<StandingsModel> GetStandingsAsync()
    {
        var session = Session;
        //if (!session.User.IsLeader && !session.User.IsAdmin)
        //    throw new UnauthorizedAccessException(text[TextKey.PermissionDenied]);

        return await GetAsync<StandingsModel>($"communities/{Uri.EscapeDataString(session.Community.Id)}/standings?period=total&wpe=true");
    }
}
