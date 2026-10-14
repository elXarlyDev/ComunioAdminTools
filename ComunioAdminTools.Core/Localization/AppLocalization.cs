using System.Globalization;
using System.Resources;

namespace ComunioAdminTools.Core.Localization;

public sealed class AppLocalization
{
    public const string SpanishCulture = "es-ES";
    public const string ValencianCulture = "ca-ES-valencia";

    // Both catalogs live in Core, including in WebAssembly. Switching language does
    // not depend on downloading a satellite assembly or on process-wide culture.
    private static readonly ResourceManager SpanishResources = new(
        "ComunioAdminTools.Core.Localization.Strings.es-ES", typeof(AppLocalization).Assembly);
    private static readonly ResourceManager ValencianResources = new(
        "ComunioAdminTools.Core.Localization.Strings.ca-ES-valencia", typeof(AppLocalization).Assembly);

    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo(SpanishCulture);
    public string Language => Culture.Name.Equals(ValencianCulture, StringComparison.OrdinalIgnoreCase)
        ? ValencianCulture : SpanishCulture;
    public event Action? Changed;

    public string this[TextKey key] => GetString(key);

    public string GetString(TextKey key)
    {
        var manager = Language == ValencianCulture ? ValencianResources : SpanishResources;
        return manager.GetString(key.ToString(), CultureInfo.InvariantCulture)
            ?? SpanishResources.GetString(key.ToString(), CultureInfo.InvariantCulture)
            ?? throw new MissingManifestResourceException(key.ToString());
    }

    public string Format(TextKey key, params object?[] arguments)
        => string.Format(Culture, this[key], arguments);

    public void SetCulture(string? culture)
    {
        var normalized = NormalizeCulture(culture);
        if (Language == normalized) return;
        Culture = CultureInfo.GetCultureInfo(normalized);
        Changed?.Invoke();
    }

    public static string NormalizeCulture(string? culture)
        => culture is not null && (culture.Equals("ca", StringComparison.OrdinalIgnoreCase)
            || culture.StartsWith("ca-", StringComparison.OrdinalIgnoreCase))
            ? ValencianCulture : SpanishCulture;

    public string ErrorMessage(Exception error, TextKey fallback = TextKey.OperationError)
        => error switch
        {
            ILocalizedError localized => Format(localized.ResourceKey, localized.Arguments),
            UnauthorizedAccessException => this[TextKey.PermissionDenied],
            ArgumentException => this[TextKey.CredentialsRequired],
            HttpRequestException => this[TextKey.NetworkError],
            OperationCanceledException => this[TextKey.TimeoutError],
            _ => this[fallback]
        };
}
