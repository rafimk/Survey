using System.Text.RegularExpressions;
using SurveyApp.Models;

namespace SurveyApp.Services;

/// <summary>
/// Single place for answer validation. Integrity checks (known option values, payload size) always run;
/// survey rules (required, length, pattern, selection counts) run only when <c>enforceRules</c> is true.
/// </summary>
public static class SurveyAnswerValidator
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    /// <returns>Element id → message. Empty when valid.</returns>
    public static IReadOnlyDictionary<string, string> Validate(
        SurveyDefinition survey, SurveyAnswers answers, bool enforceRules)
    {
        var errors = new Dictionary<string, string>();

        foreach (var e in survey.Elements)
        {
            var error = e.Type switch
            {
                SurveyElementTypes.CheckboxGroup => CheckChoices(survey, e, answers, enforceRules),
                SurveyElementTypes.RadioGroup => CheckSelection(survey, e, answers, enforceRules),
                SurveyElementTypes.RatingMatrix => CheckRatings(survey, e, answers, enforceRules),
                SurveyElementTypes.Text or SurveyElementTypes.TextArea => CheckText(survey, e, answers, enforceRules),
                _ => null
            };
            if (error is not null) errors[e.Id] = error;
        }
        return errors;
    }

    private static string? CheckChoices(SurveyDefinition s, SurveyElementDefinition e, SurveyAnswers a, bool enforce)
    {
        var selected = a.Choices.GetValueOrDefault(e.Id) ?? [];
        var allowed = e.Options.Select(o => o.Value).ToHashSet();
        if (!selected.IsSubsetOf(allowed)) return s.Messages.SubmissionFailure;
        if (!enforce) return null;

        if (e.Required && selected.Count == 0) return RequiredMessage(s, e);
        if (selected.Count < (e.MinSelections ?? 0) || selected.Count > (e.MaxSelections ?? int.MaxValue))
            return RuleMessage(s, e);
        return null;
    }

    /// <summary>Single selection: structurally one known option or nothing. "Required" applies only when rules are enforced.</summary>
    private static string? CheckSelection(SurveyDefinition s, SurveyElementDefinition e, SurveyAnswers a, bool enforce)
    {
        var selected = a.Selections.GetValueOrDefault(e.Id);
        if (selected is not null && !e.Options.Any(o => o.Value == selected)) return s.Messages.SubmissionFailure;
        if (enforce && e.Required && selected is null) return RequiredMessage(s, e);
        return null;
    }

    private static string? CheckRatings(SurveyDefinition s, SurveyElementDefinition e, SurveyAnswers a, bool enforce)
    {
        var values = a.Ratings.GetValueOrDefault(e.Id) ?? [];
        var rowIds = e.Rows.Select(r => r.Id).ToHashSet();
        var allowed = e.Columns.Select(c => c.Value).ToHashSet();

        if (values.Keys.Any(k => !rowIds.Contains(k)) ||
            values.Values.Any(v => v is not null && !allowed.Contains(v.Value)))
            return s.Messages.SubmissionFailure;

        if (enforce && e.Required && e.Rows.Any(r => values.GetValueOrDefault(r.Id) is null))
            return RequiredMessage(s, e);
        return null;
    }

    private static string? CheckText(SurveyDefinition s, SurveyElementDefinition e, SurveyAnswers a, bool enforce)
    {
        var text = a.Texts.GetValueOrDefault(e.Id);
        if (text is { Length: > SurveyLimits.MaxTextLength }) return s.Messages.SubmissionFailure;
        if (!enforce) return null;

        if (string.IsNullOrWhiteSpace(text))
            return e.Required ? RequiredMessage(s, e) : null;

        var length = text.Trim().Length;
        if (length < (e.MinLength ?? 0) || length > (e.MaxLength ?? int.MaxValue)) return RuleMessage(s, e);

        if (e.Pattern is not null)
        {
            try
            {
                if (!Regex.IsMatch(text, e.Pattern, RegexOptions.None, RegexTimeout)) return RuleMessage(s, e);
            }
            catch (RegexMatchTimeoutException)
            {
                return RuleMessage(s, e);
            }
        }
        return null;
    }

    private static string RequiredMessage(SurveyDefinition s, SurveyElementDefinition e) =>
        e.ValidationMessage ?? s.Messages.ValidationSummary;

    private static string RuleMessage(SurveyDefinition s, SurveyElementDefinition e) =>
        e.RuleMessage ?? e.ValidationMessage ?? s.Messages.ValidationSummary;
}
