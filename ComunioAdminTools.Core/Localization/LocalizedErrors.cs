using System.Net;

namespace ComunioAdminTools.Core.Localization;

public interface ILocalizedError
{
    TextKey ResourceKey { get; }
    object?[] Arguments { get; }
}

public sealed class LocalizedInvalidOperationException(AppLocalization text, TextKey key, Exception? inner = null)
    : InvalidOperationException(text[key], inner), ILocalizedError
{
    public TextKey ResourceKey => key;
    public object?[] Arguments => [];
    public override string Message => text[key];
}

public sealed class LocalizedHttpRequestException(
    AppLocalization text, TextKey key, HttpStatusCode? status = null, Exception? inner = null)
    : HttpRequestException(text.Format(key, (int?)status), inner, status), ILocalizedError
{
    public TextKey ResourceKey => key;
    public object?[] Arguments => [(int?)StatusCode];
    public override string Message => text.Format(key, Arguments);
}
