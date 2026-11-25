using System.Text.Json.Nodes;
using SurveyApp.Services;

namespace SurveyApp.Tests;

public class SurveyDefinitionTests
{
    [Fact]
    public void Shipped_survey_json_is_valid_and_complete()
    {
        var survey = TestSupport.LoadSurvey();

        Assert.Equal(9, survey.Elements.Count);
        Assert.Equal(10, survey.Elements.Single(e => e.Id == "service-rating").Rows.Count);
        Assert.Equal("#dc2626", survey.Elements.Last().Color);
        Assert.Equal("thank-you", survey.Elements.Last().Id);
    }

    [Fact]
    public void Invalid_json_is_reported()
    {
        var (definition, errors) = SurveyConfigurationService.Parse("{ not json");

        Assert.Null(definition);
        Assert.Contains(errors, e => e.StartsWith("Invalid JSON"));
    }

    [Theory]
    [InlineData("elements[1].id", "introduction", "duplicate element id")]
    [InlineData("elements[0].type", "script", "unsupported type")]
    [InlineData("elements[8].color", "red;background:url(x)", "color must be a hex")]
    [InlineData("elements[8].fontWeight", 5000, "fontWeight")]
    [InlineData("elements[7].pattern", "([", "regular expression")]
    [InlineData("header.logo", "https://evil.example/x.png", "header.logo")]
    [InlineData("header.logo", "//evil.example/x.png", "header.logo")]
    [InlineData("direction", "sideways", "direction")]
    [InlineData("messages.accessDenied", "", "messages.accessDenied")]
    [InlineData("elements[6].validationMessage", "", "validationMessage is required")]
    public void Invalid_definitions_are_rejected(string path, object value, string expectedError)
    {
        var json = Mutate(path, value);

        var (definition, errors) = SurveyConfigurationService.Parse(json);

        Assert.Null(definition);
        Assert.Contains(errors, e => e.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Unknown_properties_are_rejected_to_catch_typos()
    {
        var (definition, errors) = SurveyConfigurationService.Parse(Mutate("header.titel", "x"));

        Assert.Null(definition);
        Assert.NotEmpty(errors);
    }

    /// <summary>Sets a dotted/indexed path such as <c>elements[3].color</c> in the shipped JSON.</summary>
    private static string Mutate(string path, object value)
    {
        JsonNode root = JsonNode.Parse(TestSupport.SurveyJson)!;
        var node = root;
        var parts = path.Split('.');
        foreach (var part in parts[..^1]) node = Step(node, part);
        node[parts[^1]] = value switch
        {
            string s => JsonValue.Create(s),
            int i => JsonValue.Create(i),
            _ => throw new ArgumentException("Unsupported test value.")
        };
        return root.ToJsonString();
    }

    private static JsonNode Step(JsonNode node, string part)
    {
        var bracket = part.IndexOf('[');
        if (bracket < 0) return node[part]!;
        var index = int.Parse(part[(bracket + 1)..^1]);
        return node[part[..bracket]]![index]!;
    }
}
