# Blazor Survey Application — Configuration and Authentication Enhancements

## 1. Objective

Enhance the existing Blazor survey application without rewriting it. Inspect the existing architecture, components, configuration and persistence first. Preserve the modern UI, Arabic RTL support, responsive layout and existing functionality.

## 2. Thank-you message

Display **شكراً لمشاركتكم، نقدّر وقتكم وآراءكم.** in red (`#dc2626`), centered, with font weight 500–600 and appropriate spacing above the action buttons. Do not render it as an error alert. Load both text and color from survey JSON rather than hardcoding them.

## 3. Fully JSON-driven survey content

Move all user-facing survey text into a dedicated external JSON file, including titles, subtitles, descriptions, logo path and alt text, standalone labels, explicitly configured headings, checkbox and radio options, rating matrix instructions, column headings and rows, input labels and placeholders, required indicators, validation messages, thank-you text, button labels, loading text, success/failure messages and application authentication messages.

Use `Configuration/survey.json`, outside `wwwroot`, and include it in published output. Administrators must be able to edit content without rebuilding. Preserve all existing questions and options; the following is an illustrative structure, not a replacement for the complete existing survey.

```json
{
  "surveyId": "service-evaluation",
  "version": 1,
  "language": "ar",
  "direction": "rtl",
  "header": {
    "logo": "/images/survey-logo.png",
    "logoAlt": "شعار الاستبيان",
    "title": "استبيان",
    "heading": "نموذج التقديم",
    "subtitle": "عنوان فرعي تجريبي"
  },
  "elements": [
    {
      "id": "introduction",
      "type": "label",
      "text": "نص تعريفي تجريبي يوضح الهدف من هذا الاستبيان وكيفية تعبئته.",
      "alignment": "center"
    },
    {
      "id": "interests",
      "type": "checkboxGroup",
      "label": "المجالات التي تهمك",
      "required": false,
      "options": [
        { "value": "technology", "text": "التقنية" },
        { "value": "education", "text": "التعليم" },
        { "value": "health", "text": "الصحة" }
      ]
    },
    {
      "id": "rating-instructions",
      "type": "label",
      "text": "يرجى تقييم البنود التالية بحسب رأيك.",
      "alignment": "start"
    },
    {
      "id": "service-rating",
      "type": "ratingMatrix",
      "required": true,
      "columns": [
        { "value": 1, "text": "ضعيف جداً" },
        { "value": 2, "text": "ضعيف" },
        { "value": 3, "text": "متوسط" },
        { "value": 4, "text": "جيد" },
        { "value": 5, "text": "ممتاز" }
      ],
      "rows": [
        { "id": "service-quality", "text": "جودة الخدمة المقدمة" },
        { "id": "response-speed", "text": "سرعة الاستجابة" },
        { "id": "ease-of-use", "text": "سهولة استخدام النموذج" }
      ],
      "validationMessage": "يرجى تقييم جميع البنود المطلوبة."
    },
    {
      "id": "full-name",
      "type": "text",
      "label": "الاسم الكامل",
      "placeholder": "أدخل الاسم الكامل",
      "required": true,
      "validationMessage": "يرجى إدخال الاسم الكامل."
    },
    {
      "id": "comments",
      "type": "textarea",
      "label": "ملاحظات إضافية",
      "placeholder": "اكتب ملاحظاتك هنا",
      "required": false
    },
    {
      "id": "thank-you",
      "type": "label",
      "text": "شكراً لمشاركتكم، نقدّر وقتكم وآراءكم.",
      "alignment": "center",
      "color": "#dc2626",
      "fontWeight": 600
    }
  ],
  "actions": {
    "submit": { "text": "إرسال", "loadingText": "جاري الإرسال..." },
    "cancel": { "text": "إلغاء" }
  },
  "messages": {
    "submissionSuccess": "تم إرسال الاستبيان بنجاح.",
    "submissionFailure": "تعذر إرسال الاستبيان. يرجى المحاولة مرة أخرى.",
    "validationSummary": "يرجى استكمال الحقول المطلوبة.",
    "authenticationRequired": "يرجى تسجيل الدخول.",
    "accessDenied": "ليس لديك صلاحية الوصول."
  }
}
```

### Dynamic rendering

- Render elements in exact JSON array order. Do not invent headings or separate cards.
- Render standalone labels on their own lines, checkbox group labels above options, and rating matrices without extra headings unless explicitly configured.
- Preserve stable IDs and stored option values independently of display text.
- Support Arabic/English and RTL/LTR; allow adding, removing, reordering and renaming supported elements.

### Configuration loading

Reuse existing models or introduce strongly typed `SurveyDefinition`, `SurveyElementDefinition`, `SurveyHeaderDefinition`, `SurveyMessagesDefinition`, `SurveyOptions`, `ISurveyConfigurationService` and `SurveyConfigurationService` as appropriate.

Use dependency injection and `System.Text.Json` or existing configuration infrastructure. Cache parsed configuration rather than reading on every render. Support reload on change for newly opened surveys; keep existing sessions consistent with their original definition through submission. Validate configuration, duplicate IDs, unsupported types and style values. Retain the last known valid configuration on invalid runtime edits, log errors and fail startup clearly if no valid initial configuration exists. Do not render arbitrary HTML or executable content.

## 4. Configurable form validation

