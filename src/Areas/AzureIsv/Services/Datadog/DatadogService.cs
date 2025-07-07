// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using AzureMcp.LocalServiceClient.Identity;
using AzureMcp.LocalServiceClient.Arm;
using AzureMcp.Services.Azure;
using AzureMcp.Services.Azure.Tenant;

namespace AzureMcp.Areas.AzureIsv.Services.Datadog;

public partial class DatadogService : BaseAzureService, IDatadogService
{
    private readonly IArmServiceClient _armService;

    public DatadogService(IArmServiceClient armService, IIdentityServiceClient credentialService, ITenantService? tenantService = null) : base(credentialService, tenantService)
    {
        _armService = armService ?? throw new ArgumentNullException(nameof(armService));
    }

    public async Task<List<string>> ListMonitoredResources(string resourceGroup, string subscription, string datadogResource)
    {
        try
        {
            return await _armService.ListMonitoredDatadogResourcesAsync(
                subscription, resourceGroup, datadogResource);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error listing monitored resources: {ex.Message}", ex);
        }
    }
}
