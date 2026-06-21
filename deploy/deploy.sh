#!/usr/bin/env bash
set -euo pipefail

SERVER_IP=''
SSH_USER='root'
DOMAIN=''
ENVIRONMENT_FILE='.env.production'
IMPORT_LOCAL_DATABASE=false
IMPORT_UPLOADS=false

usage() {
  cat <<'USAGE'
Usage: ./deploy/deploy.sh --server-ip <VPS-IP> [options]

Options:
  --ssh-user <user>          SSH user (default: root)
  --domain <domain>          Public domain (default: quizhub.<VPS-IP>.sslip.io)
  --environment-file <path> Local production env file (default: .env.production)
  --import-local-database    Export LocalDB/current local SQL database and import its BACPAC
  --import-uploads           Copy LT_Web_Nhom4/App_Data/Uploads into the Docker volume
  -h, --help                 Show this help
USAGE
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --server-ip)
      SERVER_IP=$2
      shift 2
      ;;
    --ssh-user)
      SSH_USER=$2
      shift 2
      ;;
    --domain)
      DOMAIN=$2
      shift 2
      ;;
    --environment-file)
      ENVIRONMENT_FILE=$2
      shift 2
      ;;
    --import-local-database)
      IMPORT_LOCAL_DATABASE=true
      shift
      ;;
    --import-uploads)
      IMPORT_UPLOADS=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      printf 'Unknown argument: %s\n' "$1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [ -z "$SERVER_IP" ]; then
  printf '%s\n' '--server-ip is required.' >&2
  exit 2
fi

case "$SERVER_IP" in
  *[!A-Za-z0-9.-]*) printf '%s\n' 'Invalid server IP/hostname.' >&2; exit 2 ;;
esac
case "$SSH_USER" in
  *[!A-Za-z0-9_-]*) printf '%s\n' 'Invalid SSH user.' >&2; exit 2 ;;
esac

if [ -z "$DOMAIN" ]; then
  DOMAIN=$(printf 'quizhub.%s.sslip.io' "$SERVER_IP")
fi
case "$DOMAIN" in
  *[!A-Za-z0-9.-]*) printf '%s\n' 'Invalid domain.' >&2; exit 2 ;;
esac

for command_name in dotnet ssh scp tar; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    printf 'Required command is not available: %s\n' "$command_name" >&2
    exit 1
  fi
done

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPOSITORY_ROOT=$(dirname "$SCRIPT_DIR")
PROJECT_FILE="$REPOSITORY_ROOT/LT_Web_Nhom4/LT_Web_Nhom4.csproj"
ARTIFACTS_DIRECTORY="$SCRIPT_DIR/artifacts"
REMOTE_ROOT='/opt/quizhub'
TARGET=$(printf '%s@%s' "$SSH_USER" "$SERVER_IP")
TIMESTAMP=$(date '+%Y%m%d-%H%M%S')

mkdir -p "$ARTIFACTS_DIRECTORY"

printf '%s\n' 'Validating the application with a Release publish...'
dotnet publish "$PROJECT_FILE" \
  --configuration Release \
  --output "$ARTIFACTS_DIRECTORY/publish"

SOURCE_ARCHIVE="$ARTIFACTS_DIRECTORY/quizhub-source-$TIMESTAMP.tar.gz"
tar -czf "$SOURCE_ARCHIVE" \
  --exclude=.git \
  --exclude=.vs \
  --exclude=.vscode \
  --exclude=deploy/artifacts \
  --exclude=LT_Web_Nhom4/App_Data \
  --exclude=LT_Web_Nhom4/bin \
  --exclude=LT_Web_Nhom4/obj \
  --exclude=.env \
  --exclude=.env.production \
  -C "$REPOSITORY_ROOT" .

if [ -f "$ENVIRONMENT_FILE" ]; then
  if grep -q 'CHANGE_ME' "$ENVIRONMENT_FILE"; then
    printf '%s\n' 'The production environment file still contains CHANGE_ME placeholders.' >&2
    exit 1
  fi
  for required_key in SQL_SA_PASSWORD MEILI_MASTER_KEY JWT_KEY ACME_EMAIL; do
    if ! grep -Eq "^$required_key=.+" "$ENVIRONMENT_FILE"; then
      printf 'Missing required value in production environment file: %s\n' "$required_key" >&2
      exit 1
    fi
  done
fi

