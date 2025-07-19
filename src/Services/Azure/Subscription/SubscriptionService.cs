// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.LocalServiceClient.Arm;
using AzureMcp.LocalServiceClient.Identity;
using AzureMcp.Options;
using AzureMcp.Services.Azure.Tenant;
using AzureMcp.Services.Caching;

namespace AzureMcp.Services.Azure.Subscription;

public class SubscriptionService(IArmServiceClient armServiceClient, ICacheService cacheService, ITenantService tenantService, IIdentityServiceClient credentialService)
    : BaseAzureService(credentialService, tenantService), ISubscriptionService
{
    private readonly IArmServiceClient _armServiceClient = armServiceClient ?? throw new ArgumentNullException(nameof(armServiceClient));
    private readonly ICacheService _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    private const string CacheGroup = "subscription";
    private const string CacheKey = "subscriptions";
    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromHours(12);

    public async Task<List<AzureMcp.LocalServiceClient.Arm.SubscriptionData>> GetSubscriptions(string? tenant = null, RetryPolicyOptions? retryPolicy = null)
    {
        // Try to get from cache first
        var cacheKey = string.IsNullOrEmpty(tenant) ? CacheKey : $"{CacheKey}_{tenant}";
        var cachedResults = await _cacheService.GetAsync<List<AzureMcp.LocalServiceClient.Arm.SubscriptionData>>(CacheGroup, cacheKey, s_cacheDuration);
        if (cachedResults != null)
        {
            return cachedResults;
        }

        // If not in cache, fetch from Azure
        var results = await _armServiceClient.ListSubscriptionsAsync(tenant);
        // Cache the results
        await _cacheService.SetAsync(CacheGroup, cacheKey, results, s_cacheDuration);

        return results;
    }

    public async Task<AzureMcp.LocalServiceClient.Arm.SubscriptionData> GetSubscription(string subscription, string? tenant = null, RetryPolicyOptions? retryPolicy = null)
    {
        // Try to get from cache first using a subscription-specific cache key
        var cacheKey = string.IsNullOrEmpty(tenant) ? $"subscription_{subscription}" : $"subscription_{subscription}_{tenant}";
        var cachedResult = await _cacheService.GetAsync<AzureMcp.LocalServiceClient.Arm.SubscriptionData>(CacheGroup, cacheKey, s_cacheDuration);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        // If not in cache, fetch from Azure using the new gRPC method
        var result = await _armServiceClient.GetSubscriptionAsync(subscription, tenant);
        
        // Cache the result
        await _cacheService.SetAsync(CacheGroup, cacheKey, result, s_cacheDuration);

        return result;
    }

    public bool IsSubscriptionId(string subscription, string? tenant = null)
    {
        return Guid.TryParse(subscription, out _);
    }

    public async Task<string> GetSubscriptionIdByName(string subscriptionName, string? tenant = null, RetryPolicyOptions? retryPolicy = null)
    {
        var subscriptions = await GetSubscriptions(tenant, retryPolicy);
        var subscription = subscriptions.FirstOrDefault(s => s.DisplayName.Equals(subscriptionName, StringComparison.OrdinalIgnoreCase)) ??
            throw new Exception($"Could not find subscription with name {subscriptionName}");

        return subscription.SubscriptionId;
    }

    public async Task<string> GetSubscriptionNameById(string subscriptionId, string? tenant = null, RetryPolicyOptions? retryPolicy = null)
    {
        var subscriptions = await GetSubscriptions(tenant, retryPolicy);
        var subscription = subscriptions.FirstOrDefault(s => s.SubscriptionId.Equals(subscriptionId, StringComparison.OrdinalIgnoreCase)) ??
            throw new Exception($"Could not find subscription with ID {subscriptionId}");

        return subscription.DisplayName;
    }

    private async Task<string> GetSubscriptionId(string subscription, string? tenant, RetryPolicyOptions? retryPolicy)
    {
        if (IsSubscriptionId(subscription))
        {
            return subscription;
        }

        return await GetSubscriptionIdByName(subscription, tenant, retryPolicy);
    }
}
