using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using SurveyApp.Options;
using SurveyApp.Persistence;

namespace SurveyApp.Tests;

/// <summary>Runs only when SURVEY_TEST_SQL_CONNECTION points at an approved test database where Database/001_CreateSurveySubmissions.sql was already applied.</summary>
public sealed class SqlIntegrationFactAttribute : FactAttribute
{
    public const string Variable = "SURVEY_TEST_SQL_CONNECTION";

    public SqlIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"Pending: live SQL Server test. Set {Variable} to an approved test database (script 001 applied) to run it.";
    }
}

public class SqlServerSubmissionStoreTests
{
    private static string Connection => Environment.GetEnvironmentVariable(SqlIntegrationFactAttribute.Variable)!;

    private static SqlServerSubmissionStore CreateStore() =>
        new(new SqlPersistenceSettings(Connection), NullLogger<SqlServerSubmissionStore>.Instance);

    private static SubmissionRecord NewRecord(string name = "محمد رافع") => new(
        Guid.NewGuid(), "sql-integration-test", "1", DateTime.UtcNow, AuthenticationMode.None, null, null,
        $$"""{"full-name":"{{name}}"}""", """{"surveyId":"sql-integration-test"}""");

    [SqlIntegrationFact]
    public async Task Arabic_answers_round_trip_and_anonymous_identity_is_null()
    {
        var record = NewRecord();
        try
        {
            Assert.Equal(SubmissionOutcome.Persisted, await CreateStore().SaveAsync(record, default));

            await using var connection = new SqlConnection(Connection);
            await connection.OpenAsync();
            await using var command = new SqlCommand(
                "SELECT ResponseJson, UserIdentifier, UserName, AuthenticationMode FROM dbo.SurveySubmissions WHERE SubmissionId = @id", connection);
            command.Parameters.AddWithValue("@id", record.SubmissionId);
            await using var reader = await command.ExecuteReaderAsync();

            Assert.True(await reader.ReadAsync());
            Assert.Equal(record.ResponseJson, reader.GetString(0));
            Assert.True(reader.IsDBNull(1));
            Assert.True(reader.IsDBNull(2));
            Assert.Equal("none", reader.GetString(3));
        }
        finally { await DeleteAsync(record.SubmissionId); }
    }

    [SqlIntegrationFact]
    public async Task Retry_is_idempotent_and_a_different_payload_under_the_same_id_is_refused()
    {
        var record = NewRecord();
        var store = CreateStore();
        try
        {
            Assert.Equal(SubmissionOutcome.Persisted, await store.SaveAsync(record, default));
            Assert.Equal(SubmissionOutcome.Persisted, await store.SaveAsync(record, default));
            Assert.Equal(SubmissionOutcome.Failed, await store.SaveAsync(record with { ResponseJson = """{"full-name":"other"}""" }, default));
        }
        finally { await DeleteAsync(record.SubmissionId); }
    }

    [Fact]
    public async Task Unreachable_database_reports_failure_not_success()
    {
        // Not a live test: nothing listens on this address, so the connection fails quickly.
        var store = new SqlServerSubmissionStore(
            new SqlPersistenceSettings("Server=tcp:127.0.0.1,1;Database=x;Integrated Security=True;Connect Timeout=2;Encrypt=False"),
            NullLogger<SqlServerSubmissionStore>.Instance);

        Assert.Equal(SubmissionOutcome.Failed, await store.SaveAsync(NewRecord(), default));
    }

    private static async Task DeleteAsync(Guid id)
    {
        await using var connection = new SqlConnection(Connection);
        await connection.OpenAsync();
        await using var command = new SqlCommand("DELETE FROM dbo.SurveySubmissions WHERE SubmissionId = @id", connection);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync();
    }
}
