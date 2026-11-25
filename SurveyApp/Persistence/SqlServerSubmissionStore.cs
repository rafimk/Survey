using System.Data;
using Microsoft.Data.SqlClient;

namespace SurveyApp.Persistence;

public sealed record SqlPersistenceSettings(string ConnectionString);

/// <summary>
/// Writes one row per accepted submission with parameterized SQL. A single INSERT is atomic, so no explicit
/// transaction is needed. The connection is opened and disposed per call. Failures are logged without response
/// contents or credentials and reported as <see cref="SubmissionOutcome.Failed"/> — never as success.
/// </summary>
public sealed class SqlServerSubmissionStore(SqlPersistenceSettings settings, ILogger<SqlServerSubmissionStore> logger)
    : ISurveySubmissionStore
{
    private const int CommandTimeoutSeconds = 15;
    private const int DuplicateKeyViolation = 2627;
    private const int DuplicateUniqueIndex = 2601;

    private const string InsertSql = """
        INSERT INTO dbo.SurveySubmissions
            (SubmissionId, SurveyId, SurveyVersion, SubmittedUtc, AuthenticationMode,
             UserIdentifier, UserName, ResponseJson, DefinitionSnapshotJson)
        VALUES
            (@SubmissionId, @SurveyId, @SurveyVersion, @SubmittedUtc, @AuthenticationMode,
             @UserIdentifier, @UserName, @ResponseJson, @DefinitionSnapshotJson);
        """;

    private const string SelectExistingSql =
        "SELECT ResponseJson FROM dbo.SurveySubmissions WHERE SubmissionId = @SubmissionId;";

    public async Task<SubmissionOutcome> SaveAsync(SubmissionRecord record, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            try
            {
                await using var insert = CreateInsert(connection, record);
                await insert.ExecuteNonQueryAsync(cancellationToken);
                logger.LogInformation("Stored survey submission {SubmissionId}.", record.SubmissionId);
                return SubmissionOutcome.Persisted;
            }
            catch (SqlException ex) when (ex.Number is DuplicateKeyViolation or DuplicateUniqueIndex)
            {
                return await ResolveDuplicateAsync(connection, record, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or TimeoutException or IOException)
        {
            // Type and SQL error number only: messages can echo server names, and nothing may echo answers.
            logger.LogError("Storing survey submission {SubmissionId} failed ({ExceptionType}, SQL error {SqlNumber}).",
                record.SubmissionId, ex.GetType().Name, (ex as SqlException)?.Number);
            return SubmissionOutcome.Failed;
        }
    }

    /// <summary>A retry of an already stored submission is idempotent; a different payload under the same id is refused.</summary>
    private async Task<SubmissionOutcome> ResolveDuplicateAsync(
        SqlConnection connection, SubmissionRecord record, CancellationToken cancellationToken)
    {
        await using var select = new SqlCommand(SelectExistingSql, connection) { CommandTimeout = CommandTimeoutSeconds };
        select.Parameters.Add("@SubmissionId", SqlDbType.UniqueIdentifier).Value = record.SubmissionId;
        var existing = (string?)await select.ExecuteScalarAsync(cancellationToken);

        if (string.Equals(existing, record.ResponseJson, StringComparison.Ordinal))
        {
            logger.LogInformation("Survey submission {SubmissionId} was already stored; treating the retry as success.", record.SubmissionId);
            return SubmissionOutcome.Persisted;
        }

        logger.LogWarning("Survey submission {SubmissionId} already exists with a different payload; refusing to overwrite.", record.SubmissionId);
        return SubmissionOutcome.Failed;
    }

    private static SqlCommand CreateInsert(SqlConnection connection, SubmissionRecord r)
    {
        var command = new SqlCommand(InsertSql, connection) { CommandTimeout = CommandTimeoutSeconds };
        var p = command.Parameters;
        p.Add("@SubmissionId", SqlDbType.UniqueIdentifier).Value = r.SubmissionId;
        p.Add("@SurveyId", SqlDbType.NVarChar, 200).Value = r.SurveyId;
        p.Add("@SurveyVersion", SqlDbType.NVarChar, 100).Value = r.SurveyVersion;
        p.Add(new SqlParameter("@SubmittedUtc", SqlDbType.DateTime2) { Scale = 3, Value = r.SubmittedUtc });
        p.Add("@AuthenticationMode", SqlDbType.NVarChar, 20).Value = r.AuthenticationMode.ToString().ToLowerInvariant();
        p.Add("@UserIdentifier", SqlDbType.NVarChar, 512).Value = (object?)r.UserIdentifier ?? DBNull.Value;
        p.Add("@UserName", SqlDbType.NVarChar, 512).Value = (object?)r.UserName ?? DBNull.Value;
        p.Add("@ResponseJson", SqlDbType.NVarChar, -1).Value = r.ResponseJson;
        p.Add("@DefinitionSnapshotJson", SqlDbType.NVarChar, -1).Value = (object?)r.DefinitionSnapshotJson ?? DBNull.Value;
        return command;
    }
}
