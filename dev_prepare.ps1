# dev_prepare.ps1 - Installs .NET 10 SDK and development requirements

$ErrorActionPreference = "Stop"

Write-Host "Checking .NET 10 SDK installation..." -ForegroundColor Cyan

$dotnetFound = $false
try {
    $ver = & dotnet --version 2>$null
    if ($ver -and $ver.StartsWith("10.")) {
        Write-Host ".NET 10 SDK is already installed: $ver" -ForegroundColor Green
        $dotnetFound = $true
    }
} catch {
}

if (-not $dotnetFound) {
    # Check if installed in user profile .dotnet
    $userDotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
    if (Test-Path $userDotnet) {
        $ver = & $userDotnet --version 2>$null
        if ($ver -and $ver.StartsWith("10.")) {
            Write-Host "Found .NET 10 SDK in ${userDotnet}: $ver" -ForegroundColor Green
            $env:DOTNET_ROOT = Split-Path $userDotnet
            $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
            $dotnetFound = $true
        }
    }
}

if (-not $dotnetFound) {
    # Try winget first as per README instructions
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    $wingetSuccess = $false
    if ($winget) {
        Write-Host "Attempting installation via winget..." -ForegroundColor Yellow
        try {
            & winget install --id Microsoft.DotNet.SDK.10 --exact --accept-package-agreements --accept-source-agreements --silent
            if ($LASTEXITCODE -eq 0) {
                $wingetSuccess = $true
                Write-Host ".NET 10 SDK successfully installed via winget." -ForegroundColor Green
            }
        } catch {
            Write-Warning "winget install failed or required elevation."
        }
    }

    if (-not $wingetSuccess) {
        Write-Host "Installing .NET 10 SDK via dotnet-install.ps1..." -ForegroundColor Yellow
        $installScript = Join-Path $env:TEMP "dotnet-install.ps1"
        Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript
        $targetDir = Join-Path $env:USERPROFILE ".dotnet"
        & $installScript -Channel 10.0 -InstallDir $targetDir
        
        $env:DOTNET_ROOT = $targetDir
        $env:PATH = "$targetDir;$env:PATH"
        
        $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
        if ($userPath -notlike "*$targetDir*") {
            [Environment]::SetEnvironmentVariable("Path", "$targetDir;$userPath", "User")
        }
    }
}

Write-Host "Verifying development environment..." -ForegroundColor Cyan
$finalVer = & dotnet --version
Write-Host "Ready! .NET SDK: $finalVer" -ForegroundColor Green