BACPAC_PATH=''
if [ "$IMPORT_LOCAL_DATABASE" = true ]; then
  if ! command -v python3 >/dev/null 2>&1; then
    printf '%s\n' 'python3 is required to read the local connection string.' >&2
    exit 1
  fi

  LOCAL_CONNECTION_STRING=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1], encoding="utf-8"))["ConnectionStrings"]["DefaultConnection"])' "$REPOSITORY_ROOT/LT_Web_Nhom4/appsettings.json")
  SQLPACKAGE_COMMAND=$(command -v SqlPackage || true)
  if [ -z "$SQLPACKAGE_COMMAND" ]; then
    SQLPACKAGE_COMMAND="$ARTIFACTS_DIRECTORY/sqlpackage/sqlpackage"
    if [ ! -x "$SQLPACKAGE_COMMAND" ]; then
      dotnet tool install microsoft.sqlpackage --tool-path "$ARTIFACTS_DIRECTORY/sqlpackage"
    fi
  fi

  BACPAC_PATH="$ARTIFACTS_DIRECTORY/quizhub-$TIMESTAMP.bacpac"
  printf '%s\n' 'Exporting the local database to a BACPAC artifact...'
  "$SQLPACKAGE_COMMAND" \
    /Action:Export \
    "/SourceConnectionString:$LOCAL_CONNECTION_STRING" \
    "/TargetFile:$BACPAC_PATH"
fi

UPLOADS_ARCHIVE=''
if [ "$IMPORT_UPLOADS" = true ]; then
  UPLOADS_DIRECTORY="$REPOSITORY_ROOT/LT_Web_Nhom4/App_Data/Uploads"
  if [ ! -d "$UPLOADS_DIRECTORY" ]; then
    printf 'Uploads directory does not exist: %s\n' "$UPLOADS_DIRECTORY" >&2
    exit 1
  fi
  UPLOADS_ARCHIVE="$ARTIFACTS_DIRECTORY/uploads-$TIMESTAMP.tar.gz"
  tar -czf "$UPLOADS_ARCHIVE" -C "$UPLOADS_DIRECTORY" .
fi

run_remote_compose() {
  ssh "$TARGET" "cd '$REMOTE_ROOT' && export DOMAIN='$DOMAIN' && docker compose --env-file .env.production -f docker-compose.prod.yml $1"
}

printf 'Uploading QuizHub to %s...\n' "$TARGET"
ssh "$TARGET" "mkdir -p '$REMOTE_ROOT/deploy/artifacts'"
scp "$SOURCE_ARCHIVE" "$TARGET:/tmp/quizhub-source.tar.gz"
ssh "$TARGET" "tar -xzf /tmp/quizhub-source.tar.gz -C '$REMOTE_ROOT'"

if [ -f "$ENVIRONMENT_FILE" ]; then
  scp "$ENVIRONMENT_FILE" "$TARGET:$REMOTE_ROOT/.env.production"
elif ! ssh "$TARGET" "test -f '$REMOTE_ROOT/.env.production'"; then
  printf 'No local environment file was supplied and %s/.env.production does not exist on the server.\n' "$REMOTE_ROOT" >&2
  exit 1
fi

ssh "$TARGET" "if grep -q CHANGE_ME '$REMOTE_ROOT/.env.production'; then echo 'Replace CHANGE_ME values in .env.production.' >&2; exit 1; fi"
run_remote_compose 'config --quiet'

if [ -n "$BACPAC_PATH" ]; then
  scp "$BACPAC_PATH" "$TARGET:$REMOTE_ROOT/deploy/artifacts/quizhub.bacpac"
  run_remote_compose 'up -d --wait --wait-timeout 240 sqlserver'
  run_remote_compose '--profile tools run --rm sqlpackage'
fi

if [ -n "$UPLOADS_ARCHIVE" ]; then
  scp "$UPLOADS_ARCHIVE" "$TARGET:$REMOTE_ROOT/deploy/artifacts/uploads.tar.gz"
  run_remote_compose '--profile tools run --rm upload-import'
fi

printf '%s\n' 'Building and starting the production stack...'
run_remote_compose 'up -d --build --remove-orphans --wait --wait-timeout 300'
ssh "$TARGET" "curl --fail --silent --show-error --retry 24 --retry-delay 5 --retry-all-errors 'https://$DOMAIN/health'"

printf '\nQuizHub deployment completed.\n'
printf 'Web:     https://%s\n' "$DOMAIN"
printf 'Admin:   https://%s/Admin\n' "$DOMAIN"
printf 'Swagger: https://%s/swagger\n' "$DOMAIN"
printf 'Health:  https://%s/health\n' "$DOMAIN"
