using SurveyApp.Options;

namespace SurveyApp.Tests;

public class StartupSettingsTests
{
    private static readonly (string, string?)[] ValidKeycloak =
    [
        ("Authentication:Mode", "keycloak"),
        ("Authentication:Keycloak:Authority", "https://sso.example.internal/realms/survey"),
        ("Authentication:Keycloak:ClientId", "survey-web"),
        ("Authentication:Keycloak:ClientSecret", "s3cret")
    ];

    private static InvalidOperationException Rejected(params (string, string?)[] values) =>
        Assert.Throws<InvalidOperationException>(() => SurveyStartupSettings.Load(TestSupport.Config(values)));

    [Fact]
    public void Local_default_is_anonymous_without_persistence_and_needs_no_infrastructure()
    {
        var s = SurveyStartupSettings.Load(TestSupport.Config(("Authentication:Mode", "none")));

        Assert.Equal(AuthenticationMode.None, s.Mode);
        Assert.False(s.SqlPersistenceEnabled);
        Assert.Null(s.ConnectionString);
    }

    [Theory]
    [InlineData("NONE", AuthenticationMode.None)]
    [InlineData("Window", AuthenticationMode.Window)]
    [InlineData(" window ", AuthenticationMode.Window)]
    public void Modes_are_case_insensitive(string value, AuthenticationMode expected)
    {
        Assert.Equal(expected, SurveyStartupSettings.Load(TestSupport.Config(("Authentication:Mode", value))).Mode);
    }

    [Theory]
    [InlineData("windows")]
    [InlineData("oidc")]
    [InlineData("anonymous")]
    [InlineData("")]
    [InlineData(null)]
    public void Unknown_or_missing_modes_fail_startup_instead_of_defaulting_to_anonymous(string? value)
    {
        var ex = Rejected(("Authentication:Mode", value));

        Assert.Contains("none, window, keycloak", ex.Message);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void Obsolete_windows_flag_is_rejected_with_migration_guidance(string value)
    {
        var ex = Rejected(("Authentication:Mode", "none"), ("FeatureFlags:EnableWindowsAuthentication", value));

        Assert.Contains("Authentication__Mode", ex.Message);
    }

    [Fact]
    public void Keycloak_settings_are_validated_only_when_keycloak_is_selected()
    {
        // Garbage Keycloak section is ignored in 'none' and 'window'.
        foreach (var mode in new[] { "none", "window" })
            SurveyStartupSettings.Load(TestSupport.Config(("Authentication:Mode", mode), ("Authentication:Keycloak:Authority", "nonsense")));

        var ex = Rejected(("Authentication:Mode", "keycloak"));
        Assert.Contains("Authority", ex.Message);
        Assert.Contains("ClientId", ex.Message);
        Assert.Contains("ClientSecret", ex.Message);
    }

    [Fact]
    public void Valid_keycloak_configuration_is_accepted()
    {
        var s = SurveyStartupSettings.Load(TestSupport.Config(ValidKeycloak));

        Assert.Equal(AuthenticationMode.Keycloak, s.Mode);
        Assert.Equal("/signin-oidc", s.Keycloak.CallbackPath);
        Assert.True(s.Keycloak.RequireHttpsMetadata);
    }

    [Fact]
    public void Keycloak_authority_must_be_https_unless_explicitly_relaxed()
    {
        (string, string?)[] http = [.. ValidKeycloak.Where(v => !v.Item1.EndsWith("Authority")), ("Authentication:Keycloak:Authority", "http://kc.local/realms/survey")];

        Assert.Contains("https", Rejected(http).Message);
        SurveyStartupSettings.Load(TestSupport.Config([.. http, ("Authentication:Keycloak:RequireHttpsMetadata", "false")]));
    }

    [Fact]
    public void Startup_errors_never_echo_the_client_secret()
    {
        var ex = Rejected(("Authentication:Mode", "keycloak"), ("Authentication:Keycloak:ClientSecret", "TOP-SECRET-VALUE"));

        Assert.DoesNotContain("TOP-SECRET-VALUE", ex.Message);
    }

    [Fact]
    public void Callback_paths_must_be_local()
    {
        var ex = Rejected([.. ValidKeycloak, ("Authentication:Keycloak:CallbackPath", "//evil.example/cb")]);

        Assert.Contains("CallbackPath", ex.Message);
    }

    [Fact]
    public void Sql_persistence_requires_a_connection_string_only_when_enabled()
    {
        SurveyStartupSettings.Load(TestSupport.Config(("Authentication:Mode", "none"), ("FeatureFlags:EnableSqlPersistence", "false")));

        Assert.Contains("SurveyDatabase", Rejected(("Authentication:Mode", "none"), ("FeatureFlags:EnableSqlPersistence", "true")).Message);

        var s = SurveyStartupSettings.Load(TestSupport.Config(
            ("Authentication:Mode", "none"), ("FeatureFlags:EnableSqlPersistence", "true"),
            ("ConnectionStrings:SurveyDatabase", "Server=x;Database=y;Integrated Security=True")));
        Assert.True(s.SqlPersistenceEnabled);
    }

    [Fact]
    public void Reverse_proxy_requires_explicit_trusted_proxies_and_a_valid_path_base()
    {
        Assert.Contains("KnownProxies", Rejected(("Authentication:Mode", "none"), ("ReverseProxy:Enabled", "true")).Message);
        Assert.Contains("PathBase", Rejected(("Authentication:Mode", "none"), ("ReverseProxy:PathBase", "survey/")).Message);

        SurveyStartupSettings.Load(TestSupport.Config(
            ("Authentication:Mode", "none"), ("ReverseProxy:Enabled", "true"),
            ("ReverseProxy:KnownProxies:0", "10.0.0.5"), ("ReverseProxy:KnownNetworks:0", "10.1.0.0/16"),
            ("ReverseProxy:PathBase", "/survey")));
    }
}
