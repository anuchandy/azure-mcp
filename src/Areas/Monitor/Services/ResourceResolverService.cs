// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using AzureMcp.LocalServiceClient.Arm;
using AzureMcp.LocalServiceClient.Identity;
using AzureMcp.Options;
using AzureMcp.Services.Azure;
using AzureMcp.Services.Azure.Tenant;

namespace AzureMcp.Areas.Monitor.Services;

public class ResourceResolverService(IArmServiceClient armServiceClient, ITenantService tenantService, IIdentityServiceClient credentialService)
    : BaseAzureService(credentialService, tenantService), IResourceResolverService
{
    private readonly IArmServiceClient _armServiceClient = armServiceClient ?? throw new ArgumentNullException(nameof(armServiceClient));

    public async Task<ResourceIdentifier> ResolveResourceIdAsync(
        string subscription,
        string? resourceGroup,
        string? resourceType,
        string resourceName,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription, resourceName);

        if (ResourceIdentifier.TryParse(resourceName, out ResourceIdentifier? result))
        {
            // If already a valid ResourceIdentifier, return it directly
            return result!;
        }

        var resolvedResourceId = await _armServiceClient.ResolveResourceIdAsync(
            subscription,
            resourceGroup,
            resourceType,
            resourceName,
            tenant);

        return new ResourceIdentifier(resolvedResourceId);
    }
}
