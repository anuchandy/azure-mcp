#!/bin/env pwsh
#Requires -Version 7

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Regular','Native','Both')]
    [string] $BuildType,
    [ValidateSet('x64','arm64')]
    [string] $Architecture,
    [switch] $RegularKeepPdb
)

$ErrorActionPreference = 'Stop'

. "$PSScriptRoot/../common/scripts/common.ps1"

$RepoRoot = $RepoRoot.Path.Replace('\', '/')

$projectDir = "$RepoRoot/core/src/AzureMcp.Cli"
$projectFile = "$projectDir/AzureMcp.Cli.csproj"

function Cleanup {
    param($projectFile, $outputDir, $RepoRoot)
    
    Write-Host "Cleaning up..." -ForegroundColor Yellow

    dotnet clean $projectFile | Out-Null
    
    $originalProgress = $ProgressPreference
    $ProgressPreference = 'SilentlyContinue'
    Get-ChildItem -Path $RepoRoot -Recurse -Directory | Where-Object { $_.Name -in @("bin", "obj") } | ForEach-Object {
        Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
    $ProgressPreference = $originalProgress

    if (Test-Path $outputDir) { Remove-Item $outputDir -Recurse -Force }
    
    $zippedDir = "$RepoRoot/.work/size-check/published-zipped"
    if (Test-Path $zippedDir) { Remove-Item $zippedDir -Recurse -Force }
}

function NativeArtifactSize {
    param($outputDir, $os)
    
    $extension = if ($os -eq 'win') { '.exe' } else { '' }
    $exe = "$outputDir/azmcp$extension"
    
    if (Test-Path $exe) {
        $size = (Get-Item $exe).Length
        $sizeMB = [math]::Round($size / 1MB, 2)
        return @{
            Size = $size
            SizeMB = $sizeMB
            FileCount = 1
        }
    } else {
        throw "Native executable not found: $exe"
    }
}

function RegularArtifactSize {
    param($outputDir)
    
    if (-not (Test-Path $outputDir)) {
        throw "Output directory not found: $outputDir"
    }
    
    $files = Get-ChildItem $outputDir -Recurse -File
    $totalSize = ($files | Measure-Object Length -Sum).Sum
    $sizeMB = [math]::Round($totalSize / 1MB, 2)
    
    return @{
        Size = $totalSize
        SizeMB = $sizeMB
        FileCount = $files.Count
    }
}

function CompressedRegularArtifactSize {
    param($regularOutputDir, $RepoRoot)
    
    if (-not $regularOutputDir -or -not (Test-Path $regularOutputDir)) {
        throw "Output directory not found: $regularOutputDir"
    }
    
    $zippedDir = "$RepoRoot/.work/size-check/published-zipped"
    if (-not (Test-Path $zippedDir)) {
        New-Item $zippedDir -ItemType Directory -Force | Out-Null
    }
    
    $regularTgzPath = "$zippedDir/regular.tar.gz"
    Write-Host "Compressing Self-Contained .NET package..." -ForegroundColor Gray
    tar -czf $regularTgzPath -C $regularOutputDir .
    
    if (Test-Path $regularTgzPath) {
        $size = (Get-Item $regularTgzPath).Length
        $sizeMB = [math]::Round($size / 1MB, 2)
        return @{
            Size = $size
            SizeMB = $sizeMB
            Path = $regularTgzPath
        }
    }
    
    throw "Failed to create compressed regular package: $regularTgzPath"
}

function CompressedNativeArtifactSize {
    param($nativeOutputDir, $os, $RepoRoot)
    
    if (-not $nativeOutputDir -or -not (Test-Path $nativeOutputDir)) {
        throw "Output directory not found: $nativeOutputDir"
    }
    
    $extension = if ($os -eq 'win') { '.exe' } else { '' }
    $exe = "$nativeOutputDir/azmcp$extension"
    
    if (-not (Test-Path $exe)) {
        throw "Native executable not found: $exe"
    }
    
    $zippedDir = "$RepoRoot/.work/size-check/published-zipped"
    if (-not (Test-Path $zippedDir)) {
        New-Item $zippedDir -ItemType Directory -Force | Out-Null
    }
    
    $nativeTgzPath = "$zippedDir/native.tar.gz"
    Write-Host "Compressing Native executable..." -ForegroundColor Gray
    tar -czf $nativeTgzPath -C $nativeOutputDir "azmcp$extension"
    
    if (Test-Path $nativeTgzPath) {
        $size = (Get-Item $nativeTgzPath).Length
        $sizeMB = [math]::Round($size / 1MB, 2)
        return @{
            Size = $size
            SizeMB = $sizeMB
            Path = $nativeTgzPath
        }
    }
    
    throw "Failed to create compressed native package: $nativeTgzPath"
}

function Print-Comparison {
    param($sizes, $compressedSizes)
    
    if ($sizes.Regular.SizeMB -gt $sizes.Native.SizeMB) {
        $ratio = [math]::Round($sizes.Regular.SizeMB / $sizes.Native.SizeMB, 2)
        Write-Host "Self-contained .NET package is ${ratio}x larger than Native" -ForegroundColor White
    } elseif ($sizes.Native.SizeMB -gt $sizes.Regular.SizeMB) {
        $ratio = [math]::Round($sizes.Native.SizeMB / $sizes.Regular.SizeMB, 2)
        Write-Host "Native is ${ratio}x larger than Self-contained .NET package" -ForegroundColor White
    } else {
        Write-Host "Both builds are the same size" -ForegroundColor White
    }
    
    if ($compressedSizes.Regular.SizeMB -gt $compressedSizes.Native.SizeMB) {
        $compressedRatio = [math]::Round($compressedSizes.Regular.SizeMB / $compressedSizes.Native.SizeMB, 2)
        Write-Host "Self-contained .NET package (compressed) is ${compressedRatio}x larger than Native (compressed)" -ForegroundColor White
    } elseif ($compressedSizes.Native.SizeMB -gt $compressedSizes.Regular.SizeMB) {
        $compressedRatio = [math]::Round($compressedSizes.Native.SizeMB / $compressedSizes.Regular.SizeMB, 2)
        Write-Host "Native (compressed) is ${compressedRatio}x larger than Self-contained .NET package (compressed)" -ForegroundColor White
    } else {
        Write-Host "Both compressed builds are the same size" -ForegroundColor White
    }
}

function Clean-And-Publish {
    param($projectFile, $BuildType, $Architecture, $RepoRoot, $RegularKeepPdb)
    
    $runtime = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
    $os, $arch = $runtime -split '-'
    if ($Architecture) { $arch = $Architecture }
    
    $isNative = $BuildType -eq 'Native'
    $publishDirName = if ($isNative) { "published-native" } else { "published-regular" }
    $outputDir = "$RepoRoot/.work/size-check/$publishDirName"
    
    Cleanup -projectFile $projectFile -outputDir $outputDir -RepoRoot $RepoRoot
    
    New-Item $outputDir -ItemType Directory -Force | Out-Null

    $bt = if ($isNative) { "Native package" } else { "Self-contained .NET package" }
    Write-Host "Building $bt for $os-$arch..." -ForegroundColor Green
    
    $publishArgs = @(
        'publish', $projectFile
        '--runtime', "$os-$arch"
        '--output', $outputDir
        '--self-contained'
        '/p:Configuration=Release'
    )
    if ($isNative) { $publishArgs += '/p:BuildNative=true' }
    
    & dotnet $publishArgs
    
    if ($BuildType -eq 'Regular' -and -not $RegularKeepPdb) {
        $pdbFiles = Get-ChildItem -Path $outputDir -Filter "*.pdb" -Recurse
        if ($pdbFiles) {
            Write-Host "Removing $($pdbFiles.Count) PDB file(s) from Self-contained .NET package..." -ForegroundColor Gray
            $pdbFiles | Remove-Item -Force
        } else {
            Write-Host "No PDB files found in Self-contained .NET package." -ForegroundColor Gray
        }
    }

    return @{
        OutputDir = $outputDir
        OS = $os
        Architecture = $arch
    }
}

function Build-Single-Type {
    param($projectFile, $SingleBuildType, $Architecture, $RepoRoot, $RegularKeepPdb)
    
    $buildResult = Clean-And-Publish -projectFile $projectFile -BuildType $SingleBuildType -Architecture $Architecture -RepoRoot $RepoRoot -RegularKeepPdb $RegularKeepPdb
    
    if ($SingleBuildType -eq 'Native') {
        $sizeInfo = NativeArtifactSize -outputDir $buildResult.OutputDir -os $buildResult.OS
    } else {
        $sizeInfo = RegularArtifactSize -outputDir $buildResult.OutputDir
    }
    
    return @{
        SizeInfo = $sizeInfo
        OutputDir = $buildResult.OutputDir
        OS = $buildResult.OS
    }
}

Push-Location $RepoRoot
try {
    if ($BuildType -eq 'Both') {
        $regularResult = Build-Single-Type -projectFile $projectFile -SingleBuildType 'Regular' -Architecture $Architecture -RepoRoot $RepoRoot -RegularKeepPdb $RegularKeepPdb
        $nativeResult = Build-Single-Type -projectFile $projectFile -SingleBuildType 'Native' -Architecture $Architecture -RepoRoot $RepoRoot -RegularKeepPdb $false

        $sizes = @{
            Regular = $regularResult.SizeInfo
            Native = $nativeResult.SizeInfo
        }

        $regularCompressed = CompressedRegularArtifactSize -regularOutputDir $regularResult.OutputDir -RepoRoot $RepoRoot
        $nativeCompressed = CompressedNativeArtifactSize -nativeOutputDir $nativeResult.OutputDir -os $nativeResult.OS -RepoRoot $RepoRoot
        
        $compressedSizes = @{
            Regular = $regularCompressed
            Native = $nativeCompressed
        }

        Write-Host "`nSelf-contained .NET package: $($sizes.Regular.SizeMB) MB ($($sizes.Regular.Size) bytes, $($sizes.Regular.FileCount) files)" -ForegroundColor White
        Write-Host "Native: $($sizes.Native.SizeMB) MB ($($sizes.Native.Size) bytes)" -ForegroundColor White
        Write-Host "Self-contained .NET package (compressed): $($compressedSizes.Regular.SizeMB) MB ($($compressedSizes.Regular.Size) bytes)" -ForegroundColor White
        Write-Host "Native (compressed): $($compressedSizes.Native.SizeMB) MB ($($compressedSizes.Native.Size) bytes)" -ForegroundColor White

        Print-Comparison -sizes $sizes -compressedSizes $compressedSizes
    } else {
        $result = Build-Single-Type -projectFile $projectFile -SingleBuildType $BuildType -Architecture $Architecture -RepoRoot $RepoRoot -RegularKeepPdb $RegularKeepPdb
        
        if ($BuildType -eq 'Regular') {
            Write-Host "`nSelf-contained .NET package: $($result.SizeInfo.SizeMB) MB ($($result.SizeInfo.Size) bytes, $($result.SizeInfo.FileCount) files)" -ForegroundColor White
        } else {
            Write-Host "`nNative: $($result.SizeInfo.SizeMB) MB ($($result.SizeInfo.Size) bytes)" -ForegroundColor White
        }
        
        if ($BuildType -eq 'Regular') {
            $regularCompressed = CompressedRegularArtifactSize -regularOutputDir $result.OutputDir -RepoRoot $RepoRoot
            Write-Host "Self-contained .NET package (compressed): $($regularCompressed.SizeMB) MB ($($regularCompressed.Size) bytes)" -ForegroundColor White
        } else {
            $nativeCompressed = CompressedNativeArtifactSize -nativeOutputDir $result.OutputDir -os $result.OS -RepoRoot $RepoRoot
            Write-Host "Native (compressed): $($nativeCompressed.SizeMB) MB ($($nativeCompressed.Size) bytes)" -ForegroundColor White
        }
    }
}
finally {
    Pop-Location
}
