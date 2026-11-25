using System.ComponentModel.DataAnnotations;

namespace SurveyApp.Options;

public sealed class SurveyOptions
{
    public const string SectionName = "Survey";

    /// <summary>Path to the survey JSON; relative paths resolve against the application content root.</summary>
    [Required]
    public string ConfigurationFile { get; set; } = "Configuration/survey.json";

    /// <summary>Watch the file and apply valid edits to newly opened surveys without a restart.</summary>
    public bool ReloadOnChange { get; set; } = true;
}
