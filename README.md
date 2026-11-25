# Survey application (Blazor, .NET 10)

Arabic RTL survey form. Content comes from an editable JSON file. Authentication (`none` / `window` / `keycloak`),
form validation and SQL Server persistence are independent settings.

```
SurveyApp/                         Blazor Web App (Interactive Server)
  Configuration/survey.json        ALL survey text, elements, options and messages (edit without rebuilding)
  Database/001_CreateSurveySubmissions.sql   idempotent schema script (run manually)
  appsettings.json                 Survey, FeatureFlags, Authentication, ConnectionStrings, ReverseProxy
SurveyApp.Tests/                   xUnit tests
Survey.slnx
```

Run locally (anonymous, no database, no external services): `dotnet run --project SurveyApp` → http://localhost:5078.
Test: `dotnet test Survey.slnx`.

## 1. Breaking configuration change

`FeatureFlags:EnableWindowsAuthentication` **no longer exists**. The application refuses to start while it is present
(in appsettings *or* as the environment variable `FeatureFlags__EnableWindowsAuthentication`) and prints how to migrate:

| Old | New |
|---|---|
| `EnableWindowsAuthentication: false` | `Authentication:Mode = "none"` |
| `EnableWindowsAuthentication: true` | `Authentication:Mode = "window"` |
| env `FeatureFlags__EnableWindowsAuthentication` | env `Authentication__Mode` |

Also changed in `survey.json`: `messages.submissionSuccess` was replaced by `submissionSaved`, `submissionCompleted` and
`submissionPersistenceFailure`, and a new `account` section holds the sign-in/out wording (all strings are required).

## 2. Settings (`appsettings.json`)

```json
"FeatureFlags": { "EnableFormValidation": true, "EnableSqlPersistence": false },
"Authentication": {
  "Mode": "none",
  "Keycloak": { "Authority": "", "ClientId": "", "ClientSecret": "", "RequireHttpsMetadata": true,
                "CallbackPath": "/signin-oidc", "SignedOutCallbackPath": "/signout-callback-oidc" }
},
"ConnectionStrings": { "SurveyDatabase": "" },
"ReverseProxy": { "Enabled": false, "KnownProxies": [], "KnownNetworks": [], "PathBase": "" }
```

| Setting | Takes effect |
|---|---|
| `EnableFormValidation` | next render/submission (reloaded live) |
| `Authentication:*`, `EnableSqlPersistence`, `ConnectionStrings`, `ReverseProxy` | **restart** |
| `Configuration/survey.json` | next survey opened, no restart |

`Authentication:Mode` accepts exactly `none`, `window`, `keycloak` (case-insensitive). Anything else — including empty
or missing — stops startup; the app never falls back to anonymous access. Provider settings are validated only for the
selected mode, the SQL connection string only when persistence is enabled. Error messages never contain secrets.

**Secrets:** never commit them. Use environment variables (`Authentication__Keycloak__ClientSecret`,
`ConnectionStrings__SurveyDatabase`) or, in development, user secrets:
`dotnet user-secrets set "Authentication:Keycloak:ClientSecret" "…" --project SurveyApp`.

## 3. Authentication modes

All modes except `none` require an authenticated user for every page, the Blazor circuit and submissions (fallback
authorization policy), and the submission service re-checks authentication on the server — hiding UI is not
authorization. Identity is derived only from the server-side principal, never from the browser.

### `none`
Anonymous viewing and submission. No authentication services or middleware are registered. Stored identity is `NULL`.

### `window` — Windows Integrated Authentication
Under IIS the IIS handler is used; otherwise (Kestrel / development) ASP.NET Core Negotiate (Kerberos or NTLM). Only one
is ever registered. Identity: `DOMAIN\user` (display and `UserName`), the Windows SID as `UserIdentifier`. No login page.
Cancelling the credential prompt shows `messages.authenticationRequired`.

**IIS setup** (application settings cannot override IIS authentication):
1. Install the IIS role service *Windows Authentication* and the ASP.NET Core Hosting Bundle (.NET 10).
2. Site → *Authentication*: enable **Windows Authentication** and **Anonymous Authentication**. The app decides whether
   to challenge, so the same IIS configuration serves every mode. (If Anonymous is disabled, IIS challenges
   everything — `none` then cannot work.) Providers: *Negotiate* then *NTLM*.
3. Kerberos needs an SPN for the site's host name on the app pool's service account when the pool is not a
   machine/network-service identity: `setspn -S HTTP/survey.corp.example DOMAIN\poolaccount`; otherwise clients use NTLM.
