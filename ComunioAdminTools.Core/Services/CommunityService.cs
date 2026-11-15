using ComunioAdminTools.Core.Models;
using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Localization;

namespace ComunioAdminTools.Core.Services;

public abstract class CommunityService(IHttpClientFactory factory, ISessionManager sessionManager, AppLocalization text) 
    : ComunioService(factory, text)
{
    protected readonly ISessionManager sessionManager = sessionManager;

    protected SessionDataModel Session => sessionManager.Session
        ?? throw new AuthenticationRequiredException(text, TextKey.SignInRequired);

    protected string GetCommunityUriPart() => $"communities/{Uri.EscapeDataString(Session.Community.Id)}";

    protected async Task<StandingsModel> GetStandings(CancellationToken cancellationToken = default) =>
        await Get<StandingsModel>($"communities/{Uri.EscapeDataString(Session.Community.Id)}/standings?period=total&wpe=true", cancellationToken);
    
}
