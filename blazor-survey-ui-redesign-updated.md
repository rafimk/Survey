# Modernize the Existing Blazor Survey Form UI/UX

## Objective

Redesign the existing Blazor survey form into a modern, elegant, professional, enterprise-grade user interface. Use the attached screenshots as a reference for the current implementation.

Follow modern enterprise UI principles inspired by shadcn/ui, Tailwind CSS and contemporary SaaS applications while retaining the existing Blazor architecture. This is a UI/UX improvement task, not an application rewrite.

## 1. Design Language

- Clean, minimal, professional appearance with a light neutral page background and white content surfaces.
- Subtle borders, soft shadows and rounded corners between 10px and 16px.
- Consistent 8px-based spacing system and modern Arabic typography.
- Restrained indigo or blue accent color.
- Smooth hover, focus and selection transitions.
- Avoid excessive gradients, shadows and decorative elements.
- Maintain a professional government and enterprise application appearance.

## 2. Header Redesign — High Priority

- Keep the existing logo on the right and preserve its appearance.
- Center-align the survey title, main heading and subtitle relative to the **entire header container**, not the remaining space beside the logo.
- Use a three-column CSS Grid with equal-width left and right columns and a flexible center column. Keep the left column empty to balance the logo.
- Vertically align the logo and heading content.
- Reduce unnecessary vertical whitespace.
- Center-align a description only if it is explicitly configured as header metadata. Standalone text/label items in the survey body must remain in their configured position and be right-aligned.
- Add a subtle separator between the header and form content.

Desktop layout:

```text
┌──────────────────────────────────────────────┐
│                 استبيان              [Logo]  │
│              نموذج التقديم                   │
│            عنوان فرعي تجريبي                 │
│                                              │
│       Optional header description            │
│──────────────────────────────────────────────│
└──────────────────────────────────────────────┘
```

On mobile, allow the header to stack gracefully without overlapping the logo or title.

## 3. Main Form Container

- Center the form horizontally with a maximum width of approximately 1100px.
- Use responsive horizontal padding, a light neutral page background and a continuous white form surface.
- Reduce excessive empty spaces and maintain consistent vertical rhythm.
- Avoid unnecessary nested cards and heavy borders.

## 4. Dynamic Form Content and Field Ordering — Mandatory Correction

The survey is dynamically configured. Do not interpret the sample content as fixed sections or invent headings such as `تقييم الخدمات` (Service evaluation). The latest screenshot shows an unwanted heading and description grouped inside a separate card.

Render every configured content item in its original order, using its existing type and configured text. Inspect the existing model and renderer to identify the actual field types and ordering mechanism.

Required example sequence (top to bottom):

```text
نص تعريفي تجريبي يوضح الهدف من هذا الاستبيان وكيفية تعبئته.

المجالات التي تهمك:
[ التقنية ]  [ التعليم ]  [ الصحة ]

يرجى تقييم البنود التالية بحسب رأيك.

[ Existing rating matrix ]
```

- First item: standalone configured text/label, displayed on its own line.
- Next item: configured checkbox-group title/label on its own line, followed by the checkbox options on the next line.
- Next item: another standalone configured text/label on its own line, followed by the rating matrix as the next configured item.
- The example text is illustrative only. Never hardcode it, invent a title, or append a colon unless the configured text or existing label formatting calls for one.
- Do not combine standalone text items into a generated section heading and subtitle. Do not move them into the header or into another field's card.
- Keep standalone labels and field labels right-aligned in RTL, with consistent typography and spacing. Only the actual survey header title/subtitle should be centered; the logo remains on the right.
- Use one continuous white form surface with compact spacing between items. Do not create a separate card for every label, checkbox group or rating matrix. A subtle border around the rating matrix itself is acceptable.
- Apply the same dynamic rendering rules to all subsequent checkbox groups, text inputs, textareas and other supported field types.
- Preserve configured visibility, order, required indicators, bindings and validation. Do not change the backend schema or add new field types merely to achieve this layout.

Acceptance criteria: the visible sequence must match the configured survey sequence exactly, with no synthetic headings, duplicated labels, or relocated text. Verify with multiple survey configurations, not only the sample shown in the screenshot.

## 5. Modern Checkbox Components

Redesign existing checkbox options as modern selectable chips.

- Unselected: light neutral background, subtle border, neutral text and comfortable padding.
- Selected: soft primary-colored background, primary border, clear checkmark and slightly emphasized text.
- Make the entire chip clickable, with hover and keyboard focus states.
- Preserve multi-selection functionality, responsive wrapping and RTL ordering.

## 6. Rating Matrix Redesign

Modernize the existing rating table without changing its data structure or functionality.

### Table styling

- Rounded outer container with subtle border.
- Modern light-colored table header and clear Arabic typography.
- Comfortable row heights of approximately 52–60px.
- Subtle alternating row backgrounds, light horizontal separators and row hover effects.
- Avoid heavy vertical grid lines.
- Give the evaluation item column sufficient width and distribute rating columns evenly.

### Rating controls

- Replace browser-default radio button appearance with modern custom-styled circular controls.
- Provide a clear primary-colored selected state and subtle selected-cell background.
- Make the rating cell a comfortable click target.
- Maintain one selection per evaluation item, keyboard navigation and accessibility.
- Highlight unanswered required rows when validation fails.

