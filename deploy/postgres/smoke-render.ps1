param(
    [string]$BaseUrl = "https://quizhub-nhom4.onrender.com",
    [int]$Attempts = 5,
    [int]$DelaySeconds = 20
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Net.Http

$base = $BaseUrl.TrimEnd("/")
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $true
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(60)

function Invoke-SmokeRequest {
    param(
        [string]$Path,
        [switch]$Json
    )

    $url = "$base$Path"
    $lastError = $null
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            $response = $client.GetAsync($url).GetAwaiter().GetResult()
            $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $statusCode = [int]$response.StatusCode

            if ($statusCode -ge 200 -and $statusCode -lt 400) {
                Write-Host "OK $statusCode $Path"
                if ($Json) {
                    return $body | ConvertFrom-Json
                }

                return $body
            }

            $lastError = "HTTP $statusCode"
        }
        catch {
            $lastError = $_.Exception.Message
        }

        if ($attempt -lt $Attempts) {
            Write-Host "Retry $attempt/$Attempts ${Path}: $lastError"
            Start-Sleep -Seconds $DelaySeconds
        }
    }

    throw "Smoke check failed for $Path after $Attempts attempts: $lastError"
}

try {
    Invoke-SmokeRequest "/" | Out-Null
    Invoke-SmokeRequest "/health" | Out-Null
    Invoke-SmokeRequest "/Identity/Login/Account/Login" | Out-Null
    Invoke-SmokeRequest "/swagger" | Out-Null
    $ready = Invoke-SmokeRequest "/health/ready" -Json

    if ($ready.status -ne "Ready") {
        throw "Readiness status is '$($ready.status)', expected Ready."
    }

    if (-not $ready.database.ready) {
        throw "Database readiness failed: $($ready.database.message)"
    }

    if ($ready.database.pendingMigrations -and $ready.database.pendingMigrations.Count -gt 0) {
        throw "Pending migrations: $($ready.database.pendingMigrations -join ', ')"
    }

    if ($ready.meilisearch.configured -and -not $ready.meilisearch.reachable) {
        throw "Meilisearch is configured but unreachable: $($ready.meilisearch.message)"
    }

    Write-Host "Render smoke check passed for $base"
}
finally {
    $client.Dispose()
    $handler.Dispose()
}
