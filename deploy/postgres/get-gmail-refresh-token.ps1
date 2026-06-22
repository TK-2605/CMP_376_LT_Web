param(
    [Parameter(Mandatory = $true)]
    [string]$ClientId,

    [Parameter(Mandatory = $true)]
    [string]$ClientSecret,

    [string]$RedirectUri = 'http://127.0.0.1:53682/'
)

$ErrorActionPreference = 'Stop'

$scope = 'https://www.googleapis.com/auth/gmail.send'
$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add($RedirectUri)
$listener.Start()

$query = @{
    client_id = $ClientId
    redirect_uri = $RedirectUri
    response_type = 'code'
    scope = $scope
    access_type = 'offline'
    prompt = 'consent'
}.GetEnumerator() | ForEach-Object {
    '{0}={1}' -f [uri]::EscapeDataString($_.Key), [uri]::EscapeDataString([string]$_.Value)
}

$authUrl = 'https://accounts.google.com/o/oauth2/v2/auth?' + ($query -join '&')

Write-Host 'Opening Google consent screen...'
Start-Process $authUrl

try {
    $context = $listener.GetContext()
    $code = $context.Request.QueryString['code']
    $errorValue = $context.Request.QueryString['error']

    $html = if ($code) {
        '<html><body><h1>QuizHub Gmail API connected</h1><p>You can close this tab.</p></body></html>'
    } else {
        "<html><body><h1>Google consent failed</h1><p>$errorValue</p></body></html>"
    }

    $buffer = [System.Text.Encoding]::UTF8.GetBytes($html)
    $context.Response.ContentType = 'text/html; charset=utf-8'
    $context.Response.ContentLength64 = $buffer.Length
    $context.Response.OutputStream.Write($buffer, 0, $buffer.Length)
    $context.Response.Close()

    if ([string]::IsNullOrWhiteSpace($code)) {
        throw "Google did not return an authorization code. Error: $errorValue"
    }
}
finally {
    $listener.Stop()
}

$token = Invoke-RestMethod -Method Post -Uri 'https://oauth2.googleapis.com/token' -Body @{
    client_id = $ClientId
    client_secret = $ClientSecret
    code = $code
    redirect_uri = $RedirectUri
    grant_type = 'authorization_code'
}

if ([string]::IsNullOrWhiteSpace($token.refresh_token)) {
    throw 'Google did not return refresh_token. Re-run this script with consent prompt, or remove old QuizHub consent from your Google Account and try again.'
}

Write-Host ''
Write-Host 'Set these Render environment variables:'
Write-Host "GmailApi__ClientId=$ClientId"
Write-Host "GmailApi__ClientSecret=$ClientSecret"
Write-Host "GmailApi__RefreshToken=$($token.refresh_token)"
Write-Host 'GmailApi__FromEmail=<the Gmail address you just authorized>'
Write-Host 'GmailApi__FromName=QuizHub'
