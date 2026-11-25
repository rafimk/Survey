# Blazor Survey — Authentication Modes and Optional SQL Server Persistence

## 1. Objective

Extend the existing Blazor survey application in place. Replace the existing Windows Authentication boolean with three mutually exclusive authentication modes and introduce optional SQL Server persistence on submission. Preserve the modern Arabic RTL UI, JSON-driven survey definition, configurable validation, red thank-you message, centered header and right-side logo.

Inspect the solution, target framework, hosting model, existing authentication, submission flow, configuration services and persistence code before implementing. Reuse existing conventions. Do not rewrite the application.

## 2. Application configuration

Merge the following example into the existing `appsettings.json`; retain unrelated settings and existing survey configuration.

```json
{
  "FeatureFlags": {
    "EnableFormValidation": true,
    "EnableSqlPersistence": false
  },
  "Authentication": {
    "Mode": "none",
    "Keycloak": {
      "Authority": "",
      "ClientId": "",
      "ClientSecret": "",
      "RequireHttpsMetadata": true,
      "CallbackPath": "/signin-oidc",
      "SignedOutCallbackPath": "/signout-callback-oidc"
    }
  },
  "ConnectionStrings": {
    "SurveyDatabase": ""
  }
}
```

Supported mode values are exactly `none`, `window`, and `keycloak` (case-insensitive). Use strongly typed options and an enum or explicit parser. Reject unknown modes at startup; never silently fall back to anonymous access. Remove the obsolete `EnableWindowsAuthentication` setting and migrate existing environment overrides explicitly. Document the breaking configuration change.

Default local configuration must be `none` with SQL persistence disabled so the application can run without external infrastructure. Authentication and persistence mode changes may require an application restart. Validate provider-specific settings only when the corresponding feature is enabled. Never commit real credentials; support environment variables and development user secrets.

## 3. Authentication modes

### 3.1 `none` — anonymous access

- Allow anonymous survey viewing and submission without login or authentication challenges.
- Do not require Windows credentials or contact Keycloak.
- Handle absent user identity safely; do not manufacture a user identity.
- Do not weaken unrelated administrative authorization.

### 3.2 `window` — Windows Integrated Authentication

- Use IIS Windows Authentication integration or ASP.NET Core Negotiate, as appropriate for the actual hosting model.
- Require authentication for survey pages and server-side submission operations.
- Obtain the domain-qualified username from the trusted server-side principal, never from browser-supplied fields.
- Support Kerberos/NTLM as permitted by the environment.
- Document IIS role services, Anonymous Authentication and Windows Authentication settings, and any SPN requirements. Application settings cannot override IIS authentication restrictions.
- Do not introduce a custom Windows username/password login page.

### 3.3 `keycloak` — OpenID Connect

Implement the full integration now, even though Keycloak and Docker are not available on the current development machine. Do not install Docker, start containers, contact an external identity provider, or require a running Keycloak instance to build or run in `none` mode.

- Use the ASP.NET Core OpenID Connect handler with cookie authentication for a server-hosted Blazor application, adapting to the actual hosting model. If the application is WebAssembly, enforce authentication and authorization in the server/API and use an appropriate supported OIDC architecture; never expose a client secret to the browser.
- Use authorization code flow with PKCE, discovery metadata, issuer/signature/audience/lifetime validation, state and nonce protections.
- Configure Authority (including realm path), ClientId, ClientSecret, callback paths and HTTPS metadata requirements through configuration.
- Use `preferred_username` for display where available, and issuer plus `sub` for stable identity. Do not use email or display name as a unique identity key.
- Require authentication before viewing protected surveys and submitting responses. Preserve safe local return URLs after login.
- Implement logout using the configured provider and local cookie sign-out where applicable. Do not create logout loops.
- Support IIS virtual directories/PathBase and trusted reverse-proxy HTTPS forwarding where applicable. Do not trust arbitrary forwarded headers.
- Keep tokens and secrets out of logs, survey payloads and browser-accessible configuration. Do not persist tokens unless a justified existing requirement exists.
- If Keycloak is unavailable while its mode is enabled, fail securely and present an appropriate configured error; never fall back to anonymous access.
- Use local dependencies/assets and packages compatible with the project's target framework and offline deployment.

Example future configuration (placeholders only):

