namespace SurveyApp.Models;

/// <summary>Answers for one survey session, keyed by element id (and row id for rating matrices).</summary>
public sealed class SurveyAnswers
{
    /// <summary>checkboxGroup: zero or more selected option values.</summary>
    public Dictionary<string, HashSet<string>> Choices { get; } = [];

    /// <summary>radioGroup: the single selected option value, or null when unanswered.</summary>
    public Dictionary<string, string?> Selections { get; } = [];
    public Dictionary<string, string?> Texts { get; } = [];
    public Dictionary<string, Dictionary<string, int?>> Ratings { get; } = [];

    public static SurveyAnswers Create(SurveyDefinition definition)
    {
        var answers = new SurveyAnswers();
        foreach (var element in definition.Elements)
        {
            switch (element.Type)
            {
                case SurveyElementTypes.CheckboxGroup:
                    answers.Choices[element.Id] = [];
                    break;
                case SurveyElementTypes.RadioGroup:
                    answers.Selections[element.Id] = null;
                    break;
                case SurveyElementTypes.Text or SurveyElementTypes.TextArea:
                    answers.Texts[element.Id] = null;
                    break;
                case SurveyElementTypes.RatingMatrix:
                    answers.Ratings[element.Id] = element.Rows.ToDictionary(r => r.Id, _ => (int?)null);
                    break;
            }
        }
        return answers;
    }
}
