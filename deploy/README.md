# QuizHub production deployment

The production stack contains QuizHub (.NET 9), SQL Server 2022, Meilisearch, and Caddy with automatic HTTPS. Docker volumes persist the database, search index, Caddy state, and private uploads.

## VPS prerequisites

- Ubuntu/Debian VPS with at least 4 GB RAM (SQL Server requires x86-64/amd64)
- Docker Engine with Docker Compose v2
- TCP ports 22, 80, and 443 open; UDP 443 is recommended for HTTP/3
- A public IPv4 address

## Configure secrets

Copy `.env.production.example` to `.env.production`, replace every `CHANGE_ME` value, and keep the resulting file out of Git. The deploy scripts upload it over SSH, or reuse `/opt/quizhub/.env.production` if it already exists on the VPS.

Generate long random values for `SQL_SA_PASSWORD`, `MEILI_MASTER_KEY`, and `JWT_KEY`. Rotate the Google client secret and Gmail app password that previously appeared in tracked configuration before a real deployment.

For Google OAuth, register this exact production redirect URI:

```text
https://quizhub.<VPS-IP>.sslip.io/signin-google
```

## Deploy from Windows PowerShell

```powershell
.\deploy\deploy.ps1 `
  -ServerIp '<VPS-IP>' `
  -SshUser 'root' `
  -Domain 'quizhub.<VPS-IP>.sslip.io' `
  -ImportLocalDatabase `
  -ImportUploads
```

`deploy.ps1` uses PowerShell-native variable interpolation only. Docker Compose placeholders remain in `docker-compose.prod.yml`, where Compose—not PowerShell—expands them. This separation prevents the `${...}` parser error caused by embedding Compose or Bash templates in a PowerShell here-string.

## Deploy from Bash

```bash
bash ./deploy/deploy.sh \
  --server-ip '<VPS-IP>' \
  --ssh-user root \
  --domain 'quizhub.<VPS-IP>.sslip.io' \
  --import-local-database \
  --import-uploads
```

Database import is intended for the first production deployment. SqlPackage refuses to import over an existing database, protecting an already deployed database from accidental replacement. Subsequent deploys should omit the database import flag; EF Core migrations still run automatically at startup.

## Verify

```text
https://quizhub.<VPS-IP>.sslip.io/health
https://quizhub.<VPS-IP>.sslip.io/health/ready
https://quizhub.<VPS-IP>.sslip.io/Admin
https://quizhub.<VPS-IP>.sslip.io/swagger
```

Swagger is disabled unless `SWAGGER_ENABLED=true`; in production it also requires an authenticated Admin session.
