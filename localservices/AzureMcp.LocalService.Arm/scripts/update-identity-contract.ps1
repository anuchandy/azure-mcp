# script to sync Identity service contract.

$IdentityProtoSource = "..\AzureMcp.LocalService.Identity\protos\identity_service.proto"
$IdentityProtoDest = "protos\identity_service.proto"

if (-not (Test-Path $IdentityProtoSource)) {
    Write-Host "Source file not found: $IdentityProtoSource" -ForegroundColor Red
    exit 1
}

Copy-Item $IdentityProtoSource $IdentityProtoDest
Write-Host "Identity service contract updated successfully" -ForegroundColor Green
