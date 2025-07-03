
## Setup
cd /Users/anuchandy/code/azure-mcp

## Clean and Build
dotnet build localservices/AzureMcp.LocalService.Identity/AzureMcp.LocalService.Identity.csproj
dotnet build localservices/AzureMcp.LocalService.Arm/AzureMcp.LocalService.Arm.csproj

dotnet clean localservices/AzureMcp.LocalService.Identity/AzureMcp.LocalService.Identity.csproj
dotnet clean localservices/AzureMcp.LocalService.Arm/AzureMcp.LocalService.Arm.csproj

## Test Steps

### 1. Start Identity LocalService
cd localservices/AzureMcp.LocalService.Identity
ASPNETCORE_URLS="http://localhost:5000" dotnet run &

### 2. Start ARM LocalService with Identity LocalService endpoint
cd ../AzureMcp.LocalService.Arm
(export ASPNETCORE_URLS="http://localhost:5001"; export AzureMcp__LocalService__Arm__IdentityServiceEndpoint="http://localhost:5000"; dotnet run) &

### 3. Test gRPC call on ARM LocalService
grpcurl -plaintext -import-path ./protos -proto arm_service.proto -d '{"scopes":["https://management.azure.com/.default"]}' localhost:5001 arm.ArmService/GetIdentityServiceStatus

### 4. Cleanup
pkill -f "AzureMcp.LocalService"