4. In-process hosting (default). Pool identity needs read access to the app folder and `Configuration\survey.json`.
5. Recycle the pool after changing `Authentication:Mode`.

### `keycloak` — OpenID Connect
Server-side cookie + OpenID Connect handler: **authorization code flow with PKCE (S256)**, discovery from the
Authority, issuer/signature/audience/lifetime validation, state and nonce. The client secret stays on the server;
tokens are not stored except the ID token inside the encrypted HttpOnly cookie, used only as `id_token_hint` for
single sign-out. No token or secret is logged or sent to the browser.

Identity: stable key = `issuer|sub` (`UserIdentifier`), display = `preferred_username` (`UserName`). Email and display
name are never used as keys. After login users return to the page they asked for (the framework only accepts local
return URLs). A user without `sub`/issuer is treated as unauthenticated.

If Keycloak is unreachable or the callback fails: callback failures redirect to a public page (`/auth/error`,
`account.signInFailed`); an unreachable provider during sign-in yields the error page (`systemText.errorText` in
production). Anonymous access is never granted. Static assets are public so these pages render.

Sign-out: a "sign out" button (POST + antiforgery) ends the local session and the Keycloak session and lands on the public
`/auth/signed-out` page (not `/`, which would immediately start another login).

**Keycloak client setup** (confidential client):
1. Realm → Clients → Create: *Client type* OpenID Connect, *Client ID* `survey-web`.
2. *Capability config*: **Client authentication ON**, **Standard flow ON**, Direct access grants OFF, Implicit OFF.
3. *Valid redirect URIs*: the **exact** callback, e.g. `https://survey.corp.example/signin-oidc` (include any app path,
   e.g. `https://host/survey/signin-oidc`). *Valid post logout redirect URIs*: `https://survey.corp.example/signout-callback-oidc`.
   *Web origins*: the site origin.
4. *Credentials* tab → copy the client secret into `Authentication__Keycloak__ClientSecret`.
5. `Authority` is the full issuer: `https://sso.corp.example/realms/survey`; for legacy Keycloak with a context path
   `https://sso.corp.example/auth/realms/survey`. `RequireHttpsMetadata` may be `false` only for local http testing.
6. Make sure users have a `preferred_username` (default `profile` scope).

**Reverse proxy / virtual directory:** IIS virtual directories need no setting (`PathBase` comes from IIS, links are
PathBase-relative). Behind another proxy set `ReverseProxy:Enabled=true` and list its address in `KnownProxies` or
`KnownNetworks`; `X-Forwarded-For/Proto/Host` are honoured only from those hosts (startup fails if enabled with none).
Use `ReverseProxy:PathBase` (e.g. `/survey`) when the proxy strips a path prefix. Keep `logo` in survey.json relative
(`logo.svg`, not `/logo.svg`) so it works under a path prefix.

## 4. SQL Server persistence

`EnableSqlPersistence=false` (default): no connection, no connection string needed, no database write. Accepted
submissions show `messages.submissionCompleted` (nothing claims to be saved).

`EnableSqlPersistence=true`: requires `ConnectionStrings:SurveyDatabase` (checked at startup). After **Submit** is accepted
by the server (authorization, integrity, and rules if validation is on) the response is inserted into
`dbo.SurveySubmissions` with parameterized SQL (`Microsoft.Data.SqlClient`; no EF/Dapper), 15 s command timeout, one
connection per call. Success (`messages.submissionSaved`) is shown only after the insert commits. On a database
error the answers stay in the form, `messages.submissionPersistenceFailure` is shown and the user can retry; the app
never silently switches to non-persisting mode. Cancel, page load and field edits never write. There is no
one-response-per-user limit.

**Idempotency:** each submission attempt has a server-issued `SubmissionId` (primary key) reused on retries of the same
form session. A retry with identical answers returns success without a second row; a different payload under the same
id is refused. If a first attempt committed but the reply was lost *and* the user then edits answers before retrying,
the retry is refused (failure message) rather than overwriting — a deliberate, safe choice; reload the form to start a new attempt.

**Stored data** (`ResponseJson`, Unicode, stable ids): `{"interests":["technology"],"service-rating":{"service-quality":5,
"data-accuracy":null},"full-name":"…","comments":""}`, plus `DefinitionSnapshotJson` (the survey definition used) and
`SurveyVersion`, so history stays interpretable when survey.json changes. Identity columns are `NULL` for anonymous.
JSON in a single table was chosen because the survey is dynamic; reporting can use `OPENJSON`. Tokens, passwords and other
claims are never stored. Response contents are never logged.

