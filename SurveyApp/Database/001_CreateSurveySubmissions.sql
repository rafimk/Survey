/*
  001_CreateSurveySubmissions.sql
  Idempotent: safe to run more than once; it never alters or drops an existing table.
  Run it MANUALLY with a deployment account (db_ddladmin) against the intended database. The application never
  creates or alters schema at startup. Requires SQL Server 2016+ (ISJSON).

  Example:  sqlcmd -S SQLSERVER -d SurveyDb -E -b -i 001_CreateSurveySubmissions.sql
*/
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.SurveySubmissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SurveySubmissions
    (
        SubmissionId           uniqueidentifier NOT NULL,           -- idempotency key issued by the server
        SurveyId               nvarchar(200)    NOT NULL,
        SurveyVersion          nvarchar(100)    NOT NULL,
        SubmittedUtc           datetime2(3)     NOT NULL,
        AuthenticationMode     nvarchar(20)     NOT NULL,           -- none | window | keycloak
        UserIdentifier         nvarchar(512)    NULL,               -- trusted stable identity; NULL for anonymous
        UserName               nvarchar(512)    NULL,               -- trusted display name; NULL for anonymous
        ResponseJson           nvarchar(max)    NOT NULL,           -- answers by stable element / option / row ids
        DefinitionSnapshotJson nvarchar(max)    NULL,               -- survey definition used for this response

        CONSTRAINT PK_SurveySubmissions PRIMARY KEY CLUSTERED (SubmissionId),
        CONSTRAINT CK_SurveySubmissions_AuthenticationMode CHECK (AuthenticationMode IN (N'none', N'window', N'keycloak')),
        CONSTRAINT CK_SurveySubmissions_ResponseJson CHECK (ISJSON(ResponseJson) = 1),
        CONSTRAINT CK_SurveySubmissions_DefinitionSnapshotJson CHECK (DefinitionSnapshotJson IS NULL OR ISJSON(DefinitionSnapshotJson) = 1)
    );
    PRINT 'Created dbo.SurveySubmissions.';
END
ELSE
    PRINT 'dbo.SurveySubmissions already exists; no changes made.';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SurveySubmissions_SurveyId_SubmittedUtc'
               AND object_id = OBJECT_ID(N'dbo.SurveySubmissions'))
    CREATE NONCLUSTERED INDEX IX_SurveySubmissions_SurveyId_SubmittedUtc
        ON dbo.SurveySubmissions (SurveyId, SubmittedUtc);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SurveySubmissions_SubmittedUtc'
               AND object_id = OBJECT_ID(N'dbo.SurveySubmissions'))
    CREATE NONCLUSTERED INDEX IX_SurveySubmissions_SubmittedUtc
        ON dbo.SurveySubmissions (SubmittedUtc);

/*
  Least privilege for the application's runtime identity (application pool account or SQL login) — run separately,
  replacing the placeholder. It only needs to read (idempotent retry check) and insert:

  -- GRANT SELECT, INSERT ON dbo.SurveySubmissions TO [DOMAIN\SurveyAppPoolAccount];
*/
