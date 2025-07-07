// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.LocalServiceClient.Arm;
using AzureMcp.LocalServiceClient.Identity;
using AzureMcp.Services.Caching;

namespace AzureMcp.Services.Azure.Tenant;

public class TenantService(IArmServiceClient armServiceClient, ICacheService cacheService, IIdentityServiceClient credentialService)
    : BaseAzureService(credentialService), ITenantService
{
    private readonly IArmServiceClient _armServiceClient = armServiceClient ?? throw new ArgumentNullException(nameof(armServiceClient));
    private readonly ICacheService _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    private const string CacheGroup = "tenant";
    private const string CacheKey = "tenants";
    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromHours(12);

    public async Task<List<TenantData>> GetTenants()
    {
        // Try to get from cache first
        var cachedResults = await _cacheService.GetAsync<List<TenantData>>(CacheGroup, CacheKey, s_cacheDuration);
        if (cachedResults != null)
        {
            return cachedResults;
        }

        // If not in cache, fetch from Azure via ARM service client
        var results = await _armServiceClient.GetTenantsAsync();

        // Cache the results
        await _cacheService.SetAsync(CacheGroup, CacheKey, results, s_cacheDuration);
        return results;
    }

    public bool IsTenantId(string tenant)
    {
        return Guid.TryParse(tenant, out _);
    }

    public async Task<string?> GetTenantId(string tenant)
    {
        if (IsTenantId(tenant))
        {
            return tenant;
        }

        return await GetTenantIdByName(tenant);
    }

    public async Task<string?> GetTenantIdByName(string tenantName)
    {
        var tenants = await GetTenants();
        var tenant = tenants.FirstOrDefault(t => t.DisplayName?.Equals(tenantName, StringComparison.OrdinalIgnoreCase) == true) ??
            throw new Exception($"Could not find tenant with name {tenantName}");

        if (tenant.TenantId == null)
            throw new InvalidOperationException($"Tenant {tenantName} has a null TenantId");

        return tenant.TenantId;
    }

    public async Task<string?> GetTenantNameById(string tenantId)
    {
        var tenants = await GetTenants();
        var tenant = tenants.FirstOrDefault(t => t.TenantId?.Equals(tenantId, StringComparison.OrdinalIgnoreCase) == true) ??
            throw new Exception($"Could not find tenant with ID {tenantId}");

        if (tenant.DisplayName == null)
            throw new InvalidOperationException($"Tenant with ID {tenantId} has a null DisplayName");

        return tenant.DisplayName;
    }
}