```json
{
  "Authentication": {
    "Mode": "keycloak",
    "Keycloak": {
      "Authority": "https://sso.example.internal/realms/survey",
      "ClientId": "survey-web",
      "RequireHttpsMetadata": true,
      "CallbackPath": "/signin-oidc",
      "SignedOutCallbackPath": "/signout-callback-oidc"
    }
  }
}
```

Document how to configure a confidential server-side Keycloak client, enable Standard Flow, register exact redirect and post-logout redirect URIs including any application path, and supply the client secret securely (for example `Authentication__Keycloak__ClientSecret`). Account for Keycloak installations with a legacy `/auth` context path by accepting the full configured Authority. No live Keycloak verification is expected on this machine; clearly distinguish implementation and automated tests from future integration testing.

## 4. Shared authentication architecture

Centralize mode selection and authorization. Register only the required provider for the selected mode where practical. Ensure authentication middleware precedes authorization middleware and that the Blazor authentication state is integrated correctly. Protect server-side submission operations as well as UI routes; hiding UI is not authorization. Handle interactive server circuits and reconnects according to the hosting model.

Expose a small current-user abstraction if useful, returning authentication mode, authenticated status, stable identifier and display username. Derive these values server-side. Keep authentication-specific code out of survey rendering and SQL persistence logic.

## 5. Optional SQL Server persistence

### 5.1 Feature flag behavior

When `FeatureFlags:EnableSqlPersistence` is `true`, persist a survey response to SQL Server only after the user clicks Submit and the server accepts the submission. When false, do not connect to SQL Server, run migrations, require a connection string or write any response to SQL Server. Preserve existing non-database submission behavior where applicable; do not claim a response was saved when persistence is disabled. Use a configurable completion message appropriate to this mode. Cancel must never persist a response.

The SQL flag is independent of authentication mode and the form-validation flag. Support all combinations.

### 5.2 Architecture

Introduce an abstraction such as `ISurveySubmissionStore` with a SQL Server implementation and an explicitly non-persisting implementation. Return an explicit outcome distinguishing persisted, accepted without persistence, and failed. Keep the Razor component independent of SQL. Use the existing data-access technology if suitable; otherwise prefer `Microsoft.Data.SqlClient` with parameterized SQL (Dapper is acceptable if already used). Do not add EF Core solely for this feature unless justified.

Use async operations, cancellation tokens, bounded command timeouts, dependency injection and structured logging. Open and dispose connections per operation. Use a transaction for multi-table writes. Never concatenate user input into SQL. Do not log response contents, credentials or personal data unnecessarily.

### 5.3 Suggested database design

Adapt to existing tables if present. Otherwise provide a versioned, idempotent SQL deployment script, for example `Database/001_CreateSurveySubmissions.sql`. Do not automatically create or alter production databases at startup.

Suggested table `dbo.SurveySubmissions`:

| Column | SQL type | Purpose |
|---|---|---|
| SubmissionId | uniqueidentifier | Primary key and idempotency identifier |
| SurveyId | nvarchar(200) | Stable survey identifier |
| SurveyVersion | nvarchar(100) | Definition version or immutable revision identifier |
| SubmittedUtc | datetime2(3) | Server-generated UTC timestamp |
| AuthenticationMode | nvarchar(20) | none/window/keycloak |
| UserIdentifier | nvarchar(512) NULL | Trusted stable identity, null for anonymous |
| UserName | nvarchar(512) NULL | Trusted display username, null for anonymous |
| ResponseJson | nvarchar(max) | Structured answers using stable element/option/row IDs |
| DefinitionSnapshotJson | nvarchar(max) NULL | Optional definition snapshot for historical interpretation |

Use Unicode types for Arabic. Add appropriate JSON validity constraints (if supported by the target SQL Server version) and indexes for survey ID and submission date. Preserve the definition revision or snapshot so historical answers remain interpretable after survey.json changes. Do not store passwords, access tokens or unnecessary identity claims. Do not impose NOT NULL constraints on optional answer fields that conflict with disabled survey validation.

JSON storage is a proposed default for dynamic surveys; if the existing application requires normalized reporting tables, explain the tradeoff and reuse the existing model rather than introducing duplicate storage.

Example response payload (illustrative):

```json
{
  "interests": ["technology", "education"],
  "service-rating": {
    "service-quality": 5,
    "response-speed": 4
  },
  "full-name": "Example",
  "comments": ""
}
```