Preserve the existing rating scale and underlying values:

- ضعيف جداً
- ضعيف
- متوسط
- جيد
- ممتاز

Do not reverse the existing rating order.

### Mobile experience

Consider rendering each evaluation item as a compact rating card with its five options rather than forcing horizontal scrolling. Retain the rating matrix on desktop. Both layouts must use the same Blazor model and validation state.

## 7. Text Inputs and Textarea

- Clear labels above fields, consistent rounded corners and subtle neutral borders.
- Input heights of approximately 44–48px.
- Modern primary-colored focus rings and appropriate validation styling.
- Right-align Arabic text and placeholders.
- Allow the textarea to resize vertically.
- Preserve all existing Blazor bindings and validation rules.

## 8. Footer and Action Buttons

Replace the bright red thank-you message with a professional neutral or muted primary text color. Preserve the Arabic message:

شكراً لمشاركتكم، نقدّر وقتكم وآراءكم.

### Submit — إرسال

- Solid indigo or blue background, white text and medium font weight.
- Minimum height of 44px, optional send icon.
- Hover, active, disabled and loading states.

### Cancel — إلغاء

- Secondary outlined button with neutral background and subtle border.
- Consistent dimensions with the submit button.

Center both buttons horizontally with consistent spacing. Prevent duplicate submissions while submission is in progress.

## 9. Typography

Use a modern Arabic-friendly font, subject to local availability: IBM Plex Sans Arabic, Tajawal or Noto Sans Arabic.

- Main title: 28–32px, semibold or bold.
- Subtitle: 14–16px.
- Section headings: 17–18px, semibold.
- Form labels: 14–15px, medium.
- Body text: 14–15px.
- Supporting text: 13–14px.

Use comfortable Arabic line heights. Serve all fonts and assets locally; do not introduce external CDN dependencies.

## 10. CSS Design Tokens

Introduce reusable CSS custom properties. Suggested starting palette:

```css
:root {
    --survey-primary: #4f46e5;
    --survey-primary-hover: #4338ca;
    --survey-primary-soft: #eef2ff;
    --survey-background: #f8fafc;
    --survey-surface: #ffffff;
    --survey-text: #0f172a;
    --survey-text-secondary: #475569;
    --survey-text-muted: #64748b;
    --survey-border: #e2e8f0;
    --survey-input-background: #f8fafc;
    --survey-success: #16a34a;
    --survey-danger: #dc2626;
    --survey-radius-sm: 8px;
    --survey-radius-md: 12px;
    --survey-radius-lg: 16px;
    --survey-shadow: 0 4px 16px rgba(15, 23, 42, 0.04);
}
```

Reuse these tokens throughout survey components instead of hardcoding repeated colors and dimensions.

## 11. Technical Requirements

- Retain Blazor; do not migrate to React, Angular or another framework.
- Inspect existing Razor components, layouts and styles before making changes.
- Preserve component parameters, dynamic survey rendering, data bindings, event handlers and validation.
- Preserve submission and cancellation behavior.
- Do not modify backend APIs, database schemas or business logic.
- Do not hardcode survey questions or options.
- Prefer reusable Razor components and scoped CSS isolation.
- Use CSS logical properties such as `padding-inline`, `margin-inline` and `text-align: start` where appropriate.
- Ensure full Arabic RTL support, accessibility, keyboard navigation and visible focus indicators.
- Do not introduce unnecessary NuGet packages or JavaScript libraries.
- Do not use external CDNs or internet-dependent assets.
- Ensure compatibility with offline and intranet deployment environments.

## 12. Responsive Design

Validate the redesigned form at 1920px, 1440px, 1024px, 768px and 390px viewport widths.

Ensure no unintended horizontal scrolling, no header or logo overlapping, proper Arabic text wrapping, responsive checkbox chips, usable rating controls, consistent margins and padding, and comfortable touch targets.

## 13. Implementation Process

First inspect the existing implementation and identify relevant Razor components, CSS files and layout structure. Then implement the redesign using the existing application architecture.

Do not simply provide recommendations or sample code. Apply the changes directly to the project.

After implementation:

- Build the application and run existing relevant tests.
- Verify that all survey functionality remains unchanged.
- Verify form validation and submission.
- Check desktop and mobile layouts.
- Resolve introduced build errors or warnings.
- Provide a summary of modified files and design improvements.
- Report remaining issues or limitations.

## Expected Result

The final survey should look like a premium, modern enterprise SaaS application rather than a traditional HTML or Bootstrap form. It should feel clean, compact, elegant, responsive and professional, with a truly centered header, right-positioned logo, modern interactive controls and a consistent reusable visual design system.

Prioritize visual quality, usability and maintainability without compromising existing functionality. The proposed colors, dimensions and spacing are design recommendations, not existing codebase requirements.

## 14. Final Correction Verification

Before completion, verify that the latest screenshot issue is resolved: remove the invented `تقييم الخدمات` heading unless explicitly configured, render the rating instruction as its own ordered text item, and keep the checkbox title above its options. Preserve the modern styling and centered header with right-side logo. These dynamic content rules take precedence over any earlier suggestion to introduce section headings or separate cards.
