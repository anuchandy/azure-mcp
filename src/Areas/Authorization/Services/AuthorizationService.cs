// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Areas.Authorization.Models;
using AzureMcp.Options;
using AzureMcp.LocalServiceClient.Arm;
using AzureMcp.Services.Azure.Tenant;
using AzureMcp.LocalServiceClient.Identity;
using AzureMcp.Services.Azure;

namespace AzureMcp.Areas.Authorization.Services;

public class AuthorizationService(IArmServiceClient armService, ITenantService tenantService, IIdentityServiceClient credentialService) 
: BaseAzureService(credentialService, tenantService), IAuthorizationService
{
    private readonly IArmServiceClient _armService = armService ?? throw new ArgumentNullException(nameof(armService));

    public async Task<List<RoleAssignment>> ListRoleAssignments(
        string? scope,
        string? tenantId = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(scope);

        return await _armService.ListRoleAssignmentsAsync(scope!, tenantId);
    }
}
