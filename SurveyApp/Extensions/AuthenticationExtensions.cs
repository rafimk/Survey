using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SurveyApp.Options;
using SurveyApp.Security;
using SurveyApp.Services;

namespace SurveyApp.Extensions;

/// <summary>
/// Registers exactly the authentication provider of the selected mode:
/// none = nothing (anonymous, no challenge); window = IIS handler or Negotiate; keycloak = cookie + OpenID Connect.
/// Any mode other than none puts a fallback policy on every endpoint: authenticated users only.
/// </summary>
public static class AuthenticationExtensions
{
    public const string AuthPath = "/auth";
    public const string ErrorPath = "/auth/error";
    public const string SignedOutPath = "/auth/signed-out";
    public const string LogoutPath = "/auth/logout";

    public static void AddSurveyAuthentication(this WebApplicationBuilder builder, SurveyStartupSettings settings)
    {
        builder.Services.AddSingleton(settings);
        builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();

        switch (settings.Mode)
        {
            case AuthenticationMode.None:
                return;

            case AuthenticationMode.Window:
                AddWindows(builder.Services);
                break;

            case AuthenticationMode.Keycloak:
                AddKeycloak(builder.Services, settings.Keycloak);
                break;
        }

        // Authenticated users only for every endpoint without its own policy; explicit policies are left untouched.
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
    }

    public static void UseSurveyAuthentication(this WebApplication app, SurveyStartupSettings settings)
    {
        if (settings.Mode == AuthenticationMode.None) return;

        // Gives the configured messages to bare 401/403 responses (e.g. when the Windows credential prompt is cancelled).
        // Setting a content type also stops the not-found re-execute middleware from replacing the response.
        app.Use(async (context, next) =>
        {
            await next(context);

            var response = context.Response;
            if (response.HasStarted || response.ContentLength.HasValue || !string.IsNullOrEmpty(response.ContentType)) return;

            var messages = context.RequestServices.GetRequiredService<ISurveyConfigurationService>().Current.Messages;
            var text = response.StatusCode switch
            {
                StatusCodes.Status401Unauthorized => messages.AuthenticationRequired,
                StatusCodes.Status403Forbidden => messages.AccessDenied,
                _ => null
            };
            if (text is null) return;

            response.ContentType = "text/plain; charset=utf-8";
            await response.WriteAsync(text);
        });

        app.UseAuthentication();
        app.UseAuthorization();
    }

    /// <summary>Sign-out endpoint (keycloak only). POST + antiforgery so a third-party page cannot log users out.</summary>
    public static void MapSurveyAuthenticationEndpoints(this WebApplication app, SurveyStartupSettings settings)
    {
        if (settings.Mode != AuthenticationMode.Keycloak) return;

        app.MapPost(LogoutPath, async (HttpContext context, IAntiforgery antiforgery) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest();
            }

            // Ends the local cookie session and, via the OIDC handler, the Keycloak session; lands on a public page
            // (not "/", which would immediately start a new login).
            return Results.SignOut(
                new AuthenticationProperties { RedirectUri = SignedOutPath },
                [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
        });
    }

    private static void AddWindows(IServiceCollection services)
    {
        // Exactly one scheme: IIS performs the handshake when hosted in IIS; Negotiate handles Kestrel (dev / self-host).
        if (IsHostedInIis())
        {
            services.Configure<IISServerOptions>(o => o.AutomaticAuthentication = false);
            services.AddAuthentication(IISServerDefaults.AuthenticationScheme);
        }
        else
        {
            services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
        }
    }

    private static void AddKeycloak(IServiceCollection services, KeycloakOptions keycloak)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = ".Survey.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            })
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.Authority = keycloak.Authority;               // discovery: {Authority}/.well-known/openid-configuration
                options.RequireHttpsMetadata = keycloak.RequireHttpsMetadata;
                options.ClientId = keycloak.ClientId;
                options.ClientSecret = keycloak.ClientSecret;
                options.CallbackPath = keycloak.CallbackPath;
                options.SignedOutCallbackPath = keycloak.SignedOutCallbackPath;

                options.ResponseType = OpenIdConnectResponseType.Code; // authorization code flow
                options.ResponseMode = OpenIdConnectResponseMode.Query;
                options.UsePkce = true;                                // PKCE (S256); state and nonce are on by default
                options.GetClaimsFromUserInfoEndpoint = false;
                options.MapInboundClaims = false;                      // keep standard names: sub, preferred_username
                options.SaveTokens = false;                            // no access/refresh tokens in the cookie
                options.BackchannelTimeout = TimeSpan.FromSeconds(15);
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "preferred_username",
                    ValidateIssuer = true,
                    ValidateAudience = true,                           // audience = ClientId
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true
                };

                options.Events.OnTokenValidated = context =>
                {
                    if (context.Principal?.Identity is System.Security.Claims.ClaimsIdentity identity && context.SecurityToken is not null)
                    {
                        // The handler drops "iss"; keep the validated issuer for the stable identity (issuer + sub).
                        identity.AddClaim(new System.Security.Claims.Claim(SurveyClaimTypes.Issuer, context.SecurityToken.Issuer));
                    }

                    // Only the ID token is kept (needed as id_token_hint for single sign-out), inside the encrypted cookie.
                    if (context.TokenEndpointResponse?.IdToken is { Length: > 0 } idToken && context.Properties is not null)
                    {
                        context.Properties.StoreTokens([new AuthenticationToken { Name = OpenIdConnectParameterNames.IdToken, Value = idToken }]);
                    }
                    return Task.CompletedTask;
                };

                // Provider unavailable / denied / invalid callback: show the configured error page. Never fall back to anonymous.
                options.Events.OnRemoteFailure = context =>
                {
                    context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("SurveyApp.Keycloak")
                        .LogWarning("OpenID Connect sign-in failed: {Reason}", context.Failure?.GetType().Name);
                    context.Response.Redirect(context.Request.PathBase + ErrorPath);
                    context.HandleResponse();
                    return Task.CompletedTask;
                };
            });
    }

    // ASP.NET Core Module / IIS Express set APP_POOL_ID for the worker process.
    private static bool IsHostedInIis() => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_POOL_ID"));
}
