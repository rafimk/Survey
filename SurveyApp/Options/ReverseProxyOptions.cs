namespace SurveyApp.Options;

/// <summary>
/// ReverseProxy section. Forwarded headers are honoured only from the listed proxies/networks; with
/// <see cref="Enabled"/> false they are ignored entirely.
/// </summary>
public sealed class ReverseProxyOptions
{
    public bool Enabled { get; set; }
    public string[] KnownProxies { get; set; } = [];

    /// <summary>CIDR ranges such as "10.0.0.0/24".</summary>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>Application path prefix when hosted behind a proxy path (e.g. "/survey"). IIS virtual directories need no setting.</summary>
    public string? PathBase { get; set; }
}
