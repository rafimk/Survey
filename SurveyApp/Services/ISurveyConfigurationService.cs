using SurveyApp.Models;

namespace SurveyApp.Services;

public interface ISurveyConfigurationService
{
    /// <summary>
    /// The latest valid survey definition. Capture it once when a survey is opened so the session
    /// stays consistent even if the file is edited later.
    /// </summary>
    SurveyDefinition Current { get; }
}
