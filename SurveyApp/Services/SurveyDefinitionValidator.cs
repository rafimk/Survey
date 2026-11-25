using System.Text.RegularExpressions;
using SurveyApp.Models;

namespace SurveyApp.Services;

/// <summary>Validates a parsed <see cref="SurveyDefinition"/>; returns human-readable problems (empty when valid).</summary>
public static partial class SurveyDefinitionValidator
{
    private static readonly HashSet<string> Alignments = ["start", "center", "end"];
    private static readonly HashSet<string> Directions = ["rtl", "ltr"];

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex ColorPattern();

    public static List<string> Validate(SurveyDefinition d)
    {
        var errors = new List<string>();

        Require(d.SurveyId, "surveyId", errors);
        if (d.Version < 1) errors.Add("version must be 1 or greater.");
        Require(d.Language, "language", errors);
        if (!Directions.Contains(d.Direction)) errors.Add("direction must be 'rtl' or 'ltr'.");
        Require(d.RequiredIndicator, "requiredIndicator", errors);

        Require(d.Header.Title, "header.title", errors);
        if (!string.IsNullOrWhiteSpace(d.Header.Logo) && !IsSafeRelativePath(d.Header.Logo))
            errors.Add("header.logo must be a relative or application-rooted path (no scheme or host).");

        ValidateElements(d, errors);

        Require(d.Actions.Submit.Text, "actions.submit.text", errors);
        Require(d.Actions.Submit.LoadingText, "actions.submit.loadingText", errors);
        Require(d.Actions.Cancel.Text, "actions.cancel.text", errors);
        Require(d.Actions.Cancel.ConfirmTitle, "actions.cancel.confirmTitle", errors);
        Require(d.Actions.Cancel.ConfirmMessage, "actions.cancel.confirmMessage", errors);
        Require(d.Actions.Cancel.ConfirmText, "actions.cancel.confirmText", errors);
        Require(d.Actions.Cancel.DismissText, "actions.cancel.dismissText", errors);

        RequireAllStrings(d.Messages, "messages", errors);
        RequireAllStrings(d.Account, "account", errors);
        RequireAllStrings(d.SystemText, "systemText", errors);
        return errors;
    }

    private static void ValidateElements(SurveyDefinition d, List<string> errors)
    {
        if (d.Elements.Count == 0) errors.Add("elements must contain at least one element.");

        var seen = new HashSet<string>();
        for (var i = 0; i < d.Elements.Count; i++)
        {
            var e = d.Elements[i];
            var at = $"elements[{i}]" + (string.IsNullOrEmpty(e.Id) ? "" : $" ('{e.Id}')");

            if (!IdPattern().IsMatch(e.Id)) errors.Add($"{at}: id must be 1-64 characters of letters, digits, '-' or '_'.");
            else if (!seen.Add(e.Id)) errors.Add($"{at}: duplicate element id.");

            if (!SurveyElementTypes.All.Contains(e.Type))
            {
                errors.Add($"{at}: unsupported type '{e.Type}'. Supported: {string.Join(", ", SurveyElementTypes.All)}.");
                continue;
            }

            switch (e.Type)
            {
                case SurveyElementTypes.Label: ValidateLabel(e, at, errors); break;
                case SurveyElementTypes.CheckboxGroup: ValidateChoiceGroup(e, at, errors, allowSelectionCounts: true); break;
                case SurveyElementTypes.RadioGroup: ValidateChoiceGroup(e, at, errors, allowSelectionCounts: false); break;
                case SurveyElementTypes.RatingMatrix: ValidateRatingMatrix(e, at, errors); break;
                default: ValidateTextInput(e, at, errors); break;
            }
        }
    }

    private static void ValidateLabel(SurveyElementDefinition e, string at, List<string> errors)
    {
        Require(e.Text, $"{at}: text", errors);
        if (e.Alignment is not null && !Alignments.Contains(e.Alignment))
            errors.Add($"{at}: alignment must be start, center or end.");
        if (e.Color is not null && !ColorPattern().IsMatch(e.Color))
            errors.Add($"{at}: color must be a hex value such as #dc2626.");
        if (e.FontWeight is < 100 or > 900)
            errors.Add($"{at}: fontWeight must be between 100 and 900.");
    }