### 5.4 Submission workflow

1. User clicks Submit; disable duplicate clicks and show the configured loading text.
2. Verify authorization on the server according to the active authentication mode.
3. Validate request integrity, allowed element IDs and values, payload limits and the survey definition revision.
4. Apply survey-required and other configurable form rules only when `EnableFormValidation` is enabled. Essential security and data-integrity checks always remain active.
5. Generate or reuse a server-issued submission identifier bound to the submission attempt. Use a unique database constraint and idempotent handling to prevent duplicate records on retries; do not silently accept a different payload under the same identifier.
6. If SQL persistence is enabled, save the accepted response and trusted identity, and confirm success only after commit. If disabled, complete without a database write and report the correct non-persisted outcome.
7. Display the appropriate JSON-configured success or completion message. On database failure, retain the entered answers, display a configured failure message, and allow a safe retry. Never show false success or silently discard a failed write.

Do not clear the form before successful completion. Do not persist on page load, field change or cancel. Do not introduce a one-response-per-user restriction unless explicitly requested.

### 5.5 SQL Server configuration and deployment

Document a connection string example using placeholders and Windows integrated database credentials:

```text
Server=SQLSERVER;Database=SurveyDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=False;
```

Also document SQL authentication using securely supplied credentials if required. The database service identity is independent of the survey user's authentication mode; do not impersonate the survey user for database access. Explain IIS application pool/service account permissions and certificate trust. Grant least privilege to the runtime account and use a separate deployment account for schema changes. Do not hardcode passwords or disable certificate verification as a production default.

When persistence is enabled, require a valid nonempty connection string at startup. Handle database unavailability explicitly; never silently switch to non-persisting mode. Do not require a live SQL Server to build or run with persistence disabled. Do not execute schema scripts against any existing database without approval.

## 6. Preserve configuration-driven UI

All user-facing survey text, including new login/logout labels, authentication errors, loading, database failure, saved-success and non-persisted completion messages, must come from the existing survey JSON configuration or its established localization configuration. Preserve existing JSON element order, stable IDs, reload behavior and validation configuration. Preserve the red thank-you message, centered title, right-side logo, modern controls and RTL responsiveness. Do not introduce unrelated visual changes.

## 7. Testing and acceptance criteria

Test all 12 combinations of three authentication modes, two validation states and two persistence states using automated tests and suitable test doubles. At minimum verify:

- `none` + persistence disabled starts and submits without Keycloak, Docker or SQL Server.
- Unknown authentication modes fail securely; missing provider settings are rejected only when required.
- Windows and Keycloak modes reject anonymous protected submissions; test policy behavior using test authentication handlers.
- Keycloak configuration, scheme registration, challenge and callback configuration are tested without a live provider where practical.
- Disabled persistence never invokes SQL or requires a connection string.
- Enabled persistence invokes the store only for accepted submissions; rejected submissions and cancel do not write.
- Validation disabled allows incomplete answers while preserving essential integrity checks.
- Anonymous identity fields are null; authenticated identity comes from trusted claims.
- Database errors do not show success; retries do not create duplicate records.
- Arabic answers round-trip correctly in SQL integration tests when an approved test database is available.
- Existing survey rendering, JSON reload, validation and responsive layout remain functional.

Use mocks/fakes for unavailable infrastructure and clearly label tests that are not live integration tests. Do not install Docker or invent successful integration results. If SQL Server or Windows domain infrastructure is unavailable, provide manual verification steps and mark those tests pending as appropriate.

## 8. Implementation and delivery

Implement the changes directly in the existing project, not just sample code. Build the solution and run available tests. Resolve introduced errors and warnings. Update README with the three authentication modes, feature flags, sample configuration, secure secret handling, IIS deployment, Keycloak client setup, SQL script execution and future integration verification steps.

Provide a summary of modified files, final configuration, database script location, test results, outstanding infrastructure-dependent checks and any architectural decisions. Obtain approval before destructive changes or changes to existing database schemas. Keep the application runnable locally in anonymous, non-persisting mode.

## Expected result

Administrators can select `none`, `window` or `keycloak` through appsettings.json and independently enable or disable SQL Server persistence and survey validation. Once compatible Keycloak/IIS/SQL infrastructure is configured and verified, the relevant features can be activated without rebuilding the application. Configuration changes that affect startup services require a restart.
