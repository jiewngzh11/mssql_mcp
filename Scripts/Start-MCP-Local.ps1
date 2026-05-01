[CmdletBinding()]
param(
    [ValidateSet("Stdio", "Http")]
    [string]$Transport = "Stdio",

    [string]$DataDir = "",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [int]$HttpPort = 3001,

    [switch]$PrintConfig,

    [switch]$ShowApiKey,

    [switch]$UseEnvironmentSecrets
)

$ErrorActionPreference = "Stop"

function New-LocalSecret {
    $bytes = [byte[]]::new(48)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    }
    finally {
        $rng.Dispose()
    }

    return [Convert]::ToBase64String($bytes)
}

function Protect-LocalSecret {
    param([Parameter(Mandatory = $true)][string]$Value)

    $secure = ConvertTo-SecureString -String $Value -AsPlainText -Force
    return $secure | ConvertFrom-SecureString
}

function Unprotect-LocalSecret {
    param([Parameter(Mandatory = $true)][string]$ProtectedValue)

    $secure = $ProtectedValue | ConvertTo-SecureString
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    }
}

function Get-OrCreate-LocalSecret {
    param(
        [Parameter(Mandatory = $true)][string]$Path
    )

    if (Test-Path -LiteralPath $Path) {
        $protected = Get-Content -LiteralPath $Path -Raw
        return Unprotect-LocalSecret -ProtectedValue $protected.Trim()
    }

    $secret = New-LocalSecret
    $protectedSecret = Protect-LocalSecret -Value $secret
    Set-Content -LiteralPath $Path -Value $protectedSecret -NoNewline
    return $secret
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "mssqlMCP.csproj"
$targetFramework = "net9.0"

if ([string]::IsNullOrWhiteSpace($DataDir)) {
    $localAppData = $env:LOCALAPPDATA
    if ([string]::IsNullOrWhiteSpace($localAppData)) {
        $localAppData = Join-Path $env:USERPROFILE "AppData\Local"
    }

    $DataDir = Join-Path $localAppData "mssqlMCP"
}

$DataDir = [System.IO.Path]::GetFullPath($DataDir)
New-Item -ItemType Directory -Force -Path $DataDir | Out-Null

if (-not $UseEnvironmentSecrets -or [string]::IsNullOrWhiteSpace($env:MSSQL_MCP_KEY)) {
    $env:MSSQL_MCP_KEY = Get-OrCreate-LocalSecret -Path (Join-Path $DataDir "mssql_mcp_key.dpapi")
}

if (-not $UseEnvironmentSecrets -or [string]::IsNullOrWhiteSpace($env:MSSQL_MCP_API_KEY)) {
    $env:MSSQL_MCP_API_KEY = Get-OrCreate-LocalSecret -Path (Join-Path $DataDir "mssql_mcp_api_key.dpapi")
}

$env:MSSQL_MCP_DATA = $DataDir
$env:MSSQL_MCP_CONNECTION_STORE_PROVIDER = "Sqlite"
$env:MSSQL_MCP_TRANSPORT = $Transport

if ($Transport -eq "Http") {
    $env:ASPNETCORE_URLS = "http://localhost:$HttpPort"
}

$userDotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $userDotnet) { $userDotnet } else { "dotnet" }
$dllPath = Join-Path $repoRoot "bin\$Configuration\$targetFramework\mssqlMCP.dll"

if ($PrintConfig) {
    [pscustomobject]@{
        Transport = $Transport
        DataDir = $DataDir
        Dotnet = $dotnet
        ProjectPath = $projectPath
        DllPath = $dllPath
        HttpUrl = if ($Transport -eq "Http") { "http://localhost:$HttpPort/mcp" } else { $null }
        HasMssqlMcpKey = -not [string]::IsNullOrWhiteSpace($env:MSSQL_MCP_KEY)
        HasMssqlMcpApiKey = -not [string]::IsNullOrWhiteSpace($env:MSSQL_MCP_API_KEY)
    } | ConvertTo-Json -Depth 3
    return
}

if ($ShowApiKey) {
    $env:MSSQL_MCP_API_KEY
    return
}

if (-not (Test-Path -LiteralPath $dllPath)) {
    [Console]::Error.WriteLine("Build the project before starting local MCP: & `"$dotnet`" build --no-restore")
    exit 1
}

& $dotnet $dllPath
exit $LASTEXITCODE
