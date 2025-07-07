// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.LocalServiceClient.Arm;
using AzureMcp.LocalServiceClient.Identity;
using AzureMcp.Models.ResourceGroup;
using AzureMcp.Options;
using AzureMcp.Services.Azure.Subscription;
using AzureMcp.Services.Caching;

namespace AzureMcp.Services.Azure.ResourceGroup;

public class ResourceGroupService(IArmServiceClient armService, ICacheService cacheService, ISubscriptionService subscriptionService, IIdentityServiceClient credentialService)
    : BaseAzureService(credentialService), IResourceGroupService
{
    private readonly IArmServiceClient _armService = armService ?? throw new ArgumentNullException(nameof(armService));
    private readonly ICacheService _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    private readonly ISubscriptionService _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
    private const string CacheGroup = "resourcegroup";
    private const string CacheKey = "resourcegroups";
    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromHours(1);

    public async Task<List<ResourceGroupInfo>> GetResourceGroups(string subscription, string? tenant = null, RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription);

        var subscriptionId = await getSubscriptionId(subscription, tenant, retryPolicy);

        // Try to get from cache first
        var cacheKey = $"{CacheKey}_{subscriptionId}_{tenant ?? "default"}";
        var cachedResults = await _cacheService.GetAsync<List<ResourceGroupInfo>>(CacheGroup, cacheKey, s_cacheDuration);
        if (cachedResults != null)
        {
            return cachedResults;
        }

        // If not in cache, fetch from Azure
        try
        {
            var resourceGroups = await _armService.GetResourceGroupsAsync(subscriptionId, tenant);
            // Cache the results
            await _cacheService.SetAsync(CacheGroup, cacheKey, resourceGroups, s_cacheDuration);

            return resourceGroups;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving resource groups: {ex.Message}", ex);
        }
    }

    public async Task<ResourceGroupInfo?> GetResourceGroup(string subscription, string resourceGroupName, string? tenant = null, RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription, resourceGroupName);

        var subscriptionId = await getSubscriptionId(subscription, tenant, retryPolicy);

        // Try to get from cache first
        var cacheKey = $"{CacheKey}_{subscriptionId}_{tenant ?? "default"}";
        var cachedResults = await _cacheService.GetAsync<List<ResourceGroupInfo>>(CacheGroup, cacheKey, s_cacheDuration);
        if (cachedResults != null)
        {
            return cachedResults.FirstOrDefault(rg => rg.Name.Equals(resourceGroupName, StringComparison.OrdinalIgnoreCase));
        }

        try
        {
            var rg = await _armService.GetResourceGroupAsync(resourceGroupName, subscription, tenant);
            return rg;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving resource group {resourceGroupName}: {ex.Message}", ex);
        }
    }

    private async Task<string> getSubscriptionId(string subscription, string? tenant, RetryPolicyOptions? retryPolicy)
    {
        if (_subscriptionService.IsSubscriptionId(subscription, tenant))
        {
            return subscription;
        }
        return await _subscriptionService.GetSubscriptionIdByName(subscription, tenant, retryPolicy);
    }
}
