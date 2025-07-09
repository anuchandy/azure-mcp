#!/usr/bin/env pwsh
#Requires -Version 7

param(
    [string]$Runtime
)

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
$rootPath = Split-Path (Split-Path $scriptDir)

if (-not $Runtime) {
    $runtime = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
} else {
    $runtime = $Runtime
}

$buildAot = Join-Path $scriptDir "build-aot.ps1"
if (-not (Test-Path $buildAot)) {
    throw "build-aot.ps1 script not found at: $buildAot"
}

& $buildAot -Runtime $runtime
if ($LASTEXITCODE -ne 0) {
    throw "AOT build failed with exit code: $LASTEXITCODE"
}

$localServicesDir = Join-Path $rootPath "localservices"
$serviceProjects = @(
    "AzureMcp.LocalService.Arm",
    "AzureMcp.LocalService.CosmosDB", 
    "AzureMcp.LocalService.Identity"
)

foreach ($service in $serviceProjects) {
    $serviceDir = Join-Path $localServicesDir $service
    $serviceCsproj = Join-Path $serviceDir "$service.csproj"
    
    & dotnet clean $serviceCsproj --configuration Release --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to clean $service"
    }
    
    & dotnet build $serviceCsproj --configuration Release --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build $service"
    }
    
    $servicePublishDir = Join-Path $serviceDir "bin/Release/net9.0/publish"
    & dotnet publish $serviceCsproj --configuration Release --output $servicePublishDir --verbosity quiet /p:DebugType=None
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to publish $service"
    }
}

$mainPublishDir = Join-Path $rootPath "src/bin/Release/net9.0/$runtime/publish"
if (-not (Test-Path $mainPublishDir)) {
    throw "Main publish directory not found: $mainPublishDir"
}

$localServicesTargetDir = Join-Path $mainPublishDir "localservices"
New-Item -Path $localServicesTargetDir -ItemType Directory -Force | Out-Null

foreach ($service in $serviceProjects) {
    $servicePublishDir = Join-Path $localServicesDir "$service/bin/Release/net9.0/publish"
    $serviceTargetDir = Join-Path $localServicesTargetDir $service
    
    if (-not (Test-Path $servicePublishDir)) {
        throw "Service publish directory not found: $servicePublishDir"
    }
    Copy-Item -Path $servicePublishDir -Destination $serviceTargetDir -Recurse -Force
}

$mainExecutable = Get-ChildItem -Path $mainPublishDir -Filter "azmcp*" -File | Where-Object { $_.Extension -eq ".exe" -or $_.Extension -eq "" } | Select-Object -First 1
Write-Host ""
Write-Host "cd $mainPublishDir" -ForegroundColor Yellow
Write-Host ""
Write-Host "Start SSE mode: ./azmcp server start --transport sse" -ForegroundColor Cyan
