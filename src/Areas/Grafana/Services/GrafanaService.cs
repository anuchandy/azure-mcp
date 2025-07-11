// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// cSpell:ignore Grafanas

using AzureMcp.LocalServiceClient.Arm;
using AzureMcp.LocalServiceClient.Identity;
using AzureMcp.Options;
using AzureMcp.Services.Azure;
using AzureMcp.Services.Azure.Tenant;

namespace AzureMcp.Areas.Grafana.Services;

public class GrafanaService(IArmServiceClient armServiceClient, IIdentityServiceClient credentialService, ITenantService tenantService)
    : BaseAzureService(credentialService, tenantService), IGrafanaService
{
    private readonly IArmServiceClient _armServiceClient = armServiceClient ?? throw new ArgumentNullException(nameof(armServiceClient));

    public async Task<IEnumerable<Models.Workspace.Workspace>> ListWorkspacesAsync(
        string subscriptionId,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscriptionId);

        try
        {
            var workspaces = await _armServiceClient.ListGrafanaWorkspacesAsync(subscriptionId, tenant);
            return workspaces;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to list Grafana workspaces: {ex.Message}", ex);
        }
    }
}
