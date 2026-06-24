[CmdletBinding()]
param(
    [string]$EnvironmentFile = '.env.postgres',

    [string]$SourceSqlServer = '(localdb)\mssqllocaldb',

    [string]$SourceDatabase = 'WebThiTracNghiem',

    [switch]$ResetTarget,

    [switch]$VerifyOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$MigratorProject = Join-Path $PSScriptRoot 'QuizHub.PostgresMigrator/QuizHub.PostgresMigrator.csproj'

function Read-EnvironmentFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Environment file not found: $Path. Copy .env.postgres.example to .env.postgres first."
    }

    $values = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) {
            continue
        }

        $separator = $trimmed.IndexOf('=')
        if ($separator -lt 1) {
            continue
        }

        $key = $trimmed.Substring(0, $separator).Trim()
        $value = $trimmed.Substring($separator + 1).Trim()
        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        $values[$key] = $value
    }

    return $values
}

function Get-Setting {
    param(
        [Parameter(Mandatory = $true)][hashtable]$Values,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($Values.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace([string]$Values[$Name])) {
        return [string]$Values[$Name]
    }

    return ''
}

function Convert-PostgresConnectionString {
    param([Parameter(Mandatory = $true)][string]$Value)

    if ($Value -notmatch '^postgres(ql)?://') {
        return $Value
    }

    $uri = [Uri]$Value
    $credentials = $uri.UserInfo.Split([char[]]@(':'), 2)
    if ($credentials.Count -ne 2) {
        throw 'PostgreSQL URL must include username and password.'
    }

    $database = $uri.AbsolutePath.TrimStart('/')
    if ([string]::IsNullOrWhiteSpace($database)) {
        throw 'PostgreSQL URL must include a database name.'
    }

    $username = [Uri]::UnescapeDataString($credentials[0])
    $password = [Uri]::UnescapeDataString($credentials[1])
    $port = if ($uri.Port -gt 0) { $uri.Port } else { 5432 }
    return "Host=$($uri.Host);Port=$port;Database=$database;Username=$username;Password=$password;SSL Mode=Require;Trust Server Certificate=true"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Required command is not available: dotnet'
}

$envValues = Read-EnvironmentFile -Path $EnvironmentFile
$targetConnectionString = Get-Setting -Values $envValues -Name 'POSTGRES_CONNECTION_STRING'
if ([string]::IsNullOrWhiteSpace($targetConnectionString) -or $targetConnectionString -like '*CHANGE_ME*') {
    throw "Missing POSTGRES_CONNECTION_STRING in $EnvironmentFile"
}
$targetConnectionString = Convert-PostgresConnectionString -Value $targetConnectionString

$sourceConnectionString = "Server=$SourceSqlServer;Database=$SourceDatabase;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
$arguments = @(
    'run',
    '--project', $MigratorProject,
    '--',
    '--target', $targetConnectionString
)
if (-not $VerifyOnly) {
    $arguments += @('--source', $sourceConnectionString)
}
if ($ResetTarget) {
    $arguments += '--reset-target'
}
if ($VerifyOnly) {
    $arguments += '--verify-only'
}

Push-Location $RepositoryRoot
try {
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL sample migration failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
