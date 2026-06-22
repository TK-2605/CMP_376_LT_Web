# QuizHub PostgreSQL deployment

This path keeps the current ASP.NET Core MVC app and switches EF Core to PostgreSQL in production.

## Recommended free stack

- Web host: Render Free Web Service, using the existing Dockerfile
- Database: Neon Free Postgres
- Public domain: `https://quizhub-nhom4.onrender.com` or the generated `*.onrender.com` URL

Neon is preferred for the free database because its Free plan is $0 with no 30-day database expiry. Render's own free Postgres databases expire after 30 days, so use Render for the web app and Neon for PostgreSQL.

## Create Neon Postgres

1. Create a free Neon project.
2. Copy the pooled or direct connection string.
3. Use the Npgsql format in Render:

```text
Host=<host>;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true
```

## Deploy web app on Render

1. Push this branch to GitHub.
2. In Render, create a new Blueprint or Web Service from the repo.
3. Render reads `render.yaml`.
4. Fill every `sync: false` environment variable.
5. Set Google OAuth redirect URI after Render prints the live domain:

```text
https://<render-domain>/signin-google
```

You can also deploy through Render's API after the branch has been pushed:

```powershell
.\deploy\postgres\deploy-render.ps1 -RenderApiKey '<render-api-key>'
```

For Meilisearch on Render, create a second free Docker web service from the same repo and set its Dockerfile path to:

```text
./deploy/meilisearch/Dockerfile
```

Set `MEILI_MASTER_KEY` to the same value used by `Meilisearch__ApiKey`, then set the QuizHub web service `Meilisearch__Url` to the Meilisearch service URL.

## Import the local sample database

Copy `.env.postgres.example` to `.env.postgres`, fill `POSTGRES_CONNECTION_STRING`, then run:

```powershell
.\deploy\postgres\migrate-sample.ps1 -ResetTarget
```

The migrator reads local SQL Server LocalDB:

```text
(localdb)\mssqllocaldb / WebThiTracNghiem
```

It creates the PostgreSQL schema from the EF model, copies Identity and QuizHub tables in dependency order, then resets PostgreSQL identity sequences.
To verify the imported sample counts later without copying again, run:

```powershell
.\deploy\postgres\migrate-sample.ps1 -VerifyOnly
```

## Render environment variable mapping

Use these values from `.env.postgres`:

- `ConnectionStrings__DefaultConnection` = `POSTGRES_CONNECTION_STRING`
- `Database__Provider` = `PostgreSql`
- `Database__EnsureCreatedOnStartup` = `true`
- `Database__ApplyMigrationsOnStartup` = `false`
- `Authentication__Google__ClientId`
- `Authentication__Google__ClientSecret`
- `Email__Provider` = `GmailApi` on Render Free
- `GmailApi__ClientId`
- `GmailApi__ClientSecret`
- `GmailApi__RefreshToken`
- `GmailApi__FromEmail`
- `Smtp__UserName`
- `Smtp__Password`
- `Smtp__FromEmail`
- `Resend__ApiKey` and `Resend__FromEmail` as a HTTPS fallback if Gmail API is not used
- `Brevo__ApiKey` and `Brevo__FromEmail` as a HTTPS fallback if Gmail API is not used
- `SendGrid__ApiKey` and `SendGrid__FromEmail` as a HTTPS fallback if Gmail API is not used
- `Jwt__Key`

Meilisearch is optional for app availability. If it is empty, search falls back to SQL and the app works, but `/Admin/Technologies` will show Meilisearch as fallback instead of Ready.

Render Free Web Services block outbound SMTP ports `25`, `465`, and `587`, so Gmail SMTP app passwords can validate locally but still time out on Render Free. Use Gmail API over HTTPS for OTP email on the free deployment, or use another HTTPS provider such as Resend, Brevo, or SendGrid. Upgrade Render to a paid instance only if you want to keep SMTP.

## Gmail API OTP on Render Free

Use this path when OTP must be sent from a Gmail account while the app is deployed on Render Free.

1. In Google Cloud Console, enable Gmail API for the project.
2. Create an OAuth client that allows loopback redirect `http://127.0.0.1:53682/`.
3. Generate a refresh token locally:

```powershell
deploy/postgres/get-gmail-refresh-token.ps1 `
  -ClientId "YOUR_GOOGLE_CLIENT_ID" `
  -ClientSecret "YOUR_GOOGLE_CLIENT_SECRET"
```

4. Put these values in Render Environment:

- `Email__Provider` = `GmailApi`
- `GmailApi__ClientId`
- `GmailApi__ClientSecret`
- `GmailApi__RefreshToken`
- `GmailApi__FromEmail`
- `GmailApi__FromName` = `QuizHub`

When `GmailApi__*` is complete, QuizHub sends OTP through `gmail.googleapis.com` on HTTPS port `443`, not through SMTP.

## Verify

```text
https://<render-domain>/health
https://<render-domain>/health/ready
https://<render-domain>/Identity/Login/Account/Login
https://<render-domain>/Admin
https://<render-domain>/swagger
```
