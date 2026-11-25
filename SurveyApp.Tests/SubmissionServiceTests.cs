using SurveyApp.Models;
using SurveyApp.Options;
using SurveyApp.Persistence;
using SurveyApp.Security;
using SurveyApp.Services;

namespace SurveyApp.Tests;

/// <summary>Service-level tests with fake stores (no SQL Server, no identity provider).</summary>
public class SubmissionServiceTests
{
    private readonly SurveyDefinition _survey = TestSupport.LoadSurvey();

    /// <summary>All 12 combinations: 3 authentication modes × validation on/off × SQL persistence on/off.</summary>
    public static TheoryData<AuthenticationMode, bool, bool> AllCombinations()
    {
        var data = new TheoryData<AuthenticationMode, bool, bool>();
        foreach (var mode in TestSupport.Modes)
            foreach (var validation in new[] { true, false })
                foreach (var sql in new[] { true, false })
                    data.Add(mode, validation, sql);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public async Task Signed_in_user_submissions_follow_validation_and_persistence_flags(
        AuthenticationMode mode, bool validation, bool sql)
    {
        // SQL on  = a store that persists; SQL off = a store that reports "accepted without persistence".
        var store = new SpyStore(sql ? SubmissionOutcome.Persisted : SubmissionOutcome.AcceptedWithoutPersistence);
        var service = TestSupport.CreateService(validation, store);
        var user = TestSupport.SignedIn(mode);

        // Empty answers: rejected (and never stored) only when validation is on.
        var empty = await service.SubmitAsync(_survey, SurveyAnswers.Create(_survey), Guid.NewGuid(), user);
        Assert.Equal(!validation, empty.Succeeded);
        Assert.Equal(validation ? 0 : 1, store.Saved.Count);

        // Complete answers: always accepted, stored once more, with the message matching the persistence state.
        var complete = SurveyAnswers.Create(_survey);
        TestSupport.FillRequired(_survey, complete);
        var result = await service.SubmitAsync(_survey, complete, Guid.NewGuid(), user);

        Assert.True(result.Succeeded);
        Assert.Equal(sql ? _survey.Messages.SubmissionSaved : _survey.Messages.SubmissionCompleted, result.Message);
        var record = store.Saved[^1];
        Assert.Equal(mode, record.AuthenticationMode);
        Assert.Equal(user.Identifier, record.UserIdentifier); // null for anonymous, trusted value otherwise
        Assert.Equal(user.UserName, record.UserName);
    }

    [Theory]
    [InlineData(AuthenticationMode.Window, true)]
    [InlineData(AuthenticationMode.Window, false)]
    [InlineData(AuthenticationMode.Keycloak, true)]
    [InlineData(AuthenticationMode.Keycloak, false)]
    public async Task Protected_modes_reject_anonymous_submissions_without_touching_the_store(AuthenticationMode mode, bool validation)
    {
        var store = new SpyStore();
        var service = TestSupport.CreateService(validation, store);
        var answers = SurveyAnswers.Create(_survey);
        TestSupport.FillRequired(_survey, answers);

        var result = await service.SubmitAsync(_survey, answers, Guid.NewGuid(), CurrentUser.Anonymous(mode));

        Assert.False(result.Succeeded);
        Assert.Equal(_survey.Messages.AuthenticationRequired, result.Message);
        Assert.Empty(store.Saved);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Mode_none_accepts_anonymous_submissions_with_null_identity(bool validation)
    {
        var store = new SpyStore(SubmissionOutcome.AcceptedWithoutPersistence);
        var answers = SurveyAnswers.Create(_survey);
        TestSupport.FillRequired(_survey, answers);

        var result = await TestSupport.CreateService(validation, store)
            .SubmitAsync(_survey, answers, Guid.NewGuid(), CurrentUser.Anonymous(AuthenticationMode.None));

        Assert.True(result.Succeeded);
        Assert.Null(store.Saved.Single().UserIdentifier);
        Assert.Null(store.Saved.Single().UserName);
    }

    [Fact]
    public async Task Integrity_failures_are_rejected_before_storing_even_with_validation_disabled()
    {
        var store = new SpyStore();
        var answers = SurveyAnswers.Create(_survey);
        answers.Selections["interests"] = "not-an-option";

        var result = await TestSupport.CreateService(validation: false, store)
            .SubmitAsync(_survey, answers, Guid.NewGuid(), CurrentUser.Anonymous(AuthenticationMode.None));

        Assert.False(result.Succeeded);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task Database_failure_is_never_reported_as_success()
    {
        var service = TestSupport.CreateService(validation: false, new SpyStore(SubmissionOutcome.Failed));

        var result = await service.SubmitAsync(
            _survey, SurveyAnswers.Create(_survey), Guid.NewGuid(), CurrentUser.Anonymous(AuthenticationMode.None));

        Assert.False(result.Succeeded);
        Assert.Equal(_survey.Messages.SubmissionPersistenceFailure, result.Message);
    }

    [Fact]
    public async Task Retry_with_the_same_submission_id_does_not_duplicate_and_a_different_payload_is_refused()
    {
        var store = new IdempotentFakeStore();
        var service = TestSupport.CreateService(validation: false, store);
        var user = CurrentUser.Anonymous(AuthenticationMode.None);
        var id = Guid.NewGuid();
        var answers = SurveyAnswers.Create(_survey);
        answers.Texts["full-name"] = "Jane";

        Assert.True((await service.SubmitAsync(_survey, answers, id, user)).Succeeded);
        Assert.True((await service.SubmitAsync(_survey, answers, id, user)).Succeeded); // identical retry
        Assert.Single(store.Rows);

        answers.Texts["full-name"] = "Someone else";
        Assert.False((await service.SubmitAsync(_survey, answers, id, user)).Succeeded);  // same id, different payload
        Assert.Single(store.Rows);
    }

    [Fact]
    public async Task Non_persisting_store_reports_accepted_without_persistence()
    {
        var outcome = await new NonPersistingSubmissionStore().SaveAsync(default!, CancellationToken.None);

        Assert.Equal(SubmissionOutcome.AcceptedWithoutPersistence, outcome);
    }
}
