using System.Net;

namespace SurveyApp.Options;

/// <summary>
/// Settings that decide which services are registered. Read and validated once at startup; any problem stops
/// the application with a clear message (there is no silent fallback, least of all to anonymous access).
/// </summary>
public sealed record SurveyStartupSettings(
    AuthenticationMode Mode,
    KeycloakOptions Keycloak,
    bool SqlPersistenceEnabled,
    string? ConnectionString,
    ReverseProxyOptions ReverseProxy)
{
    public const string AuthenticationSection = "Authentication";
    public const string KeycloakSection = "Authentication:Keycloak";
    public const string ReverseProxySection = "ReverseProxy";
    public const string ConnectionStringName = "SurveyDatabase";

    public static SurveyStartupSettings Load(IConfiguration configuration)
    {
        var errors = new List<string>();

        // Breaking change: the old boolean is gone. Fail loudly so stale appsettings/environment overrides are noticed.
        if (configuration[$"{FeatureFlagsOptions.SectionName}:EnableWindowsAuthentication"] is not null)
        {
            errors.Add("FeatureFlags:EnableWindowsAuthentication is obsolete. Remove it and set Authentication:Mode instead: " +
                       "'window' (was true) or 'none' (was false). If it came from an environment variable, rename " +
                       "FeatureFlags__EnableWindowsAuthentication to Authentication__Mode.");
        }

        var modeText = configuration[$"{AuthenticationSection}:Mode"];
        if (!AuthenticationModeParser.TryParse(modeText, out var mode))
        {
            errors.Add($"Authentication:Mode '{modeText}' is not supported. Allowed values: {AuthenticationModeParser.AllowedValues}.");
        }

        var keycloak = configuration.GetSection(KeycloakSection).Get<KeycloakOptions>() ?? new KeycloakOptions();
        if (mode == AuthenticationMode.Keycloak) ValidateKeycloak(keycloak, errors);

        var sqlEnabled = configuration.GetValue<bool>($"{FeatureFlagsOptions.SectionName}:{nameof(FeatureFlagsOptions.EnableSqlPersistence)}");
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (sqlEnabled && string.IsNullOrWhiteSpace(connectionString))
        {
            errors.Add($"FeatureFlags:EnableSqlPersistence is true but ConnectionStrings:{ConnectionStringName} is empty.");
        }

        var proxy = configuration.GetSection(ReverseProxySection).Get<ReverseProxyOptions>() ?? new ReverseProxyOptions();
        ValidateReverseProxy(proxy, errors);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Invalid application configuration:" + Environment.NewLine + " - " +
                string.Join(Environment.NewLine + " - ", errors));
        }

        return new SurveyStartupSettings(mode, keycloak, sqlEnabled, sqlEnabled ? connectionString : null, proxy);
    }

    private static void ValidateKeycloak(KeycloakOptions k, List<string> errors)
    {
        const string section = KeycloakSection;

        if (!Uri.TryCreate(k.Authority, UriKind.Absolute, out var authority) ||
            (authority.Scheme != Uri.UriSchemeHttps && authority.Scheme != Uri.UriSchemeHttp))
        {
            errors.Add($"{section}:Authority must be an absolute http(s) URL including the realm, e.g. https://sso.example.internal/realms/survey.");
        }
        else if (k.RequireHttpsMetadata && authority.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add($"{section}:Authority must use https while {section}:RequireHttpsMetadata is true.");
        }

        if (string.IsNullOrWhiteSpace(k.ClientId)) errors.Add($"{section}:ClientId is required.");
        if (string.IsNullOrWhiteSpace(k.ClientSecret))
            errors.Add($"{section}:ClientSecret is required (set Authentication__Keycloak__ClientSecret or a user secret).");
        if (!IsLocalPath(k.CallbackPath)) errors.Add($"{section}:CallbackPath must be an application-relative path starting with '/'.");
        if (!IsLocalPath(k.SignedOutCallbackPath)) errors.Add($"{section}:SignedOutCallbackPath must be an application-relative path starting with '/'.");
    }

    private static void ValidateReverseProxy(ReverseProxyOptions p, List<string> errors)
    {
        if (p.PathBase is { Length: > 0 } pathBase && (!pathBase.StartsWith('/') || pathBase.EndsWith('/') || pathBase.StartsWith("//")))
            errors.Add("ReverseProxy:PathBase must start with '/' and must not end with '/'.");

        if (!p.Enabled) return;

        if (p.KnownProxies.Length == 0 && p.KnownNetworks.Length == 0)
            errors.Add("ReverseProxy:Enabled requires at least one entry in KnownProxies or KnownNetworks (forwarded headers are never trusted from arbitrary hosts).");
        foreach (var proxy in p.KnownProxies.Where(x => !IPAddress.TryParse(x, out _)))
            errors.Add($"ReverseProxy:KnownProxies contains an invalid IP address '{proxy}'.");
        foreach (var network in p.KnownNetworks.Where(x => !TryParseNetwork(x, out _)))
            errors.Add($"ReverseProxy:KnownNetworks contains an invalid CIDR range '{network}'.");
    }

    public static bool TryParseNetwork(string value, out System.Net.IPNetwork network) =>
        System.Net.IPNetwork.TryParse(value, out network);

    private static bool IsLocalPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path.StartsWith('/') && !path.StartsWith("//") && !path.Contains('\\');
}
