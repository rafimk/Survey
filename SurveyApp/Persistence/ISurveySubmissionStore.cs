using SurveyApp.Options;

namespace SurveyApp.Persistence;

public enum SubmissionOutcome
{
    /// <summary>Committed to the database (or an identical earlier attempt with the same id already was).</summary>
    Persisted,

    /// <summary>Accepted, but persistence is disabled so nothing was stored.</summary>
    AcceptedWithoutPersistence,

    /// <summary>Not stored (database unavailable, rejected, or conflicting payload). Safe to retry.</summary>
    Failed
}

/// <summary>One accepted survey response, ready to store. Identity fields come only from the trusted server-side principal.</summary>
public sealed record SubmissionRecord(
    Guid SubmissionId,
    string SurveyId,
    string SurveyVersion,
    DateTime SubmittedUtc,
    AuthenticationMode AuthenticationMode,
    string? UserIdentifier,
    string? UserName,
    string ResponseJson,
    string? DefinitionSnapshotJson);

public interface ISurveySubmissionStore
{
    /// <summary>Stores an accepted submission. Never throws for infrastructure problems; reports <see cref="SubmissionOutcome.Failed"/>.</summary>
    Task<SubmissionOutcome> SaveAsync(SubmissionRecord record, CancellationToken cancellationToken);
}
