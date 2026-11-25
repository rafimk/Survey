namespace SurveyApp.Options;

/// <summary>Authentication:Keycloak — only validated and used when Authentication:Mode is keycloak.</summary>
public sealed class KeycloakOptions
{
    /// <summary>Full issuer URL including the realm (and the legacy "/auth" prefix if the server uses one).</summary>
    public string Authority { get; set; } = "";
    public string ClientId { get; set; } = "";

    /// <summary>Confidential client secret. Supply via environment variable or user secrets; never commit it.</summary>
    public string ClientSecret { get; set; } = "";
    public bool RequireHttpsMetadata { get; set; } = true;
    public string CallbackPath { get; set; } = "/signin-oidc";
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";
}
