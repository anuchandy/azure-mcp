#!/bin/env pwsh
#Requires -Version 7

[CmdletBinding()]
param(
    [string] $ArtifactsPath,
    [string] $OutputPath,
    [string] $Version,
    [switch] $UsePaths,
    [switch] $AllPlatforms
)

. "$PSScriptRoot/../../eng/common/scripts/common.ps1"
. "$PSScriptRoot/../../eng/scripts/AOT-Config.ps1"

$RepoRoot = $RepoRoot.Path.Replace('\', '/')

# dir containing the template for the NPM package.
$npmPackagePath = "$RepoRoot/eng/npm/aot-package-template"

if(!$Version) {
    $Version = & "$PSScriptRoot/../../eng/scripts/Get-Version.ps1"
}

if(!$ArtifactsPath) {
    # temporary workspace for NPM packaging.
    $ArtifactsPath = "$RepoRoot/.work/aot-npm-temp"
}

if(!$OutputPath) {
    # final output directory for NPM packages.
    $OutputPath = "$RepoRoot/.dist/aot-npm-packages"
}

function New-PlatformDirectory($runtime, $artifactsPath, $npmPackagePath) {
    Write-Host "Setting up platform directory for $runtime" -ForegroundColor Green
    
    $platformDir = "$artifactsPath/$runtime"
    Remove-Item -Path $platformDir -Recurse -Force -ErrorAction SilentlyContinue -ProgressAction SilentlyContinue
    New-Item -Path "$platformDir/dist" -ItemType Directory -Force | Out-Null
    
    # Copy the NPM package template
    if (!(Test-Path $npmPackagePath)) {
        throw "NPM package template path does not exist: $npmPackagePath"
    }
    Copy-Item -Path "$npmPackagePath/*" -Recurse -Destination $platformDir -Force
    if (!(Test-Path "$platformDir/package.json")) {
        throw "Failed to copy package.json template to $platformDir"
    }
    if (!(Test-Path "$platformDir/index.js")) {
        throw "Failed to copy index.js template to $platformDir"
    }
    return $platformDir
}

function Build-AOTAzMcp($runtime, $platformDir) {
    Write-Host "Building AOT azmcp for $runtime." -ForegroundColor Yellow
    
    # Build the azmcp (AOT)
    & "$PSScriptRoot/build-aot.ps1" -Runtime $runtime | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "AOT azmcp build failed for $runtime"
    }
    # Copy AOT azmcp build to dist folder
    $aotPublishDir = "$RepoRoot/src/bin/Release/net9.0/$runtime/publish"
    if (Test-Path $aotPublishDir) {
        Copy-Item -Path "$aotPublishDir/*" -Destination "$platformDir/dist/" -Recurse -Force
    } else {
        throw "AOT publish directory not found: $aotPublishDir"
    }
}

function Build-NonAOTLocalServices($platformDir) {
    $localServicesDir = "$platformDir/localservices"
    $localServiceProjects = @(
        "AzureMcp.LocalService.Identity",
        "AzureMcp.LocalService.Arm",
        "AzureMcp.LocalService.CosmosDB"
    )

    New-Item -Path $localServicesDir -ItemType Directory -Force | Out-Null
    
    foreach ($project in $localServiceProjects) {
        $projectPath = "$RepoRoot/localservices/$project/$project.csproj"
        if (Test-Path $projectPath) {
            Write-Host "Building $project" -ForegroundColor Cyan
            
            # Build the local service (non-AOT)
            & dotnet publish $projectPath --configuration Release --output "$localServicesDir/$project" --no-self-contained | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to build required local service: $project"
            }
        } else {
            throw "Required local service project not found: $projectPath"
        }
    }
}

function Update-PackageJson($os, $arch, $platformDir, $version) {
    Write-Host "Updating package.json for $os-$arch" -ForegroundColor Yellow

    switch($os) {
        'win' { $node_os = 'win32'; $extension = '.exe' }
        'osx' { $node_os = 'darwin'; $extension = '' }
        default { $node_os = $os; $extension = '' }
    }
    
    $package = Get-Content "$platformDir/package.json" -Raw
    $package = $package.Replace('{os}', $node_os)
    $package = $package.Replace('{cpu}', $arch)
    $package = $package.Replace('{version}', $version)
    $package = $package.Replace('{executable}', "azmcp$extension")
    
    Set-Content -Path "$platformDir/package.json" -Value $package
}

