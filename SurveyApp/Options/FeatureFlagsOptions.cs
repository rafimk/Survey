namespace SurveyApp.Options;

public sealed class FeatureFlagsOptions
{
    public const string SectionName = "FeatureFlags";

    /// <summary>Enforce survey-specific rules (required, length, pattern, selection counts). Reloadable.</summary>
    public bool EnableFormValidation { get; set; } = true;

    /// <summary>Persist accepted submissions to SQL Server. Evaluated at startup; changing it needs a restart.</summary>
    public bool EnableSqlPersistence { get; set; }
}
