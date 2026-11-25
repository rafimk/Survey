namespace SurveyApp.Options;

/// <summary>The three mutually exclusive authentication modes (config values: none, window, keycloak).</summary>
public enum AuthenticationMode
{
    None,
    Window,
    Keycloak
}

public static class AuthenticationModeParser
{
    public const string AllowedValues = "none, window, keycloak";

    /// <summary>Case-insensitive, exact names only. Anything else (including empty) is rejected, never defaulted.</summary>
    public static bool TryParse(string? value, out AuthenticationMode mode)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "none": mode = AuthenticationMode.None; return true;
            case "window": mode = AuthenticationMode.Window; return true;
            case "keycloak": mode = AuthenticationMode.Keycloak; return true;
            default: mode = default; return false;
        }
    }

    /// <summary>The lowercase configuration/database spelling of the mode.</summary>
    public static string ToConfigValue(this AuthenticationMode mode) => mode.ToString().ToLowerInvariant();
}
