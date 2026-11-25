namespace SurveyApp.Persistence;

/// <summary>Used when FeatureFlags:EnableSqlPersistence is false: touches no database and says so.</summary>
public sealed class NonPersistingSubmissionStore : ISurveySubmissionStore
{
    public Task<SubmissionOutcome> SaveAsync(SubmissionRecord record, CancellationToken cancellationToken) =>
        Task.FromResult(SubmissionOutcome.AcceptedWithoutPersistence);
}
