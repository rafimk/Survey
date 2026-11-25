using SurveyApp.Models;
using SurveyApp.Services;

namespace SurveyApp.Tests;

public class SurveyAnswerValidatorTests
{
    private readonly SurveyDefinition _survey = TestSupport.LoadSurvey();

    [Fact]
    public void Enforced_empty_answers_report_required_fields_with_configured_messages()
    {
        var errors = SurveyAnswerValidator.Validate(_survey, SurveyAnswers.Create(_survey), enforceRules: true);

        Assert.Equal(["full-name", "service-rating"], errors.Keys.Order());
        Assert.Equal("يرجى إدخال الاسم الكامل.", errors["full-name"]);
        Assert.Equal("يرجى تقييم جميع البنود المطلوبة.", errors["service-rating"]);
    }

    [Fact]
    public void Enforced_complete_answers_are_valid()
    {
        var answers = SurveyAnswers.Create(_survey);
        TestSupport.FillRequired(_survey, answers);

        Assert.Empty(SurveyAnswerValidator.Validate(_survey, answers, enforceRules: true));
    }

    [Fact]
    public void Partially_rated_matrix_is_invalid_when_enforced()
    {
        var answers = SurveyAnswers.Create(_survey);
        TestSupport.FillRequired(_survey, answers);
        answers.Ratings["service-rating"]["data-accuracy"] = null;

        Assert.Contains("service-rating", SurveyAnswerValidator.Validate(_survey, answers, enforceRules: true).Keys);
    }

    [Fact]
    public void Length_rule_uses_rule_message()
    {
        var answers = SurveyAnswers.Create(_survey);
        TestSupport.FillRequired(_survey, answers);
        answers.Texts["comments"] = new string('x', 501);

        var errors = SurveyAnswerValidator.Validate(_survey, answers, enforceRules: true);

        Assert.Equal("يجب ألا تتجاوز الملاحظات 500 حرف.", errors["comments"]);
    }

    [Fact]
    public void Disabled_validation_allows_empty_answers_and_survey_rules()
    {
        var answers = SurveyAnswers.Create(_survey);
        answers.Texts["comments"] = new string('x', 501); // beyond the survey rule, within the hard cap

        Assert.Empty(SurveyAnswerValidator.Validate(_survey, answers, enforceRules: false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Integrity_checks_run_even_when_validation_is_disabled(bool enforce)
    {
        var tampered = SurveyAnswers.Create(_survey);
        tampered.Selections["interests"] = "not-an-option";
        tampered.Ratings["service-rating"]["service-quality"] = 99;
        tampered.Texts["full-name"] = new string('x', SurveyLimits.MaxTextLength + 1);

        var errors = SurveyAnswerValidator.Validate(_survey, tampered, enforce);

        Assert.Equal(["full-name", "interests", "service-rating"], errors.Keys.Order());
        Assert.All(errors.Values, m => Assert.Equal(_survey.Messages.SubmissionFailure, m));
    }
}
