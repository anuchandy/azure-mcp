// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Areas.Aks.Models;
using AzureMcp.Options;
using AzureMcp.LocalServiceClient.Arm;
using AzureMcp.LocalServiceClient.Identity;
using AzureMcp.Services.Azure;
using AzureMcp.Services.Azure.Tenant;
using AzureMcp.Services.Caching;

namespace AzureMcp.Areas.Aks.Services;

public sealed class AksService(
    IArmServiceClient armServiceClient,
    IIdentityServiceClient identityServiceClient,
    ITenantService tenantService,
    ICacheService cacheService) : BaseAzureService(identityServiceClient, tenantService), IAksService
{
    private readonly IArmServiceClient _armServiceClient = armServiceClient ?? throw new ArgumentNullException(nameof(armServiceClient));
    private readonly ICacheService _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

    private const string CacheGroup = "aks";
    private const string AksClustersCacheKey = "clusters";
    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromHours(1);

    public async Task<List<Cluster>> ListClusters(
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription);

        // Create cache key
        var cacheKey = string.IsNullOrEmpty(tenant)
            ? $"{AksClustersCacheKey}_{subscription}"
            : $"{AksClustersCacheKey}_{subscription}_{tenant}";

        // Try to get from cache first
        var cachedClusters = await _cacheService.GetAsync<List<Cluster>>(CacheGroup, cacheKey, s_cacheDuration);
        if (cachedClusters != null)
        {
            return cachedClusters;
        }

        try
        {
            var clusters = await _armServiceClient.ListAksClustersAsync(subscription, tenant);
            // Cache the results
            await _cacheService.SetAsync(CacheGroup, cacheKey, clusters, s_cacheDuration);

            return clusters;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving AKS clusters: {ex.Message}", ex);
        }
    }
}
