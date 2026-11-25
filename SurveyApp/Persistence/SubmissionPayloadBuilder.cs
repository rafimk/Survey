using System.Text.Encodings.Web;
using System.Text.Json;
using SurveyApp.Models;

namespace SurveyApp.Persistence;

/// <summary>
/// Builds the stored JSON. Response keys are the stable element ids; option values and row ids are stable too,
/// so answers stay interpretable after survey.json changes (the definition snapshot is stored alongside).
/// Output is deterministic (survey element order) so identical answers always produce identical JSON.
/// </summary>
public static class SubmissionPayloadBuilder
{
    // Unicode text stays readable (Arabic is not \u-escaped). The JSON is stored, never rendered as HTML.
    private static readonly JavaScriptEncoder Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    private static readonly JsonSerializerOptions SnapshotOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = Encoder,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string BuildResponseJson(SurveyDefinition survey, SurveyAnswers answers)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = Encoder }))
        {
            writer.WriteStartObject();
            foreach (var element in survey.Elements)
            {
                switch (element.Type)
                {
                    case SurveyElementTypes.CheckboxGroup:
                        var selected = answers.Choices.GetValueOrDefault(element.Id) ?? [];
                        writer.WriteStartArray(element.Id);
                        foreach (var option in element.Options.Where(o => selected.Contains(o.Value)))
                            writer.WriteStringValue(option.Value);
                        writer.WriteEndArray();
                        break;

                    case SurveyElementTypes.RadioGroup:
                        // Scalar option value, or null when unanswered (checkbox groups stay arrays).
                        writer.WriteString(element.Id, answers.Selections.GetValueOrDefault(element.Id));
                        break;

                    case SurveyElementTypes.RatingMatrix:
                        var ratings = answers.Ratings.GetValueOrDefault(element.Id) ?? [];
                        writer.WriteStartObject(element.Id);
                        foreach (var row in element.Rows)
                        {
                            if (ratings.GetValueOrDefault(row.Id) is { } value) writer.WriteNumber(row.Id, value);
                            else writer.WriteNull(row.Id);
                        }
                        writer.WriteEndObject();
                        break;

                    case SurveyElementTypes.Text or SurveyElementTypes.TextArea:
                        writer.WriteString(element.Id, answers.Texts.GetValueOrDefault(element.Id) ?? "");
                        break;
                }
            }
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string BuildDefinitionSnapshotJson(SurveyDefinition survey) =>
        JsonSerializer.Serialize(survey, SnapshotOptions);
}
