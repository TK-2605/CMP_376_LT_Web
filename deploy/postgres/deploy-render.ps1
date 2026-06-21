[CmdletBinding()]
param(
    [string]$EnvironmentFile = '.env.postgres',

    [string]$RenderApiKey,

    [string]$RepositoryUrl = 'https://github.com/TK-2605/CMP_376_LT_Web',

    [string]$Branch = 'codex/deploy-release-candidate',

    [string]$ServiceName = 'quizhub-nhom4',

    [string]$FallbackSuffix = '',

    [switch]$SkipSmokeTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ApiBase = 'https://api.render.com/v1'

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
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$Default = ''
    )

    if ($Values.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace([string]$Values[$Name])) {
        return [string]$Values[$Name]
    }

    return $Default
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

function New-EnvVar {
    param(
        [Parameter(Mandatory = $true)][string]$Key,
        [Parameter(Mandatory = $true)][string]$Value
    )

    return @{ key = $Key; value = $Value }
}

function Invoke-Render {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [object]$Body = $null
    )

    $headers = @{
        Authorization = "Bearer $RenderApiKey"
        Accept = 'application/json'
    }
    $parameters = @{
        Method = $Method
        Uri = "$ApiBase$Path"
        Headers = $headers
        ErrorAction = 'Stop'
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = ($Body | ConvertTo-Json -Depth 12)
    }

    return Invoke-RestMethod @parameters
}

$envValues = Read-EnvironmentFile -Path $EnvironmentFile
if ([string]::IsNullOrWhiteSpace($RenderApiKey)) {
    $RenderApiKey = Get-Setting -Values $envValues -Name 'RENDER_API_KEY'
}
if ([string]::IsNullOrWhiteSpace($RenderApiKey) -or $RenderApiKey -like '*CHANGE_ME*') {
    throw 'Render API key is required. Pass -RenderApiKey or set RENDER_API_KEY in .env.postgres.'
}

$postgres = Convert-PostgresConnectionString -Value (Get-Setting -Values $envValues -Name 'POSTGRES_CONNECTION_STRING')
if ([string]::IsNullOrWhiteSpace($postgres)) {
    throw 'POSTGRES_CONNECTION_STRING is required.'
}

$jwtKey = Get-Setting -Values $envValues -Name 'JWT_KEY'
if ($jwtKey.Length -lt 32) {
    throw 'JWT_KEY must be at least 32 characters.'
}

if (-not [string]::IsNullOrWhiteSpace($FallbackSuffix)) {
    $ServiceName = "$ServiceName-$FallbackSuffix"
}

$owners = Invoke-Render -Method Get -Path '/owners?limit=20'
if ($owners.Count -eq 0) {
    throw 'Render API key has no accessible workspace.'
}

$owner = $owners[0].owner
if ($null -eq $owner) {
    $owner = $owners[0]
}
$ownerId = $owner.id
if ([string]::IsNullOrWhiteSpace($ownerId)) {
    throw 'Could not determine Render ownerId.'
}