**Deployment:**
1. Review and run `SurveyApp/Database/001_CreateSurveySubmissions.sql` yourself with a deployment account, against the intended
   database (`sqlcmd -S SQLSERVER -d SurveyDb -E -b -i 001_CreateSurveySubmissions.sql`). It is idempotent and never alters
   an existing table. The app never creates or alters schema.
2. Connection string (integrated security; the app pool identity is the DB login, independent of the survey user's auth mode):
   `Server=SQLSERVER;Database=SurveyDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=False;`
   SQL authentication (if required): add `User Id=…;Password=…` supplied via `ConnectionStrings__SurveyDatabase`, never in a file.
3. Least privilege for the runtime account: `GRANT SELECT, INSERT ON dbo.SurveySubmissions TO [DOMAIN\SurveyAppPoolAccount];`
4. The SQL Server certificate must be trusted by the web server (install the CA); do not use `TrustServerCertificate=True`
   in production.

## 5. survey.json and validation

See the element reference below. All user-facing text lives in the file, including thank-you color/weight, submit/cancel and
dialog text, messages, `account` and `systemText` (reconnect dialog, error and not-found pages). The file is validated
(duplicate ids, unsupported types, bad colors/logo paths, bad regexes, missing messages, misspelled properties); valid edits
apply to newly opened surveys, an open survey keeps its definition until submitted/cancelled; invalid edits are logged
and the last valid version stays; with no valid file at startup the app does not start.

| type | Properties |
|---|---|
| `label` | `text`, `alignment` (`start`/`center`/`end`), optional `color` (`#rgb`/`#rrggbb`), `fontWeight` (100–900) |
| `checkboxGroup` | `label`, `options[{value,text}]`, `required`, `minSelections`, `maxSelections` |
| `ratingMatrix` | `columns[{value:int,text}]`, `rows[{id,text}]`, `required`, `indexHeading`, `itemHeading` |
| `text` / `textarea` | `label`, `placeholder`, `required`, `minLength`, `maxLength`, `pattern` |

`validationMessage` = required rule, `ruleMessage` = length/pattern/selection rules. With `EnableFormValidation=false`
survey rules and required indicators are off and partial responses are accepted; integrity checks (known option/row/column
values, 4000-character cap) always run.

## 6. Verification status

| Check | Status |
|---|---|
| All 12 combinations (3 modes × validation × persistence) through the submission service with fake stores | automated, pass |
| `none` + persistence off starts and submits with no Keycloak, Docker or SQL Server | automated + manual, pass |
| Unknown/missing mode, obsolete flag, missing provider/connection settings rejected at startup (and only when needed) | automated + manual, pass |
| `window`/`keycloak` reject anonymous protected requests and submissions (test authentication handler) | automated, pass |
| Keycloak scheme registration, PKCE/state/nonce challenge, callback failure → error page, logout form + antiforgery — static OIDC metadata, **no live provider** | automated, pass |
| `window` on real Kestrel/Negotiate: anonymous → 401, Negotiate as current user → 200 | manual, pass |
| Keycloak with an unreachable provider fails (500, no survey content), never anonymous | manual, pass |
| Database failure ≠ success; retry idempotency | automated with fakes, pass |
| **SQL Server** store (Arabic round trip, real idempotency) | **pending** — tests skip unless `SURVEY_TEST_SQL_CONNECTION` points to an approved test database with script 001 applied |
| **Live Keycloak** login/logout, token validation, PathBase and proxy behaviour | **pending** — needs a Keycloak instance |
| **IIS**, domain accounts, Kerberos/SPN | **pending** — needs IIS/domain |

Manual verification once infrastructure exists: (Keycloak) browse to the site → redirected to Keycloak → log in → survey shows
your `preferred_username` → submit → sign out → `/auth/signed-out`; (SQL) enable persistence, submit, `SELECT TOP 1 * FROM
dbo.SurveySubmissions ORDER BY SubmittedUtc DESC`, then stop SQL and confirm the failure message with answers kept;
(IIS) browse from a domain machine and confirm `DOMAIN\user` in the log line `Survey … submitted`.

Known limitation: a Blazor circuit keeps the principal it started with, so a Keycloak cookie that expires mid-session is
re-evaluated on the next page load/new circuit, not instantly (sessions are bounded by the 8-hour cookie lifetime).
"# Survey" 
