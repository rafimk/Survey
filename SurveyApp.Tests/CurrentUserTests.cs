using System.Security.Claims;
using SurveyApp.Options;
using SurveyApp.Security;

namespace SurveyApp.Tests;

public class CurrentUserTests
{
    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test", nameType: "preferred_username", roleType: ClaimTypes.Role));

    [Fact]
    public void Mode_none_never_exposes_an_identity_even_if_a_principal_is_present()
    {
        var user = CurrentUser.From(AuthenticationMode.None, Principal(new Claim("preferred_username", "intruder")));

        Assert.Equal(new CurrentUser(AuthenticationMode.None, false, null, null), user);
    }

    [Fact]
    public void Missing_principal_is_anonymous()
    {
        Assert.False(CurrentUser.From(AuthenticationMode.Window, null).IsAuthenticated);
        Assert.False(CurrentUser.From(AuthenticationMode.Keycloak, new ClaimsPrincipal(new ClaimsIdentity())).IsAuthenticated);
    }

    [Fact]
    public void Windows_identity_uses_the_domain_qualified_name_and_sid()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, @"CONTOSO\jdoe"), new Claim(ClaimTypes.PrimarySid, "S-1-5-21-1")], "Negotiate"));

        var user = CurrentUser.From(AuthenticationMode.Window, principal);

        Assert.True(user.IsAuthenticated);
        Assert.Equal(@"CONTOSO\jdoe", user.UserName);
        Assert.Equal("S-1-5-21-1", user.Identifier);
    }

    [Fact]
    public void Keycloak_identity_is_issuer_plus_subject_with_preferred_username_for_display()
    {
        var user = CurrentUser.From(AuthenticationMode.Keycloak, Principal(
            new Claim("sub", "abc-123"),
            new Claim(SurveyClaimTypes.Issuer, "https://sso.example.internal/realms/survey"),
            new Claim("preferred_username", "jdoe"),
            new Claim("email", "jdoe@example.com"),
            new Claim("name", "Jane Doe")));

        Assert.Equal("https://sso.example.internal/realms/survey|abc-123", user.Identifier);
        Assert.Equal("jdoe", user.UserName);
    }

    [Fact]
    public void Keycloak_principal_without_subject_or_issuer_is_not_trusted()
    {
        var noSubject = Principal(new Claim(SurveyClaimTypes.Issuer, "https://sso"), new Claim("preferred_username", "jdoe"));
        var noIssuer = Principal(new Claim("sub", "abc"), new Claim("email", "jdoe@example.com"));

        Assert.False(CurrentUser.From(AuthenticationMode.Keycloak, noSubject).IsAuthenticated);
        Assert.False(CurrentUser.From(AuthenticationMode.Keycloak, noIssuer).IsAuthenticated);
    }
}
