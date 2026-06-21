[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9.-]+$')]
    [string]$ServerIp,

    [ValidatePattern('^[A-Za-z_][A-Za-z0-9_-]*$')]
    [string]$SshUser = 'root',

    [ValidatePattern('^[A-Za-z0-9.-]*$')]
    [string]$Domain = '',

    [string]$EnvironmentFile = '.env.production',

    [switch]$ImportLocalDatabase,

    [switch]$ImportUploads
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = Split-Path -Parent $PSScriptRoot
$ProjectFile = Join-Path $RepositoryRoot 'LT_Web_Nhom4/LT_Web_Nhom4.csproj'
$ArtifactsDirectory = Join-Path $PSScriptRoot 'artifacts'
$RemoteRoot = '/opt/quizhub'
$Target = '{0}@{1}' -f $SshUser, $ServerIp

if ([string]::IsNullOrWhiteSpace($Domain)) {
    $Domain = 'quizhub.{0}.sslip.io' -f $ServerIp
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw ('Command failed with exit code {0}: {1}' -f $LASTEXITCODE, $FilePath)
    }
}

function Assert-CommandAvailable {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command is not available: $Name"
    }
}

function Assert-SafeEnvironmentFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    $content = Get-Content -LiteralPath $Path -Raw
    if ($content -match 'CHANGE_ME') {
        throw 'The production environment file still contains CHANGE_ME placeholders.'
    }

    foreach ($requiredKey in @('SQL_SA_PASSWORD', 'MEILI_MASTER_KEY', 'JWT_KEY', 'ACME_EMAIL')) {
        $pattern = '(?m)^' + [regex]::Escape($requiredKey) + '=(.+)$'
        if ($content -notmatch $pattern -or [string]::IsNullOrWhiteSpace($Matches[1])) {
            throw "Missing required value in production environment file: $requiredKey"
        }
    }
}

function Get-SqlPackagePath {
    $installed = Get-Command SqlPackage -ErrorAction SilentlyContinue
    if ($installed) {
        return $installed.Source
    }

    $toolDirectory = Join-Path $ArtifactsDirectory 'sqlpackage'
    $windowsExecutable = Join-Path $toolDirectory 'sqlpackage.exe'
    $unixExecutable = Join-Path $toolDirectory 'sqlpackage'
    if (Test-Path -LiteralPath $windowsExecutable) {
        return $windowsExecutable
    }
    if (Test-Path -LiteralPath $unixExecutable) {
        return $unixExecutable
    }

    Invoke-NativeCommand -FilePath 'dotnet' -Arguments @(
        'tool', 'install', 'microsoft.sqlpackage', '--tool-path', $toolDirectory
    )

    if (Test-Path -LiteralPath $windowsExecutable) {
        return $windowsExecutable
    }
    if (Test-Path -LiteralPath $unixExecutable) {
        return $unixExecutable
    }

    throw 'SqlPackage installation completed but its executable could not be found.'
}

function Get-RemoteComposeCommand {
    param([Parameter(Mandatory = $true)][string]$Command)

    return "cd '$RemoteRoot' && export DOMAIN='$Domain' && docker compose --env-file .env.production -f docker-compose.prod.yml $Command"
}

foreach ($commandName in @('dotnet', 'ssh', 'scp', 'tar')) {
    Assert-CommandAvailable -Name $commandName
}

New-Item -ItemType Directory -Path $ArtifactsDirectory -Force | Out-Null

Write-Host 'Validating the application with a Release publish...'
Invoke-NativeCommand -FilePath 'dotnet' -Arguments @(
    'publish', $ProjectFile,
    '--configuration', 'Release',
    '--output', (Join-Path $ArtifactsDirectory 'publish')
)

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$sourceArchive = Join-Path $ArtifactsDirectory ("quizhub-source-$timestamp.tar.gz")
Invoke-NativeCommand -FilePath 'tar' -Arguments @(
    '-czf', $sourceArchive,
    '--exclude=.git',
    '--exclude=.vs',
    '--exclude=.vscode',
    '--exclude=deploy/artifacts',
    '--exclude=LT_Web_Nhom4/App_Data',
    '--exclude=LT_Web_Nhom4/bin',
    '--exclude=LT_Web_Nhom4/obj',
    '--exclude=.env',
    '--exclude=.env.production',
    '-C', $RepositoryRoot,
    '.'
)

$resolvedEnvironmentFile = $null
if (Test-Path -LiteralPath $EnvironmentFile) {
    $resolvedEnvironmentFile = (Resolve-Path -LiteralPath $EnvironmentFile).Path
    Assert-SafeEnvironmentFile -Path $resolvedEnvironmentFile
}

