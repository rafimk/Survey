# Blazor Survey — Single-Selection Groups and Textarea Enhancements

## Objective
Implement the following changes in the existing Blazor application. Preserve the modern design, dynamic JSON-driven rendering, RTL support, authentication modes, optional validation and optional SQL Server persistence. Inspect the existing implementation before editing; do not rewrite the application.

## 1. Convert Two Checkbox Groups to Radio Groups
Change the following existing groups to single-selection radio groups:

- المجالات التي تهمك (areas of interest)
- الخدمات التي استفدت منها (services benefited from)

Preserve all existing labels, options, stable IDs, stored option values and ordering. Each group must allow at most one selected option independently of the other group. Selecting a new option automatically deselects the previous option within that group. Do not preselect an option unless explicitly configured.

### UI requirements
- Retain the existing modern selectable chip design, spacing, wrapping and RTL ordering.
- Replace square checkbox indicators with circular radio indicators.
- Keep the entire chip clickable, with the existing primary-colored selected background and border.
- Use accessible native radio inputs or the appropriate Blazor radio group components, unique group names, associated labels and visible keyboard focus.
- Support keyboard navigation and mobile touch interaction.

### JSON and data binding
Update the existing survey JSON to use the supported radio-group element type (for example `radioGroup`) for these two elements. If the type is not supported yet, implement a reusable dynamic radio-group renderer. Do not hardcode the Arabic labels or options in Razor or C#.

Bind each group to a single nullable option value, not a collection. Preserve multi-select checkbox support for other dynamically configured elements. Ensure submitted response JSON represents these two groups as scalar values or null when unanswered, using existing stable option values. Do not alter historical stored submissions; maintain compatibility with earlier survey versions and follow the existing versioning convention.

## 2. Make Both Text Fields Large and Equal in Size
The existing full-name field (الاسم الكامل) is a single-line input while additional comments (ملاحظات إضافية) is a large textarea. Change the full-name field to a multiline textarea and make both fields visually identical in size.

- Both fields must occupy 100% of the available content width.
- Use the current comments textarea height as the reference (approximately 96px, subject to existing styling).
- Use identical initial height, minimum height, padding, border radius, background, font, line height, hover and focus states.
- Align Arabic text and placeholders at the top right.
- Allow vertical resizing if consistent with the existing design; both fields should initially have equal heights.
- Preserve each field's stable ID, label, placeholder, value binding and configured validation rules.
- Update the full-name element type in survey JSON to `textarea` or the existing equivalent. Reuse the existing textarea renderer rather than creating a special hardcoded full-name component.
- If configurable height is needed, introduce a validated shared textarea size option or CSS design token. Avoid arbitrary CSS injection from JSON.

## 3. Validation and Persistence
- Preserve `FeatureFlags:EnableFormValidation`. When enabled, enforce the existing JSON-defined required rules and messages. When disabled, permit unanswered groups and empty fields where supported.
- Regardless of the validation flag, a single-selection group must never accept multiple values or unknown option IDs. Enforce this on the server as a structural integrity rule.
- Preserve the existing `none`, `window` and `keycloak` authentication modes.
- Preserve `FeatureFlags:EnableSqlPersistence`. When enabled, save the updated response shape using the existing persistence service. When disabled, do not access SQL Server.
- Do not change the database schema unnecessarily. Preserve previous submissions and their interpretation.
- Preserve the red thank-you message and all existing JSON-configurable text.

## 4. Acceptance Criteria and Testing
- Selecting one option deselects the previous option within the same group only.
- Both groups can be selected independently and have no default selection unless configured.
- Existing checkbox groups still support multiple selections.
- Both textareas have the same initial height and width on desktop and mobile.
- Arabic RTL alignment, chip styling and focus states remain consistent.
- JSON-defined labels, options, placeholders and ordering remain configurable without rebuilding.
- Validation enabled and disabled modes behave correctly.
- Submitted JSON contains scalar values for the two radio groups and string values for both textareas.
- Existing authentication and SQL persistence behavior remains unchanged.

## 5. Implementation and Delivery
Inspect the existing Razor components, configuration models, survey JSON, response models, validation and persistence services. Implement the changes directly in the project, reusing existing components and styles. Build the solution, run existing and new relevant tests, resolve introduced errors and warnings, and verify desktop and mobile layouts. Provide the modified file list, updated JSON configuration, test results and any remaining limitations. Do not require a running Keycloak instance or Docker for these UI changes. Do not introduce external dependencies or CDNs.