function Build-Module($os, $arch) {
    $runtime = "$os-$arch"
    Write-Host "Building module for $runtime" -ForegroundColor Green
    
    try {
        # Create platform-specific directory and setup.
        $platformDir = New-PlatformDirectory -runtime $runtime -artifactsPath $ArtifactsPath -npmPackagePath $npmPackagePath
        
        # Build AOT azmcp
        Build-AOTAzMcp -runtime $runtime -platformDir $platformDir
        
        # Build non-AOT local services.
        Build-NonAOTLocalServices -platformDir $platformDir
        
        # Update package.json with platform-specific information.
        Update-PackageJson -os $os -arch $arch -platformDir $platformDir -version $Version
        
        return $true
    }
    catch {
        Write-Error "Failed to build module for $runtime`: $_"
        return $false
    }
}

function Get-SupportedPlatforms {
    if ($AllPlatforms) {
        return @(
            @{os='win'; arch='x64'},
            @{os='linux'; arch='x64'},
            @{os='osx'; arch='x64'},
            @{os='osx'; arch='arm64'}
        )
    } else {
        # Identify current platform.
        $runtime = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
        $parts = $runtime.Split('-')
        $os = $parts[0]
        $arch = $parts[1]
        return @(@{os=$os; arch=$arch})
    }
}

function Build-AllPlatforms {
    $platforms = Get-SupportedPlatforms
    $builtPlatforms = @()
    
    foreach ($platform in $platforms) {
        $success = Build-Module -os $platform.os -arch $platform.arch
        if ($success) {
            $builtPlatforms += $platform
        }
    }
    
    if ($builtPlatforms.Count -eq 0) {
        throw "No platforms were successfully built"
    }
    
    Write-Host "Successfully built $($builtPlatforms.Count) platform(s)" -ForegroundColor Green
    return $builtPlatforms
}

# Main
try {
    Write-Host "Starting azmcp AOT module packaging." -ForegroundColor Cyan
    
    # Clean and create directories
    Remove-Item -Path $ArtifactsPath -Recurse -Force -ErrorAction SilentlyContinue -ProgressAction SilentlyContinue
    Remove-Item -Path $OutputPath -Recurse -Force -ErrorAction SilentlyContinue -ProgressAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path "$OutputPath/platform" | Out-Null
    
    Push-Location $RepoRoot
    try {
        $builtPlatforms = Build-AllPlatforms
        
        # Package each built platform
        Write-Host "Packaging built platforms..." -ForegroundColor Cyan
        
        foreach ($platform in $builtPlatforms) {
            $runtime = "$($platform.os)-$($platform.arch)"
            $packageFolder = "$ArtifactsPath/$runtime"
            
            if (!(Test-Path $packageFolder)) {
                Write-Warning "Package folder not found: $packageFolder"
                continue
            }
            
            if (!$IsWindows) {
                # Set executable permissions on non-Windows
                Write-Host "Setting executable permissions for $packageFolder/index.js" -ForegroundColor Yellow
                & chmod +x "$packageFolder/index.js"
                
                if ($platform.os -ne 'win') {
                    $executable = Get-ChildItem -Path "$packageFolder/dist" -Filter "azmcp*" | Where-Object { $_.Extension -eq "" }
                    if ($executable) {
                        Write-Host "Setting executable permissions for $($executable.FullName)" -ForegroundColor Yellow
                        & chmod +x $executable.FullName
                    }
                }
            }
            
            # Copy documentation
            Copy-Item -Path "$RepoRoot/README.md" -Destination $packageFolder -Force
            Copy-Item -Path "$RepoRoot/LICENSE" -Destination $packageFolder -Force
            
            # Create NPM package
            Write-Host "Packaging $runtime" -ForegroundColor Yellow
            $packResult = & npm pack $packageFolder --pack-destination "$OutputPath/platform" 2>&1
            if ($LASTEXITCODE -eq 0) {
                $fileName = ($packResult | Select-Object -Last 1).Trim()
                Write-Host "Created: $fileName" -ForegroundColor Green
            } else {
                Write-Error "Failed to package $runtime"
            }
        }
        
        Write-Host ""
        Write-Host "azmcp AOT packaging completed successfully" -ForegroundColor Green
        Write-Host ""
        Write-Host "Generated NPM Package(s):" -ForegroundColor Cyan
        Get-ChildItem "$OutputPath/platform" -File | ForEach-Object {
            $size = [math]::Round($_.Length / 1MB, 2)
            Write-Host "  $($_.Name) ($size MB)" -ForegroundColor Gray
        }
        Write-Host ""
        Write-Host " An NPM Package contains:" -ForegroundColor Yellow
        Write-Host "   1. AOT-compiled azmcp executable (main binary)" -ForegroundColor Gray
        Write-Host "   2. Non-AOT local services:" -ForegroundColor Gray
        foreach ($project in @("Identity", "Arm", "CosmosDB")) {
            Write-Host "    - AzureMcp.LocalService.$project" -ForegroundColor Gray
        }
    } finally {
        Pop-Location
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
