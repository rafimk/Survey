using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using SurveyApp.Persistence;

namespace SurveyApp.Tests;

/// <summary>
/// End-to-end checks against the real pipeline using TestServer. No Keycloak, Docker or SQL Server is involved:
/// the OIDC provider metadata is a static in-memory document and authenticated users come from a test handler.
/// Settings are passed via environment variables because the mode is read during startup; the class runs its tests
/// sequentially so the variables are never set concurrently.
/// </summary>
public sealed class HostingTests : IDisposable
{
    // xUnit creates one instance per test, so environment overrides are always restored after each test.
    private readonly Stack<EnvScope> _scopes = new();

    public void Dispose()
    {
        while (_scopes.Count > 0) _scopes.Pop().Dispose();
    }

    private const string Authority = "https://sso.example.internal/realms/survey";

    private static readonly (string, string)[] KeycloakSettings =
    [
        ("Authentication__Mode", "keycloak"),
        ("Authentication__Keycloak__Authority", Authority),
        ("Authentication__Keycloak__ClientId", "survey-web"),
        ("Authentication__Keycloak__ClientSecret", "test-secret")
    ];

    // ---- mode: none ----

    [Fact]
    public async Task None_mode_serves_anonymous_users_without_sql_or_a_connection_string()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()); // Blazor entity-encodes non-ASCII

        Assert.True(response.StatusCode == HttpStatusCode.OK, html[..Math.Min(1500, html.Length)]);
        Assert.False(response.Headers.Contains("WWW-Authenticate"));
        Assert.Contains("lang=\"ar\" dir=\"rtl\"", html);
        Assert.Contains("نموذج التقديم", html);
        Assert.Contains("شكراً لمشاركتكم، نقدّر وقتكم وآراءكم.", html);
        Assert.Contains("color:#dc2626;font-weight:600;", html);
        Assert.DoesNotContain("auth/logout", html);

        // Radio groups render as native radios with a unique name per group, nothing preselected.
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(html, "type=\"radio\"[^>]*name=\"rg-interests\"|name=\"rg-interests\"[^>]*type=\"radio\"").Count);
        Assert.Equal(5, System.Text.RegularExpressions.Regex.Matches(html, "name=\"rg-services\"").Count);
        Assert.DoesNotContain("chip selected", html);
        // Full name and comments share the same textarea renderer.
        Assert.Contains("<textarea id=\"el-full-name\"", html);
        Assert.Contains("<textarea id=\"el-comments\"", html);
        Assert.DoesNotContain("<input id=\"el-full-name\"", html);
        Assert.IsType<NonPersistingSubmissionStore>(factory.Services.GetRequiredService<ISurveySubmissionStore>());
    }

    [Fact]
    public async Task Validation_disabled_hides_required_indicators()
    {
        // Built one after the other: the flag is read from the environment while each host starts.
        using var enabled = CreateFactory(("FeatureFlags__EnableFormValidation", "true"));
        var withIndicator = await enabled.CreateClient().GetStringAsync("/");

        using var disabled = CreateFactory(("FeatureFlags__EnableFormValidation", "false"));
        var withoutIndicator = await disabled.CreateClient().GetStringAsync("/");

        Assert.Contains("class=\"required\"", withIndicator);
        Assert.DoesNotContain("class=\"required\"", withoutIndicator);
    }

    // ---- SQL persistence flag ----

    [Fact]
    public void Enabled_persistence_registers_the_sql_store_without_connecting_at_startup()
    {
        using var factory = CreateFactory(
            ("FeatureFlags__EnableSqlPersistence", "true"),
            ("ConnectionStrings__SurveyDatabase", "Server=tcp:127.0.0.1,1;Database=x;Integrated Security=True;Connect Timeout=2"));

        factory.CreateClient().Dispose(); // builds the host; no connection is attempted until a submission is stored

        Assert.IsType<SqlServerSubmissionStore>(factory.Services.GetRequiredService<ISurveySubmissionStore>());
    }

    [Fact]
    public void Enabled_persistence_without_a_connection_string_stops_startup()
    {
        using var factory = CreateFactory(("FeatureFlags__EnableSqlPersistence", "true"));

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("SurveyDatabase", ex.ToString());
    }

    // ---- mode: window (policy behaviour with a test authentication handler; the Negotiate handshake itself needs a real host) ----

    [Fact]
    public async Task Window_mode_rejects_anonymous_requests_and_admits_authenticated_ones()
    {
        using var factory = CreateFactory(("Authentication__Mode", "window")).WithoutNegotiateHandshake().WithTestAuthentication();
        using var client = factory.CreateClient();

        var anonymous = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("يرجى تسجيل الدخول.", await anonymous.Content.ReadAsStringAsync());

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(TestAuthHandler.UserHeader, @"CONTOSO\jdoe");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
    }

    // ---- mode: keycloak (no live provider) ----

    [Fact]
    public async Task Keycloak_mode_redirects_anonymous_users_to_the_provider_with_code_flow_pkce_state_and_nonce()
    {
        using var factory = CreateFactory(KeycloakSettings).WithStaticOidcMetadata();
        using var client = factory.CreateNonRedirectingClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith($"{Authority}/protocol/openid-connect/auth", location.ToString());
        var query = System.Web.HttpUtility.ParseQueryString(location.Query);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("survey-web", query["client_id"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["code_challenge"]));
        Assert.False(string.IsNullOrEmpty(query["state"]));
        Assert.False(string.IsNullOrEmpty(query["nonce"]));
        Assert.Equal("http://localhost/signin-oidc", query["redirect_uri"]);
        Assert.Contains("openid", query["scope"]);
        Assert.DoesNotContain("test-secret", location.ToString());
    }

    [Fact]
    public async Task Keycloak_mode_registers_cookie_and_openid_connect_only()
    {
        using var factory = CreateFactory(KeycloakSettings).WithStaticOidcMetadata();
        factory.CreateClient().Dispose();

        var schemes = (await factory.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync())
            .Select(s => s.Name).Order().ToArray();

        Assert.Equal(["Cookies", "OpenIdConnect"], schemes);
        var oidc = factory.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnectDefaults.AuthenticationScheme);
        Assert.True(oidc.UsePkce);
        Assert.False(oidc.SaveTokens);
        Assert.Equal("/signin-oidc", oidc.CallbackPath.Value);
        Assert.Equal("/signout-callback-oidc", oidc.SignedOutCallbackPath.Value);
        Assert.Equal(OpenIdConnectResponseType.Code, oidc.ResponseType);
        Assert.True(oidc.RequireHttpsMetadata);
    }

    [Fact]
    public async Task Keycloak_callback_failures_show_the_configured_error_page_never_anonymous_access()
    {
        using var factory = CreateFactory(KeycloakSettings).WithStaticOidcMetadata();
        using var client = factory.CreateNonRedirectingClient();

        // A callback without the correlation/state cookies is an invalid (possibly forged) response.
        var callback = await client.GetAsync("/signin-oidc?code=abc&state=forged");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/auth/error", callback.Headers.Location!.ToString());

        // The error page itself is public (otherwise the failure would loop back into sign-in) and carries configured text.
        var page = await client.GetAsync("/auth/error");
        var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("تعذر تسجيل الدخول عبر مزود الهوية", html);

        // Anything else is still protected.
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task Keycloak_signed_in_user_sees_name_and_a_post_logout_form_with_antiforgery_token()
    {
        using var factory = CreateFactory(KeycloakSettings).WithStaticOidcMetadata().WithTestAuthentication();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(TestAuthHandler.UserHeader, "jdoe");

        var response = await client.SendAsync(request);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("jdoe", html);
        Assert.Contains("method=\"post\" action=\"auth/logout\"", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains("تسجيل الخروج", html);
    }

    [Fact]
    public async Task Logout_requires_a_valid_antiforgery_token()
    {
        using var factory = CreateFactory(KeycloakSettings).WithStaticOidcMetadata().WithTestAuthentication();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        request.Headers.Add(TestAuthHandler.UserHeader, "jdoe");

        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
    }

    private WebApplicationFactory<Program> CreateFactory(params (string Name, string Value)[] settings)
    {
        _scopes.Push(new EnvScope(settings)); // read while the host starts; restored in Dispose()
        return new WebApplicationFactory<Program>();
    }
}

internal static class HostingTestExtensions
{
    public static WebApplicationFactory<Program> WithTestAuthentication(this WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddAuthentication(o =>
                {
                    o.DefaultScheme = TestAuthHandler.SchemeName;
                    o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { })));

    /// <summary>
    /// TestServer cannot run the real Negotiate handler (it needs Kestrel's connection features), so the scheme is kept
    /// registered but backed by the test handler. The real handshake is verified against Kestrel manually (see README).
    /// </summary>
    public static WebApplicationFactory<Program> WithoutNegotiateHandshake(this WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                foreach (var scheme in options.Schemes.Where(s => s.HandlerType?.Name == "NegotiateHandler"))
                    scheme.HandlerType = typeof(TestAuthHandler);
            })));

    /// <summary>Replaces OIDC discovery with a static document so no network access is needed.</summary>
    public static WebApplicationFactory<Program> WithStaticOidcMetadata(this WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                    new OpenIdConnectConfiguration
                    {
                        Issuer = "https://sso.example.internal/realms/survey",
                        AuthorizationEndpoint = "https://sso.example.internal/realms/survey/protocol/openid-connect/auth",
                        TokenEndpoint = "https://sso.example.internal/realms/survey/protocol/openid-connect/token",
                        EndSessionEndpoint = "https://sso.example.internal/realms/survey/protocol/openid-connect/logout"
                    }))));

    public static HttpClient CreateNonRedirectingClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}

/// <summary>Authenticates requests that carry the X-Test-User header; challenges (401) everything else.</summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var user)) return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, user.ToString()),
                new Claim("preferred_username", user.ToString()),
                new Claim("sub", "sub-" + user),
                new Claim(SurveyApp.Security.SurveyClaimTypes.Issuer, "https://sso.example.internal/realms/survey")
            ],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        return Task.CompletedTask;
    }
}