Use a strongly typed feature flag:

```csharp
public sealed class FeatureFlagsOptions
{
    public const string SectionName = "FeatureFlags";
    public bool EnableFormValidation { get; set; } = true;
    public bool EnableWindowsAuthentication { get; set; }
}
```

### Enabled

Apply configured required fields, checkbox/radio selection rules, required rating rows, length, format and selection-count rules where supported. Display configured validation messages and required indicators, highlight invalid fields and prevent invalid submission. Enforce the same rules server-side.

### Disabled

Do not enforce survey-specific required or optional validation rules. Hide required indicators and survey validation messages. Allow partial or empty responses where persistence supports them. Never disable security checks, request integrity, payload limits, safe input handling or database integrity protections. Report persistence conflicts and obtain approval before schema changes.

Review `EditForm`, `EditContext`, `DataAnnotationsValidator`, custom validators and submission handlers. Avoid unconditional validation. Centralize validation behavior and preserve submission/cancellation. If using `IOptionsMonitor`, define consistent behavior for active forms and server submission when the flag changes.

## 5. Optional Windows Authentication

### Enabled

Require Windows Integrated Authentication for survey pages and submission endpoints. Support Kerberos or NTLM as appropriate. Read the authenticated identity from the server-side `ClaimsPrincipal`, including domain-qualified usernames. Prevent anonymous endpoint access. Record authenticated identity if supported by existing persistence. Never trust a browser-supplied username. Do not introduce a custom login page.

### Disabled

Allow anonymous survey access and submission without a Windows challenge or credentials dialog. Safely handle missing `User.Identity.Name`; do not create fake identities. Preserve unrelated administrative authorization policies.

### Implementation

Inspect target framework, Blazor hosting model, middleware, authorization attributes, component authorization, submission endpoints and IIS configuration. Use appropriate ASP.NET Core authentication, such as IIS integration or `Microsoft.AspNetCore.Authentication.Negotiate` where appropriate, conditional authorization policies and server-side `AuthenticationStateProvider` integration. Avoid duplicate schemes. Evaluate the authentication flag at startup; an application restart is expected when changing it.

### IIS deployment

Application settings alone cannot override IIS authentication settings. Anonymous access requires IIS to permit anonymous requests; Windows Authentication requires the relevant IIS feature and configuration. Prefer a compatible configuration supporting anonymous requests and Windows challenges with application-level conditional authorization, where supported. Otherwise document IIS settings for each mode. Verify actual behavior under IIS, including protected endpoints. Document role services, Anonymous Authentication, Negotiate/NTLM, application pool identity, Kerberos/SPNs if required, restart and local development. Do not silently modify server-wide IIS settings or introduce external identity providers.

## 6. Application configuration

Merge into existing `appsettings.json`, preserving unrelated settings and environment overrides:

```json
{
  "Survey": {
    "ConfigurationFile": "Configuration/survey.json",
    "ReloadOnChange": true
  },
  "FeatureFlags": {
    "EnableFormValidation": true,
    "EnableWindowsAuthentication": false
  }
}
```

Keep survey content and field rules in `survey.json`, and application behavior in `appsettings.json`. Survey edits should require neither rebuild nor restart; validation flag changes may be reloadable with `IOptionsMonitor`; authentication flag changes require restart and compatible IIS settings.

## 7. Preserve existing UI

Keep the logo on the right, truly centered header, Arabic RTL layout, modern checkbox chips, radio controls, responsive rating matrix, inputs and textarea, existing palette and spacing, dynamic element order and centered action buttons. Keep the thank-you message red. Do not introduce hardcoded headings or unnecessary cards. Preserve desktop, tablet and mobile layouts.

## 8. Testing

| Validation | Windows Authentication | Expected behavior |
|---|---|---|
| Enabled | Enabled | Authenticated users must complete required fields |
| Enabled | Disabled | Anonymous users must complete required fields |
| Disabled | Enabled | Authenticated users may submit partial responses |
| Disabled | Disabled | Anonymous users may submit partial responses |

Also test JSON text updates without rebuild, adding/removing/reordering elements, configurable thank-you color, invalid JSON retaining the previous configuration, Arabic rendering, hidden required indicators when disabled, server-side validation enforcement, anonymous access rejection when authentication is enabled, anonymous submission when disabled, existing persistence and responsive UI. Add automated tests where practical. Report any IIS/domain tests that cannot be performed locally.

## 9. Technical constraints

Preserve Blazor architecture and project conventions. Use strongly typed configuration and dependency injection. Avoid unnecessary dependencies, external CDNs and internet-dependent assets. Maintain dark-site compatibility. Do not hardcode survey text, introduce unrelated architectural changes, weaken unrelated security policies or change database schemas without approval.

## 10. Implementation and delivery

Inspect the codebase first, then implement directly in the existing project rather than merely providing recommendations. Build the entire solution, run existing and new tests, resolve introduced errors and warnings, verify all four feature combinations, JSON reload and responsive layouts. Provide a list of modified files, complete survey JSON, updated application settings and README instructions for configuration and IIS deployment. Report remaining issues, assumptions and limitations.

## Expected result

A configuration-driven survey application with editable JSON content, red thank-you text, optional form validation and optional Windows Authentication, without source changes or rebuilding to change configuration. Preserve the modern appearance and existing functionality.
