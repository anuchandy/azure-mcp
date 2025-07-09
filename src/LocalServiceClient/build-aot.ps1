#!/bin/env pwsh
#Requires -Version 7

param(
    [string]$Runtime
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot/../../eng/scripts/AOT-Config.ps1"
$config = Get-AOTConfig

$root = $config.RootPath
$projectFile = $config.ProjectFile

if (-not $Runtime) {
    $runtime = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
} else {
    $runtime = $Runtime
}

# dotnet publish default output location: bin/Release/net9.0/{runtime}/publish
$defaultOutputDir = Join-Path (Split-Path $projectFile) "bin/Release/net9.0/$runtime/publish"

function Initialize-AOTBuildDirectories {
    param(
        [string]$projectFile
    )
    
    $projectObjDir = Join-Path (Split-Path $projectFile) "obj"
    if (Test-Path $projectObjDir) {
        
        Remove-Item -Path $projectObjDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    $projectBinDir = Join-Path (Split-Path $projectFile) "bin"
    if (Test-Path $projectBinDir) {
        Remove-Item -Path $projectBinDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Building AOT (runtime: $runtime)" -ForegroundColor Green

Initialize-AOTBuildDirectories -projectFile $projectFile

$publishArgs = @(
    'publish', $projectFile,
    '--configuration', 'Release',
    '--runtime', $runtime,
    '--self-contained', 'true',
    '/p:PublishAot=true',
    '/p:PublishTrimmed=true',  # Override settings in AzureMcp.csproj
    '/p:TreatWarningsAsErrors=false'  # Do not fail on AOT warnings
)

Write-Host "Executing: dotnet $($publishArgs -join ' ')" -ForegroundColor Cyan

$output = & dotnet @publishArgs 2>&1
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Host "AOT build succeeded." -ForegroundColor Green
    if (Test-Path $defaultOutputDir) {
        $executable = Get-ChildItem -Path $defaultOutputDir -Filter "azmcp*" -File | Where-Object { $_.Extension -eq ".exe" -or $_.Extension -eq "" }
        if ($executable) {
            $size = [math]::Round($executable.Length / 1MB, 2)
            Write-Host "Exe: $($executable.FullName)" -ForegroundColor Green
            Write-Host "File size: $size MB" -ForegroundColor Green
        }
    } else {
        Write-Host "Expected output dir not found: $defaultOutputDir" -ForegroundColor Yellow
    }
} else {
    Write-Host "AOT build failed: exit-code: $exitCode" -ForegroundColor Red
    Write-Host "Output:" -ForegroundColor Yellow
    $output | Write-Host
}

exit $exitCode
