using Microsoft.Extensions.Options;
using SurveyApp.Models;
using SurveyApp.Options;
using SurveyApp.Persistence;
using SurveyApp.Security;

namespace SurveyApp.Services;

public sealed record SubmissionResult(bool Succeeded, string Message, IReadOnlyDictionary<string, string> Errors);

public interface ISurveySubmissionService
{
    /// <summary>
    /// Authoritative server-side handling of a submission against the definition the session was opened with.
    /// <paramref name="submissionId"/> is issued by the server per submission attempt and reused on retries.
    /// </summary>
    Task<SubmissionResult> SubmitAsync(
        SurveyDefinition survey, SurveyAnswers answers, Guid submissionId, CurrentUser user,
        CancellationToken cancellationToken = default);
}

public sealed class SurveySubmissionService(
    IOptionsMonitor<FeatureFlagsOptions> flags,
    ISurveySubmissionStore store,
    TimeProvider clock,
    ILogger<SurveySubmissionService> logger) : ISurveySubmissionService
{
    private static readonly IReadOnlyDictionary<string, string> NoErrors = new Dictionary<string, string>();

    public async Task<SubmissionResult> SubmitAsync(
        SurveyDefinition survey, SurveyAnswers answers, Guid submissionId, CurrentUser user,
        CancellationToken cancellationToken = default)
    {
        // 1. Authorization: every mode except 'none' needs a trusted authenticated identity.
        if (user.Mode != AuthenticationMode.None && !user.IsAuthenticated)
        {
            logger.LogWarning("Rejected a survey submission without an authenticated identity (mode {Mode}).", user.Mode);
            return Fail(survey.Messages.AuthenticationRequired);
        }

        // 2. Integrity checks always run; survey rules only when validation is enabled.
        var errors = SurveyAnswerValidator.Validate(survey, answers, flags.CurrentValue.EnableFormValidation);
        if (errors.Count > 0)
        {
            return new SubmissionResult(false, survey.Messages.ValidationSummary, errors);
        }

        // 3. Hand the accepted response to the store (database or non-persisting).
        var record = new SubmissionRecord(
            submissionId,
            survey.SurveyId,
            survey.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            clock.GetUtcNow().UtcDateTime,
            user.Mode,
            user.Identifier,
            user.UserName,
            SubmissionPayloadBuilder.BuildResponseJson(survey, answers),
            SubmissionPayloadBuilder.BuildDefinitionSnapshotJson(survey));

        var outcome = await store.SaveAsync(record, cancellationToken);

        // Response contents are never logged.
        logger.LogInformation("Survey {SurveyId} v{Version} submission {SubmissionId}: {Outcome} (mode {Mode}, user {User}).",
            survey.SurveyId, survey.Version, submissionId, outcome, user.Mode, user.UserName ?? "anonymous");

        return outcome switch
        {
            SubmissionOutcome.Persisted => new SubmissionResult(true, survey.Messages.SubmissionSaved, NoErrors),
            SubmissionOutcome.AcceptedWithoutPersistence => new SubmissionResult(true, survey.Messages.SubmissionCompleted, NoErrors),
            _ => Fail(survey.Messages.SubmissionPersistenceFailure)
        };
    }

    private static SubmissionResult Fail(string message) => new(false, message, NoErrors);
}
