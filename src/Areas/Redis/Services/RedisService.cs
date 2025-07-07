// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Areas.Redis.Models.CacheForRedis;
using AzureMcp.Areas.Redis.Models.ManagedRedis;
using AzureMcp.LocalServiceClient.Identity;
using AzureMcp.LocalServiceClient.Arm;
using AzureMcp.Models.Identity;
using AzureMcp.Options;
using AzureMcp.Services.Azure;
using AzureMcp.Services.Azure.Tenant;

namespace AzureMcp.Areas.Redis.Services;

public class RedisService(IArmServiceClient armService, ITenantService tenantService, IIdentityServiceClient credentialService)
    : BaseAzureService(credentialService, tenantService), IRedisService
{
    private readonly IArmServiceClient _armService = armService ?? throw new ArgumentNullException(nameof(armService));

    public async Task<IEnumerable<Cache>> ListCachesAsync(
        string subscriptionId,
        string? tenant = null,
        AuthMethod? authMethod = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscriptionId);

        try
        {
            var caches = await _armService.ListRedisCachesAsync(subscriptionId, tenant);
            return caches;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving Redis caches: {ex.Message}", ex);
        }
    }

    public async Task<IEnumerable<AccessPolicyAssignment>> ListAccessPolicyAssignmentsAsync(
        string cacheName,
        string resourceGroupName,
        string subscriptionId,
        string? tenant = null,
        AuthMethod? authMethod = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(cacheName, resourceGroupName, subscriptionId);

        try
        {
            var accessPolicyAssignments = await _armService.ListRedisAccessPolicyAssignmentsAsync(
                cacheName, resourceGroupName, subscriptionId, tenant);
            return accessPolicyAssignments;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving Redis cache access policy assignments: {ex.Message}", ex);
        }
    }

    public async Task<IEnumerable<Cluster>> ListClustersAsync(
        string subscriptionId,
        string? tenant = null,
        AuthMethod? authMethod = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscriptionId);

        try
        {
            var clusters = await _armService.ListRedisClustersAsync(subscriptionId, tenant);
            return clusters;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving Redis clusters: {ex.Message}", ex);
        }
    }

    public async Task<IEnumerable<Database>> ListDatabasesAsync(
        string clusterName,
        string resourceGroupName,
        string subscriptionId,
        string? tenant = null,
        AuthMethod? authMethod = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(clusterName, resourceGroupName, subscriptionId);

        try
        {
            var databases = await _armService.ListRedisDatabasesAsync(
                clusterName, resourceGroupName, subscriptionId, tenant);
            return databases;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving Redis cluster databases: {ex.Message}", ex);
        }
    }
}
