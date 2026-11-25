using System.Text.Json;
using SurveyApp.Models;
using SurveyApp.Persistence;

namespace SurveyApp.Tests;

public class SubmissionPayloadBuilderTests
{
    private readonly SurveyDefinition _survey = TestSupport.LoadSurvey();

    private SurveyAnswers Sample()
    {
        var answers = SurveyAnswers.Create(_survey);
        answers.Selections["interests"] = "health";
        answers.Ratings["service-rating"]["service-quality"] = 5;
        answers.Ratings["service-rating"]["response-speed"] = 4;
        answers.Texts["full-name"] = "محمد رافع";
        answers.Texts["comments"] = null;
        return answers;
    }

    [Fact]
    public void Response_uses_stable_ids_and_values_not_display_text()
    {
        using var doc = JsonDocument.Parse(SubmissionPayloadBuilder.BuildResponseJson(_survey, Sample()));
        var root = doc.RootElement;

        Assert.Equal("health", root.GetProperty("interests").GetString());                       // radio group: scalar
        Assert.Equal(JsonValueKind.Null, root.GetProperty("services").ValueKind);               // unanswered radio group: null
        Assert.Equal(5, root.GetProperty("service-rating").GetProperty("service-quality").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("service-rating").GetProperty("data-accuracy").ValueKind);
        Assert.Equal("", root.GetProperty("comments").GetString());
        Assert.False(root.TryGetProperty("introduction", out _)); // labels carry no answers
    }

    [Fact]
    public void Arabic_text_is_stored_readably_and_round_trips()
    {
        var json = SubmissionPayloadBuilder.BuildResponseJson(_survey, Sample());

        Assert.Contains("محمد رافع", json);   // not \u-escaped
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("محمد رافع", doc.RootElement.GetProperty("full-name").GetString());
    }

    [Fact]
    public void Output_is_deterministic_so_retries_compare_equal()
    {
        Assert.Equal(
            SubmissionPayloadBuilder.BuildResponseJson(_survey, Sample()),
            SubmissionPayloadBuilder.BuildResponseJson(_survey, Sample()));
    }

    [Fact]
    public void Definition_snapshot_preserves_the_survey_identity_and_version()
    {
        using var doc = JsonDocument.Parse(SubmissionPayloadBuilder.BuildDefinitionSnapshotJson(_survey));

        Assert.Equal(_survey.SurveyId, doc.RootElement.GetProperty("surveyId").GetString());
        Assert.Equal(_survey.Version, doc.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(_survey.Elements.Count, doc.RootElement.GetProperty("elements").GetArrayLength());
    }
}
