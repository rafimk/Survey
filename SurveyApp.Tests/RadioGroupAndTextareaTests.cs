using System.Text.Json;
using System.Text.Json.Nodes;
using SurveyApp.Models;
using SurveyApp.Persistence;
using SurveyApp.Services;

namespace SurveyApp.Tests;

public class RadioGroupAndTextareaTests
{
    private readonly SurveyDefinition _survey = TestSupport.LoadSurvey();

    [Fact]
    public void Shipped_survey_uses_radio_groups_and_textareas_with_unchanged_labels_options_and_order()
    {
        var interests = _survey.Elements.Single(e => e.Id == "interests");
        var services = _survey.Elements.Single(e => e.Id == "services");

        Assert.Equal(SurveyElementTypes.RadioGroup, interests.Type);
        Assert.Equal(SurveyElementTypes.RadioGroup, services.Type);
        Assert.Equal(["technology", "education", "health"], interests.Options.Select(o => o.Value));
        Assert.Equal(["technical-support", "consulting", "training", "development", "maintenance"], services.Options.Select(o => o.Value));
        Assert.Equal("المجالات التي تهمك", interests.Label);
        Assert.Equal("الخدمات التي استفدت منها", services.Label);

        Assert.Equal(SurveyElementTypes.TextArea, _survey.Elements.Single(e => e.Id == "full-name").Type);
        Assert.Equal(SurveyElementTypes.TextArea, _survey.Elements.Single(e => e.Id == "comments").Type);
        Assert.Equal(2, _survey.Version); // bumped: the response shape of two elements changed
    }

    [Fact]
    public void Radio_groups_start_unselected_and_are_independent()
    {
        var answers = SurveyAnswers.Create(_survey);
        Assert.Null(answers.Selections["interests"]);
        Assert.Null(answers.Selections["services"]);

        answers.Selections["interests"] = "health";
        answers.Selections["services"] = "training";
        answers.Selections["interests"] = "education";      // replaces within its own group only

        Assert.Equal("education", answers.Selections["interests"]);
        Assert.Equal("training", answers.Selections["services"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Radio_group_rejects_unknown_option_ids_regardless_of_the_validation_flag(bool enforce)
    {
        var answers = SurveyAnswers.Create(_survey);
        answers.Selections["services"] = "technology";       // valid id, but from the other group

        var errors = SurveyAnswerValidator.Validate(_survey, answers, enforce);

        Assert.Contains("services", errors.Keys);
    }

    [Fact]
    public void Required_radio_group_is_enforced_only_when_validation_is_enabled()
    {
        var survey = Mutate(root =>
        {
            var interests = root["elements"]![1]!;
            interests["required"] = true;
            interests["validationMessage"] = "اختر مجالاً.";
        });
        var answers = SurveyAnswers.Create(survey);
        TestSupport.FillRequired(survey, answers);

        Assert.Equal("اختر مجالاً.", SurveyAnswerValidator.Validate(survey, answers, enforceRules: true)["interests"]);
        Assert.Empty(SurveyAnswerValidator.Validate(survey, answers, enforceRules: false));

        answers.Selections["interests"] = "health";
        Assert.Empty(SurveyAnswerValidator.Validate(survey, answers, enforceRules: true));
    }

    [Fact]
    public void Response_json_has_scalars_for_radio_groups_and_strings_for_both_textareas()
    {
        var answers = SurveyAnswers.Create(_survey);
        answers.Selections["interests"] = "technology";
        answers.Texts["full-name"] = "محمد رافع";
        answers.Texts["comments"] = "ملاحظة";

        using var doc = JsonDocument.Parse(SubmissionPayloadBuilder.BuildResponseJson(_survey, answers));
        var root = doc.RootElement;

        Assert.Equal(JsonValueKind.String, root.GetProperty("interests").ValueKind);
        Assert.Equal("technology", root.GetProperty("interests").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("services").ValueKind);
        Assert.Equal("محمد رافع", root.GetProperty("full-name").GetString());
        Assert.Equal("ملاحظة", root.GetProperty("comments").GetString());
    }

    // ---- checkbox groups keep working for other configured elements ----

    private SurveyDefinition SurveyWithCheckbox() => Mutate(root =>
        root["elements"]!.AsArray().Insert(1, JsonNode.Parse("""
            { "id": "topics", "type": "checkboxGroup", "label": "مواضيع",
              "options": [ { "value": "a", "text": "أ" }, { "value": "b", "text": "ب" }, { "value": "c", "text": "ج" } ] }
            """)));

    [Fact]
    public void Checkbox_groups_still_allow_multiple_selections_and_are_stored_as_arrays()
    {
        var survey = SurveyWithCheckbox();
        var answers = SurveyAnswers.Create(survey);
        TestSupport.FillRequired(survey, answers);
        answers.Choices["topics"].UnionWith(["c", "a"]);

        Assert.Empty(SurveyAnswerValidator.Validate(survey, answers, enforceRules: true));
        using var doc = JsonDocument.Parse(SubmissionPayloadBuilder.BuildResponseJson(survey, answers));
        Assert.Equal(["a", "c"], doc.RootElement.GetProperty("topics").EnumerateArray().Select(e => e.GetString()));

        answers.Choices["topics"].Add("zzz");
        Assert.Contains("topics", SurveyAnswerValidator.Validate(survey, answers, enforceRules: false).Keys);
    }

    // ---- configuration validation ----

    [Fact]
    public void Radio_group_definition_rules_are_checked()
    {
        var withCounts = SurveyConfigurationService.Parse(MutateJson(r => r["elements"]![1]!["maxSelections"] = 2));
        Assert.Null(withCounts.Definition);
        Assert.Contains(withCounts.Errors, e => e.Contains("do not apply to a radioGroup"));

        var duplicate = SurveyConfigurationService.Parse(MutateJson(r =>
            r["elements"]![1]!["options"]!.AsArray().Add(JsonNode.Parse("""{ "value": "health", "text": "x" }"""))));
        Assert.Null(duplicate.Definition);
        Assert.Contains(duplicate.Errors, e => e.Contains("duplicate option values"));
    }

    private static string MutateJson(Action<JsonNode> change)
    {
        var root = JsonNode.Parse(TestSupport.SurveyJson)!;
        change(root);
        return root.ToJsonString();
    }

    private static SurveyDefinition Mutate(Action<JsonNode> change)
    {
        var (definition, errors) = SurveyConfigurationService.Parse(MutateJson(change));
        Assert.True(definition is not null, string.Join("; ", errors));
        return definition;
    }
}