    private static void ValidateChoiceGroup(SurveyElementDefinition e, string at, List<string> errors, bool allowSelectionCounts)
    {
        Require(e.Label, $"{at}: label", errors);
        if (e.Options.Count == 0) errors.Add($"{at}: options must contain at least one option.");
        if (e.Options.Any(o => string.IsNullOrWhiteSpace(o.Value) || string.IsNullOrWhiteSpace(o.Text)))
            errors.Add($"{at}: every option needs a value and text.");
        if (e.Options.GroupBy(o => o.Value).Any(g => g.Count() > 1)) errors.Add($"{at}: duplicate option values.");
        if (!allowSelectionCounts && (e.MinSelections is not null || e.MaxSelections is not null))
            errors.Add($"{at}: minSelections/maxSelections do not apply to a radioGroup (it always allows one value).");
        if (e.MinSelections is < 0) errors.Add($"{at}: minSelections cannot be negative.");
        if (e.MaxSelections is < 1) errors.Add($"{at}: maxSelections must be at least 1.");
        if (e.MinSelections > e.MaxSelections) errors.Add($"{at}: minSelections cannot exceed maxSelections.");
        RequireMessage(e, e.Required || e.MinSelections is not null || e.MaxSelections is not null, at, errors);
    }

    private static void ValidateRatingMatrix(SurveyElementDefinition e, string at, List<string> errors)
    {
        if (e.Columns.Count < 2) errors.Add($"{at}: columns must contain at least two choices.");
        if (e.Columns.Any(c => string.IsNullOrWhiteSpace(c.Text))) errors.Add($"{at}: every column needs text.");
        if (e.Columns.GroupBy(c => c.Value).Any(g => g.Count() > 1)) errors.Add($"{at}: duplicate column values.");

        if (e.Rows.Count == 0) errors.Add($"{at}: rows must contain at least one row.");
        if (e.Rows.Any(r => !IdPattern().IsMatch(r.Id) || string.IsNullOrWhiteSpace(r.Text)))
            errors.Add($"{at}: every row needs a valid id and text.");
        if (e.Rows.GroupBy(r => r.Id).Any(g => g.Count() > 1)) errors.Add($"{at}: duplicate row ids.");
        RequireMessage(e, e.Required, at, errors);
    }

    private static void ValidateTextInput(SurveyElementDefinition e, string at, List<string> errors)
    {
        Require(e.Label, $"{at}: label", errors);
        if (e.MinLength is < 0) errors.Add($"{at}: minLength cannot be negative.");
        if (e.MaxLength is < 1 or > SurveyLimits.MaxTextLength)
            errors.Add($"{at}: maxLength must be between 1 and {SurveyLimits.MaxTextLength}.");
        if (e.MinLength > e.MaxLength) errors.Add($"{at}: minLength cannot exceed maxLength.");

        if (e.Pattern is not null)
        {
            try { _ = new Regex(e.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(250)); }
            catch (ArgumentException ex) { errors.Add($"{at}: pattern is not a valid regular expression ({ex.Message})."); }
        }

        RequireMessage(e, e.Required || e.MinLength is not null || e.MaxLength is not null || e.Pattern is not null, at, errors);
    }

    /// <summary>Elements that carry rules need a message to show when a rule fails.</summary>
    private static void RequireMessage(SurveyElementDefinition e, bool hasRules, string at, List<string> errors)
    {
        if (!hasRules) return;
        if (e.Required && string.IsNullOrWhiteSpace(e.ValidationMessage))
            errors.Add($"{at}: validationMessage is required when 'required' is true.");
        if (string.IsNullOrWhiteSpace(e.RuleMessage) && string.IsNullOrWhiteSpace(e.ValidationMessage))
            errors.Add($"{at}: ruleMessage or validationMessage is required when rules are configured.");
    }

    private static void Require(string? value, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{name} is required.");
    }

    private static void RequireAllStrings(object section, string path, List<string> errors)
    {
        foreach (var p in section.GetType().GetProperties().Where(p => p.PropertyType == typeof(string)))
        {
            var name = char.ToLowerInvariant(p.Name[0]) + p.Name[1..];
            Require((string?)p.GetValue(section), $"{path}.{name}", errors);
        }
    }

    private static bool IsSafeRelativePath(string path) =>
        !path.StartsWith("//", StringComparison.Ordinal) &&
        !path.StartsWith('\\') &&
        Uri.TryCreate(path, UriKind.Relative, out _);
}
