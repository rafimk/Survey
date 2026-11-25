namespace SurveyApp.Models;

/// <summary>Supported element types (the only values allowed for <see cref="SurveyElementDefinition.Type"/>).</summary>
public static class SurveyElementTypes
{
    public const string Label = "label";
    public const string CheckboxGroup = "checkboxGroup";
    public const string RadioGroup = "radioGroup";
    public const string RatingMatrix = "ratingMatrix";
    public const string Text = "text";
    public const string TextArea = "textarea";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Label, CheckboxGroup, RadioGroup, RatingMatrix, Text, TextArea
    };
}

/// <summary>Limits that apply regardless of the validation feature flag (payload safety).</summary>
public static class SurveyLimits
{
    public const int MaxTextLength = 4000;
}

/// <summary>Complete survey content, loaded from Configuration/survey.json.</summary>
public sealed class SurveyDefinition
{
    public string SurveyId { get; set; } = "";
    public int Version { get; set; } = 1;
    public string Language { get; set; } = "ar";
    public string Direction { get; set; } = "rtl";
    public string RequiredIndicator { get; set; } = "*";
    public SurveyHeaderDefinition Header { get; set; } = new();
    public List<SurveyElementDefinition> Elements { get; set; } = [];
    public SurveyActionsDefinition Actions { get; set; } = new();
    public SurveyMessagesDefinition Messages { get; set; } = new();
    public SurveyAccountDefinition Account { get; set; } = new();
    public SurveySystemTextDefinition SystemText { get; set; } = new();
}

public sealed class SurveyHeaderDefinition
{
    public string? Logo { get; set; }
    public string? LogoAlt { get; set; }
    public string Title { get; set; } = "";
    public string? Heading { get; set; }
    public string? Subtitle { get; set; }
}

/// <summary>
/// One survey element. A single flat shape is used for all types; the validator checks which
/// properties each <see cref="Type"/> needs.
/// </summary>
public sealed class SurveyElementDefinition
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";

    // label
    public string? Text { get; set; }
    public string? Alignment { get; set; }   // start | center | end
    public string? Color { get; set; }       // #rgb or #rrggbb
    public int? FontWeight { get; set; }     // 100..900

    // input elements
    public string? Label { get; set; }
    public string? Placeholder { get; set; }
    public bool Required { get; set; }
    public string? ValidationMessage { get; set; }   // shown when a required rule fails
    public string? RuleMessage { get; set; }         // shown when a length/pattern/selection rule fails

    // text / textarea
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? Pattern { get; set; }

    // checkboxGroup / radioGroup (radio: exactly zero or one value; min/maxSelections not applicable)
    public List<SurveyOptionDefinition> Options { get; set; } = [];
    public int? MinSelections { get; set; }
    public int? MaxSelections { get; set; }

    // ratingMatrix
    public List<SurveyColumnDefinition> Columns { get; set; } = [];
    public List<SurveyRowDefinition> Rows { get; set; } = [];
    public string? IndexHeading { get; set; }   // omit to hide the row-number column
    public string? ItemHeading { get; set; }
}

/// <summary>Stored <see cref="Value"/> is independent of the displayed <see cref="Text"/>.</summary>
public sealed class SurveyOptionDefinition
{
    public string Value { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class SurveyColumnDefinition
{
    public int Value { get; set; }
    public string Text { get; set; } = "";
}

public sealed class SurveyRowDefinition
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class SurveyActionsDefinition
{
    public SurveyActionDefinition Submit { get; set; } = new();
    public SurveyActionDefinition Cancel { get; set; } = new();
}

public sealed class SurveyActionDefinition
{
    public string Text { get; set; } = "";
    public string? LoadingText { get; set; }
    public string? ConfirmTitle { get; set; }
    public string? ConfirmMessage { get; set; }
    public string? ConfirmText { get; set; }
    public string? DismissText { get; set; }
}

public sealed class SurveyMessagesDefinition
{
    public string SubmissionSaved { get; set; } = "";             // persisted to the database
    public string SubmissionCompleted { get; set; } = "";         // accepted, persistence disabled
    public string SubmissionPersistenceFailure { get; set; } = "";  // database write failed; answers are kept
    public string SubmissionFailure { get; set; } = "";
    public string ValidationSummary { get; set; } = "";
    public string AuthenticationRequired { get; set; } = "";
    public string AccessDenied { get; set; } = "";
}

/// <summary>Sign-in / sign-out wording (shown only when an authentication mode with a session is active).</summary>
public sealed class SurveyAccountDefinition
{
    public string SignedInAs { get; set; } = "";
    public string SignOut { get; set; } = "";
    public string SignedOut { get; set; } = "";
    public string SignIn { get; set; } = "";
    public string SignInFailed { get; set; } = "";
}

/// <summary>Framework-level UI text (reconnect dialog, error and not-found pages).</summary>
public sealed class SurveySystemTextDefinition
{
    public string Reconnecting { get; set; } = "";
    public string ReconnectRepeatedBefore { get; set; } = "";
    public string ReconnectRepeatedAfter { get; set; } = "";
    public string ReconnectFailed { get; set; } = "";
    public string Retry { get; set; } = "";
    public string SessionPaused { get; set; } = "";
    public string ResumeFailed { get; set; } = "";
    public string Resume { get; set; } = "";
    public string UnhandledError { get; set; } = "";
    public string Reload { get; set; } = "";
    public string NotFoundTitle { get; set; } = "";
    public string NotFoundText { get; set; } = "";
    public string ErrorTitle { get; set; } = "";
    public string ErrorText { get; set; } = "";
    public string RequestId { get; set; } = "";
}
