namespace ComunioAdminTools.Core.Authentication;

public sealed record TokenSession(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAtUtc);

public sealed class AuthenticationRequiredException(
    Localization.AppLocalization text, Localization.TextKey key, Exception? innerException = null)
    : Exception(text[key], innerException), Localization.ILocalizedError
{
    public Localization.TextKey ResourceKey => key;
    public object?[] Arguments => [];
    public override string Message => text[key];
}

