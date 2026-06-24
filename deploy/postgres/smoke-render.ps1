param(
    [string]$BaseUrl = "https://quizhub-nhom4.onrender.com",
    [int]$Attempts = 5,
    [int]$DelaySeconds = 20,
    [string]$AdminEmail = $env:QUIZHUB_SMOKE_ADMIN_EMAIL,
    [string]$AdminPassword = $env:QUIZHUB_SMOKE_ADMIN_PASSWORD,
    [string]$TestEmailTo = $env:QUIZHUB_SMOKE_TEST_EMAIL_TO
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

function Invoke-SmokeJsonPost {
    param(
        [string]$Path,
        [object]$Body,
        [string]$BearerToken = ""
    )

    $url = "$base$Path"
    $json = $Body | ConvertTo-Json -Depth 8 -Compress
    $content = [System.Net.Http.StringContent]::new($json, [System.Text.Encoding]::UTF8, "application/json")
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, $url)
    $request.Content = $content

    if (-not [string]::IsNullOrWhiteSpace($BearerToken)) {
        $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $BearerToken)
    }

    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    $bodyText = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $statusCode = [int]$response.StatusCode
    if ($statusCode -lt 200 -or $statusCode -ge 300) {
        throw "JSON POST $Path failed with HTTP ${statusCode}: $bodyText"
    }

    Write-Host "OK $statusCode $Path"
    if ([string]::IsNullOrWhiteSpace($bodyText)) {
        return $null
    }

    return $bodyText | ConvertFrom-Json
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

    $emailSmokeEnabled = -not [string]::IsNullOrWhiteSpace($AdminEmail) `
        -and -not [string]::IsNullOrWhiteSpace($AdminPassword) `
        -and -not [string]::IsNullOrWhiteSpace($TestEmailTo)

    if ($emailSmokeEnabled) {
        $login = Invoke-SmokeJsonPost "/api/auth/login" @{
            email = $AdminEmail
            password = $AdminPassword
        }

        if ([string]::IsNullOrWhiteSpace($login.accessToken)) {
            throw "JWT login did not return an access token."
        }

        $emailResult = Invoke-SmokeJsonPost "/api/auth/test-email" @{
            toEmail = $TestEmailTo
            subject = "QuizHub live smoke email provider"
        } $login.accessToken

        if (-not $emailResult.sent) {
            throw "Email provider smoke failed: $($emailResult.message)"
        }
    }
    else {
        Write-Host "Skipping authenticated email provider smoke. Set QUIZHUB_SMOKE_ADMIN_EMAIL, QUIZHUB_SMOKE_ADMIN_PASSWORD, and QUIZHUB_SMOKE_TEST_EMAIL_TO to enable it."
    }

    Write-Host "Render smoke check passed for $base"
}
finally {
    $client.Dispose()
    $handler.Dispose()
}
