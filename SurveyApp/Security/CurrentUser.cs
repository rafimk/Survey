using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using SurveyApp.Options;

namespace SurveyApp.Security;

/// <summary>Claim added after OIDC token validation: the validated token issuer (the default "iss" claim is dropped by the handler).</summary>
public static class SurveyClaimTypes
{
    public const string Issuer = "survey_issuer";
}

/// <summary>
/// Identity of the person submitting, derived only from the server-side principal.
/// Anonymous (mode none, or not authenticated) means every identity field is null.
/// </summary>
public sealed record CurrentUser(AuthenticationMode Mode, bool IsAuthenticated, string? Identifier, string? UserName)
{
    public static CurrentUser Anonymous(AuthenticationMode mode) => new(mode, false, null, null);

    public static CurrentUser From(AuthenticationMode mode, ClaimsPrincipal? principal)
    {
        if (mode == AuthenticationMode.None || principal?.Identity is not { IsAuthenticated: true } identity)
            return Anonymous(mode);

        return mode switch
        {
            // DOMAIN\user from the Windows token; the SID is the stable identifier when present.
            AuthenticationMode.Window when !string.IsNullOrWhiteSpace(identity.Name) =>
                new(mode, true, principal.FindFirstValue(ClaimTypes.PrimarySid) ?? identity.Name, identity.Name),

            // Stable identity = issuer + sub. Display name = preferred_username. Email/name are never used as keys.
            AuthenticationMode.Keycloak when FromKeycloak(principal) is { } user => user,

            _ => Anonymous(mode)
        };
    }

    private static CurrentUser? FromKeycloak(ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var issuer = principal.FindFirstValue(SurveyClaimTypes.Issuer);
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(issuer)) return null;

        return new CurrentUser(AuthenticationMode.Keycloak, true, $"{issuer}|{subject}", principal.FindFirstValue("preferred_username"));
    }
}

public interface ICurrentUserAccessor
{
    Task<CurrentUser> GetAsync();
}

/// <summary>Reads the Blazor authentication state (server-side principal of the circuit) and maps it to <see cref="CurrentUser"/>.</summary>
public sealed class CurrentUserAccessor(AuthenticationStateProvider provider, SurveyStartupSettings settings) : ICurrentUserAccessor
{
    public async Task<CurrentUser> GetAsync() =>
        CurrentUser.From(settings.Mode, (await provider.GetAuthenticationStateAsync()).User);
}
