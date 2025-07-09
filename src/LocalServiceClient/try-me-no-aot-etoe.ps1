#!/usr/bin/env pwsh

param(
    [string]$WorkspaceName = "try-me-temp"
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$WorkspaceDir = Join-Path $ProjectRoot $WorkspaceName

function Build-AllProjects {
    $Projects = @(
        @{ Path = "localservices/AzureMcp.LocalService.Identity"; Name = "Identity Service" },
        @{ Path = "localservices/AzureMcp.LocalService.Arm"; Name = "ARM Service" },
        @{ Path = "localservices/AzureMcp.LocalService.CosmosDB"; Name = "CosmosDB Service" },
        @{ Path = "src"; Name = "Azure MCP" }
    )
    
    foreach ($Project in $Projects) {
        $ProjectDir = Join-Path $ProjectRoot $Project.Path
        Invoke-DotnetCommand -Arguments "clean", "build" -WorkingDirectory $ProjectDir -ProjectName $Project.Name
    }
}

function Invoke-DotnetCommand {
    param(
        [string[]]$Arguments,
        [string]$WorkingDirectory,
        [string]$ProjectName
    )
    
    foreach ($Arg in $Arguments) {
        Write-Host "$Arg $ProjectName" -ForegroundColor Yellow
        $process = Start-Process -FilePath "dotnet" -ArgumentList $Arg -WorkingDirectory $WorkingDirectory -Wait -PassThru -NoNewWindow
        if ($process.ExitCode -ne 0) {
            throw "Failed to $Arg $ProjectName (Exit code: $($process.ExitCode))"
        }
        Write-Host "Completed: $Arg $ProjectName" -ForegroundColor Green
    }
}

function New-WorkspaceDirectory {
    Write-Host "Setting up workspace directory" -ForegroundColor Yellow
    
    if (Test-Path $WorkspaceDir) {
        Remove-Item -Path $WorkspaceDir -Recurse -Force
    }

    $VsCodeDir = New-Item -Path (Join-Path $WorkspaceDir ".vscode") -ItemType Directory -Force
    $AzmcpDllPath = Join-Path $ProjectRoot "src" "bin" "Debug" "net9.0" "azmcp.dll"
    
    @{
        servers = @{
            "Azure MCP Server" = @{
                command = "dotnet"
                args = @($AzmcpDllPath, "server", "start")
            }
        }
    } | ConvertTo-Json -Depth 10 | Out-File -FilePath (Join-Path $VsCodeDir "mcp.json") -Encoding UTF8
    
    Write-Host "Created workspace with mcp.json at: $AzmcpDllPath" -ForegroundColor Green
}

function Find-VsCodeCommand {
    foreach ($Command in @("code", "code-insiders")) {
        if (Get-Command $Command -ErrorAction SilentlyContinue) {
            return $Command
        }
    }
    
    $isWin = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
    if ($isWin) {
        $Paths = @(
            "$env:LOCALAPPDATA\Programs\Microsoft VS Code\bin\code.cmd",
            "$env:PROGRAMFILES\Microsoft VS Code\bin\code.cmd",
            "${env:PROGRAMFILES(X86)}\Microsoft VS Code\bin\code.cmd"
        )
    } else {
        $Paths = @("/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code")
    }
    foreach ($Path in $Paths) {
        if (Test-Path $Path) { return $Path }
    }
    return $null
}

try {
    Build-AllProjects
    New-WorkspaceDirectory
    $VsCodeCommand = Find-VsCodeCommand
    
    if ($VsCodeCommand) {
        Write-Host "Opening VS Code with workspace: $WorkspaceDir" -ForegroundColor Green
        Start-Process -FilePath $VsCodeCommand -ArgumentList $WorkspaceDir -NoNewWindow
    } else {
        Write-Host "VS Code not found in PATH. Try opening the directory manually:" -ForegroundColor Yellow
        Write-Host "$WorkspaceDir" -ForegroundColor Cyan
    }
    
} catch {
    Write-Host "`nAn error occurred: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Error details: $($_.ScriptStackTrace)" -ForegroundColor Red
    exit 1
}
