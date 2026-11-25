using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SurveyApp.Models;
using SurveyApp.Options;
using SurveyApp.Persistence;
using SurveyApp.Security;
using SurveyApp.Services;

namespace SurveyApp.Tests;

internal static class TestSupport
{
    public static string SurveyJsonPath { get; } = FindSurveyJson();

    public static string SurveyJson => File.ReadAllText(SurveyJsonPath);

    public static SurveyDefinition LoadSurvey()
    {
        var (definition, errors) = SurveyConfigurationService.Parse(SurveyJson);
        Assert.True(definition is not null, string.Join("; ", errors));
        return definition;
    }

    public static readonly AuthenticationMode[] Modes = [AuthenticationMode.None, AuthenticationMode.Window, AuthenticationMode.Keycloak];

    /// <summary>The trusted identity a signed-in user would have in each mode (anonymous for 'none').</summary>
    public static CurrentUser SignedIn(AuthenticationMode mode) => mode switch
    {
        AuthenticationMode.Window => new(mode, true, "S-1-5-21-1", @"CONTOSO\jdoe"),
        AuthenticationMode.Keycloak => new(mode, true, "https://sso.example.internal/realms/survey|sub-123", "jdoe"),
        _ => CurrentUser.Anonymous(mode)
    };

    /// <summary>Fills every required answer of the shipped survey.</summary>
    public static void FillRequired(SurveyDefinition survey, SurveyAnswers answers)
    {
        foreach (var rating in survey.Elements.Where(e => e.Type == SurveyElementTypes.RatingMatrix))
            foreach (var row in rating.Rows) answers.Ratings[rating.Id][row.Id] = rating.Columns[0].Value;
        answers.Texts["full-name"] = "Jane Doe";
    }

    public static SurveySubmissionService CreateService(bool validation, ISurveySubmissionStore store) =>
        new(new TestOptionsMonitor<FeatureFlagsOptions>(new() { EnableFormValidation = validation }),
            store, TimeProvider.System, NullLogger<SurveySubmissionService>.Instance);

    public static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static string FindSurveyJson()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "SurveyApp", "Configuration", "survey.json");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("Could not locate SurveyApp/Configuration/survey.json.");
    }
}

internal sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue { get; set; } = value;
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

/// <summary>Records what the service hands to the store; returns a configured outcome. A fake — no database involved.</summary>
internal sealed class SpyStore(SubmissionOutcome outcome = SubmissionOutcome.Persisted) : ISurveySubmissionStore
{
    public List<SubmissionRecord> Saved { get; } = [];

    public Task<SubmissionOutcome> SaveAsync(SubmissionRecord record, CancellationToken cancellationToken)
    {
        Saved.Add(record);
        return Task.FromResult(outcome);
    }
}

/// <summary>Mimics the SQL store's idempotency contract (unique id; same payload = success, different = refused).</summary>
internal sealed class IdempotentFakeStore : ISurveySubmissionStore
{
    public Dictionary<Guid, SubmissionRecord> Rows { get; } = [];

    public Task<SubmissionOutcome> SaveAsync(SubmissionRecord record, CancellationToken cancellationToken)
    {
        if (!Rows.TryGetValue(record.SubmissionId, out var existing))
        {
            Rows[record.SubmissionId] = record;
            return Task.FromResult(SubmissionOutcome.Persisted);
        }
        return Task.FromResult(existing.ResponseJson == record.ResponseJson ? SubmissionOutcome.Persisted : SubmissionOutcome.Failed);
    }
}

/// <summary>Sets process environment variables for the duration of a test and restores them afterwards.</summary>
internal sealed class EnvScope : IDisposable
{
    private readonly Dictionary<string, string?> _previous = [];

    public EnvScope(params (string Name, string Value)[] values)
    {
        foreach (var (name, value) in values)
        {
            _previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    public void Dispose()
    {
        foreach (var (name, value) in _previous) Environment.SetEnvironmentVariable(name, value);
    }
}
