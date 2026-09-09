# build_all.ps1 - Automated build and native single-file compilation

param(
    [string[]]$Platforms = @("win-x64"),
    [string]$Configuration = "Release",
    [string]$OutputDir = "./bin",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

Write-Host "=== Mic Helper Build Pipeline ===" -ForegroundColor Cyan

# Locate dotnet with SDK
$dotnet = "dotnet"
$hasSdk = $false
try {
    $sdks = & dotnet --list-sdks 2>$null
    if ($sdks) { $hasSdk = $true }
} catch {}

if (-not $hasSdk) {
    $userDotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
    if (Test-Path $userDotnet) {
        $dotnet = $userDotnet
        $env:DOTNET_ROOT = Split-Path $userDotnet
        $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
    } else {
        throw ".NET SDK not found. Please run dev_prepare.ps1 first."
    }
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

# Ensure workspace-safe environment variables
if (-not $env:DOTNET_CLI_HOME) {
    $env:DOTNET_CLI_HOME = Join-Path $scriptDir ".dotnet_home"
}
if (-not $env:APPDATA) {
    $env:APPDATA = Join-Path $env:DOTNET_CLI_HOME "appdata"
}
if (-not $env:NUGET_PACKAGES) {
    $env:NUGET_PACKAGES = Join-Path $env:DOTNET_CLI_HOME ".nuget\packages"
}

if (-not $Version) {
    if ($env:APP_VERSION) {
        $Version = $env:APP_VERSION
    } else {
        try {
            $tag = & git describe --tags --abbrev=0 2>$null
            if ($tag) { $Version = $tag.Trim().TrimStart('v') }
        } catch {}
    }
}
if (-not $Version) {
    $Version = "0.1.0"
}
$cleanAssemblyVer = ($Version -split '-')[0]
Write-Host "Target Version: $Version (AssemblyVersion: $cleanAssemblyVer)" -ForegroundColor Cyan

$absOutputDir = [System.IO.Path]::GetFullPath((Join-Path $scriptDir $OutputDir))
if (-not (Test-Path $absOutputDir)) {
    New-Item -ItemType Directory -Path $absOutputDir -Force | Out-Null
}

Write-Host "Restoring solution dependencies..." -ForegroundColor Cyan
& $dotnet restore MicHelper.sln -m:1
if ($LASTEXITCODE -ne 0) {
    throw "Restore failed with exit code $LASTEXITCODE"
}

Write-Host "Running tests..." -ForegroundColor Cyan
& $dotnet test tests/MicHelper.Tests/MicHelper.Tests.csproj -c $Configuration --no-restore -m:1
if ($LASTEXITCODE -ne 0) {
    throw "Tests failed with exit code $LASTEXITCODE"
}

$serverProj = Join-Path $scriptDir "src/MicHelper.Server/MicHelper.Server.csproj"
$clientProj = Join-Path $scriptDir "src/MicHelper.Client/MicHelper.Client.csproj"

foreach ($rid in $Platforms) {
    Write-Host "Publishing Server and Client for $rid ($Configuration)..." -ForegroundColor Cyan
    
    $platformOutDir = if ($Platforms.Count -gt 1) { Join-Path $absOutputDir $rid } else { $absOutputDir }
    if (-not (Test-Path $platformOutDir)) {
        New-Item -ItemType Directory -Path $platformOutDir -Force | Out-Null
    }

    Write-Host "Publishing Server -> $platformOutDir" -ForegroundColor Yellow
    & $dotnet publish $serverProj -c $Configuration -r $rid --self-contained true `
        -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishSingleFile=true -m:1 -o $platformOutDir `
        -p:Version=$Version -p:AssemblyVersion=$cleanAssemblyVer -p:FileVersion=$cleanAssemblyVer -p:InformationalVersion=$Version
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to publish Server for $rid"
    }

    Write-Host "Publishing Client -> $platformOutDir" -ForegroundColor Yellow
    & $dotnet publish $clientProj -c $Configuration -r $rid --self-contained true `
        -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishSingleFile=true -m:1 -o $platformOutDir `
        -p:Version=$Version -p:AssemblyVersion=$cleanAssemblyVer -p:FileVersion=$cleanAssemblyVer -p:InformationalVersion=$Version
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to publish Client for $rid"
    }
}

Write-Host "=== Build Complete ===" -ForegroundColor Green
Get-ChildItem -Path $absOutputDir -Filter "*.exe" -Recurse | ForEach-Object {
    Write-Host "  -> $($_.FullName) ($([math]::Round($_.Length / 1MB, 2)) MB)" -ForegroundColor Green
}