$envVars = @(
    New-EnvVar 'ASPNETCORE_ENVIRONMENT' 'Production'
    New-EnvVar 'Database__Provider' 'PostgreSql'
    New-EnvVar 'Database__EnsureCreatedOnStartup' 'true'
    New-EnvVar 'Database__ApplyMigrationsOnStartup' 'false'
    New-EnvVar 'ConnectionStrings__DefaultConnection' $postgres
    New-EnvVar 'Authentication__Google__ClientId' (Get-Setting -Values $envValues -Name 'GOOGLE_CLIENT_ID')
    New-EnvVar 'Authentication__Google__ClientSecret' (Get-Setting -Values $envValues -Name 'GOOGLE_CLIENT_SECRET')
    New-EnvVar 'Jwt__Issuer' (Get-Setting -Values $envValues -Name 'JWT_ISSUER' -Default 'QuizHub')
    New-EnvVar 'Jwt__Audience' (Get-Setting -Values $envValues -Name 'JWT_AUDIENCE' -Default 'QuizHub.Api')
    New-EnvVar 'Jwt__Key' $jwtKey
    New-EnvVar 'Jwt__ExpireMinutes' (Get-Setting -Values $envValues -Name 'JWT_EXPIRE_MINUTES' -Default '120')
    New-EnvVar 'Jwt__RefreshTokenDays' (Get-Setting -Values $envValues -Name 'JWT_REFRESH_TOKEN_DAYS' -Default '7')
    New-EnvVar 'Smtp__Host' (Get-Setting -Values $envValues -Name 'SMTP_HOST' -Default 'smtp.gmail.com')
    New-EnvVar 'Smtp__Port' (Get-Setting -Values $envValues -Name 'SMTP_PORT' -Default '587')
    New-EnvVar 'Smtp__UserName' (Get-Setting -Values $envValues -Name 'SMTP_USERNAME')
    New-EnvVar 'Smtp__Password' (Get-Setting -Values $envValues -Name 'SMTP_PASSWORD')
    New-EnvVar 'Smtp__FromEmail' (Get-Setting -Values $envValues -Name 'SMTP_FROM_EMAIL')
    New-EnvVar 'Smtp__FromName' (Get-Setting -Values $envValues -Name 'SMTP_FROM_NAME' -Default 'QuizHub')
    New-EnvVar 'Meilisearch__Url' (Get-Setting -Values $envValues -Name 'MEILI_URL')
    New-EnvVar 'Meilisearch__ApiKey' (Get-Setting -Values $envValues -Name 'MEILI_API_KEY')
    New-EnvVar 'Meilisearch__IndexName' (Get-Setting -Values $envValues -Name 'MEILI_INDEX_NAME' -Default 'quizhub-private-search')
    New-EnvVar 'PrivateMediaRoot' (Get-Setting -Values $envValues -Name 'PRIVATE_MEDIA_ROOT' -Default '/tmp/quizhub-uploads')
    New-EnvVar 'ForwardedHeaders__Enabled' 'true'
    New-EnvVar 'Swagger__Enabled' (Get-Setting -Values $envValues -Name 'SWAGGER_ENABLED' -Default 'true')
)

$body = @{
    type = 'web_service'
    name = $ServiceName
    ownerId = $ownerId
    repo = $RepositoryUrl
    branch = $Branch
    autoDeploy = 'yes'
    envVars = $envVars
    serviceDetails = @{
        env = 'docker'
        plan = 'free'
        dockerfilePath = './Dockerfile'
        healthCheckPath = '/health'
        region = 'oregon'
    }
}

try {
    $serviceResponse = Invoke-Render -Method Post -Path '/services' -Body $body
}
catch {
    $statusCode = $null
    if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
        $statusCode = [int]$_.Exception.Response.StatusCode
    }

    if ($statusCode -ne 409 -or -not [string]::IsNullOrWhiteSpace($FallbackSuffix)) {
        throw
    }

    $shortSuffix = (Get-Random -Minimum 1000 -Maximum 9999).ToString()
    Write-Host "Service name '$ServiceName' already exists. Retrying with suffix $shortSuffix."
    & $PSCommandPath -EnvironmentFile $EnvironmentFile -RenderApiKey $RenderApiKey -RepositoryUrl $RepositoryUrl -Branch $Branch -ServiceName $ServiceName -FallbackSuffix $shortSuffix -SkipSmokeTest:$SkipSmokeTest
    exit $LASTEXITCODE
}

$service = if ($serviceResponse.service) { $serviceResponse.service } else { $serviceResponse }
$serviceId = $service.id
$serviceSlug = $service.slug
if ([string]::IsNullOrWhiteSpace($serviceSlug)) {
    $serviceSlug = $ServiceName
}
$baseUrl = "https://$serviceSlug.onrender.com"

Write-Host "Render service created: $serviceId"
Write-Host "URL: $baseUrl"

if (-not $SkipSmokeTest) {
    $deadline = (Get-Date).AddMinutes(25)
    do {
        Start-Sleep -Seconds 20
        try {
            $health = Invoke-WebRequest -Uri "$baseUrl/health" -UseBasicParsing -TimeoutSec 20
            if ([int]$health.StatusCode -ge 200 -and [int]$health.StatusCode -lt 300) {
                Write-Host "Health check passed: $baseUrl/health"
                break
            }
        }
        catch {
            Write-Host 'Waiting for Render deployment...'
        }
    } while ((Get-Date) -lt $deadline)
}

@{
    ServiceId = $serviceId
    Url = $baseUrl
    HealthUrl = "$baseUrl/health"
    ReadyUrl = "$baseUrl/health/ready"
    AdminUrl = "$baseUrl/Admin"
    SwaggerUrl = "$baseUrl/swagger"
    GoogleRedirectUri = "$baseUrl/signin-google"
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'render-deploy-summary.json') -Encoding UTF8

Write-Host "Google redirect URI: $baseUrl/signin-google"