$bacpacPath = $null
if ($ImportLocalDatabase) {
    $settingsPath = Join-Path $RepositoryRoot 'LT_Web_Nhom4/appsettings.json'
    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    $localConnectionString = $settings.ConnectionStrings.DefaultConnection
    if ([string]::IsNullOrWhiteSpace($localConnectionString)) {
        throw 'The local DefaultConnection value is missing from appsettings.json.'
    }

    $bacpacPath = Join-Path $ArtifactsDirectory ("quizhub-$timestamp.bacpac")
    $sqlPackage = Get-SqlPackagePath
    Write-Host 'Exporting the local database to a BACPAC artifact...'
    Invoke-NativeCommand -FilePath $sqlPackage -Arguments @(
        '/Action:Export',
        ("/SourceConnectionString:$localConnectionString"),
        ("/TargetFile:$bacpacPath")
    )
}

$uploadsArchive = $null
if ($ImportUploads) {
    $uploadsDirectory = Join-Path $RepositoryRoot 'LT_Web_Nhom4/App_Data/Uploads'
    if (-not (Test-Path -LiteralPath $uploadsDirectory -PathType Container)) {
        throw "Uploads directory does not exist: $uploadsDirectory"
    }

    $uploadsArchive = Join-Path $ArtifactsDirectory ("uploads-$timestamp.tar.gz")
    Invoke-NativeCommand -FilePath 'tar' -Arguments @(
        '-czf', $uploadsArchive,
        '-C', $uploadsDirectory,
        '.'
    )
}

Write-Host ("Uploading QuizHub to {0}..." -f $Target)
Invoke-NativeCommand -FilePath 'ssh' -Arguments @(
    $Target,
    "mkdir -p '$RemoteRoot/deploy/artifacts'"
)
Invoke-NativeCommand -FilePath 'scp' -Arguments @(
    $sourceArchive,
    ('{0}:/tmp/quizhub-source.tar.gz' -f $Target)
)
Invoke-NativeCommand -FilePath 'ssh' -Arguments @(
    $Target,
    "tar -xzf /tmp/quizhub-source.tar.gz -C '$RemoteRoot'"
)

if ($resolvedEnvironmentFile) {
    Invoke-NativeCommand -FilePath 'scp' -Arguments @(
        $resolvedEnvironmentFile,
        ('{0}:{1}/.env.production' -f $Target, $RemoteRoot)
    )
}
else {
    & ssh $Target "test -f '$RemoteRoot/.env.production'"
    if ($LASTEXITCODE -ne 0) {
        throw "No local environment file was supplied and $RemoteRoot/.env.production does not exist on the server."
    }
}

Invoke-NativeCommand -FilePath 'ssh' -Arguments @(
    $Target,
    "if grep -q CHANGE_ME '$RemoteRoot/.env.production'; then echo 'Replace CHANGE_ME values in .env.production.' >&2; exit 1; fi"
)
Invoke-NativeCommand -FilePath 'ssh' -Arguments @(
    $Target,
    (Get-RemoteComposeCommand -Command 'config --quiet')
)

if ($bacpacPath) {
    Invoke-NativeCommand -FilePath 'scp' -Arguments @(
        $bacpacPath,
        ('{0}:{1}/deploy/artifacts/quizhub.bacpac' -f $Target, $RemoteRoot)
    )
    Invoke-NativeCommand -FilePath 'ssh' -Arguments @(
        $Target,
        (Get-RemoteComposeCommand -Command 'up -d --wait --wait-timeout 240 sqlserver')
    )
    Invoke-NativeCommand -FilePath 'ssh' -Arguments @(
        $Target,
        (Get-RemoteComposeCommand -Command '--profile tools run --rm sqlpackage')
    )
}

if ($uploadsArchive) {
    Invoke-NativeCommand -FilePath 'scp' -Arguments @(
        $uploadsArchive,
        ('{0}:{1}/deploy/artifacts/uploads.tar.gz' -f $Target, $RemoteRoot)
    )
    Invoke-NativeCommand -FilePath 'ssh' -Arguments @(
        $Target,
        (Get-RemoteComposeCommand -Command '--profile tools run --rm upload-import')
    )
}

Write-Host 'Building and starting the production stack...'
Invoke-NativeCommand -FilePath 'ssh' -Arguments @(
    $Target,
    (Get-RemoteComposeCommand -Command 'up -d --build --remove-orphans --wait --wait-timeout 300')
)

$healthCommand = "curl --fail --silent --show-error --retry 24 --retry-delay 5 --retry-all-errors 'https://$Domain/health'"
Invoke-NativeCommand -FilePath 'ssh' -Arguments @($Target, $healthCommand)

Write-Host ''
Write-Host 'QuizHub deployment completed.' -ForegroundColor Green
Write-Host ("Web:     https://{0}" -f $Domain)
Write-Host ("Admin:   https://{0}/Admin" -f $Domain)
Write-Host ("Swagger: https://{0}/swagger" -f $Domain)
Write-Host ("Health:  https://{0}/health" -f $Domain)
