// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Azure.ResourceManager.CosmosDB;
using AzureMcp.LocalService.Arm.Grpc;
using AzureMcp.LocalService.Arm.Clients;
using Grpc.Core;
using Microsoft.Extensions.Caching.Memory;

namespace AzureMcp.LocalService.Arm.Services;

/// <summary>
/// gRPC service for providing Azure Resource Manager operations.
/// </summary>
public class ArmGrpcService : ArmService.ArmServiceBase
{
    private readonly ILogger<ArmGrpcService> _logger;
    private readonly IdentityClient _identityClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArmGrpcService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="identityClient">The Identity service client.</param>
    /// <param name="serviceProvider">The service provider for creating additional services.</param>
    /// <param name="cache">The memory cache instance.</param>
    public ArmGrpcService(ILogger<ArmGrpcService> logger, IdentityClient identityClient, IServiceProvider serviceProvider, IMemoryCache cache)
    {
        _logger = logger;
        _identityClient = identityClient;
        _serviceProvider = serviceProvider;
        _cache = cache;
    }

    /// <summary>
    /// Gets the status of the Identity service connectivity.
    /// </summary>
    /// <param name="request">The status request containing optional tenant and scopes.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response indicating the Identity service status.</returns>
    public override async Task<IdentityServiceStatusResponse> GetIdentityServiceStatus(
        IdentityServiceStatusRequest request,
        ServerCallContext context)
    {
        try
        {
            var scopes = request.Scopes?.Count > 0
                ? request.Scopes.ToArray()
                : new[] { "https://management.azure.com/.default" };

            var token = await _identityClient.GetTokenAsync(
                scopes,
                string.IsNullOrWhiteSpace(request.TenantId) ? null : request.TenantId,
                context.CancellationToken);

            return new IdentityServiceStatusResponse
            {
                IsSuccess = true,
                Details = $"Successfully obtained token for scopes: {string.Join(", ", scopes)}"
            };
        }
        catch (Exception ex)
        {
            return new IdentityServiceStatusResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Details = $"Failed to obtain token from Identity service: {ex.GetType().Name}"
            };
        }
    }

    /// <summary>
    /// Lists all accessible subscriptions for the specified tenant.
    /// </summary>
    /// <param name="request">The request containing optional tenant ID.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of subscriptions.</returns>
    public override async Task<ListSubscriptionsResponse> ListSubscriptions(
        ListSubscriptionsRequest request,
        ServerCallContext context)
    {
        try
        {
            var tenantId = string.IsNullOrWhiteSpace(request.TenantId) ? null : request.TenantId;
            var subscriptions = await GetSubscriptionsAsync(tenantId);
            var response = new ListSubscriptionsResponse
            {
                IsSuccess = true
            };

            foreach (var subscription in subscriptions)
            {
                response.Subscriptions.Add(new AzureMcp.LocalService.Arm.Grpc.SubscriptionData
                {
                    SubscriptionId = subscription.SubscriptionId,
                    DisplayName = subscription.DisplayName,
                    TenantId = subscription.TenantId?.ToString() ?? string.Empty,
                    State = subscription.State?.ToString() ?? "Unknown"
                });
            }
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list subscriptions for tenant: {TenantId}", 
                string.IsNullOrWhiteSpace(request.TenantId) ? "default" : request.TenantId);
            return new ListSubscriptionsResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Creates an ArmClient with the Identity credential for the specified tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant ID for the ARM client.</param>
    /// <returns>An ArmClient instance configured with Identity authentication.</returns>
    private ArmClient CreateArmClient(string? tenantId = null)
    {
        var credential = new IdentityCredential(
            _identityClient,
            _serviceProvider.GetRequiredService<ILogger<IdentityCredential>>(),
            tenantId);
        _logger.LogDebug("Created ArmClient for tenant: {TenantId}", tenantId ?? "default");
        return new ArmClient(credential);
    }

    /// <summary>
    /// Gets storage accounts for a subscription.
    /// </summary>
    /// <param name="request">The request containing subscription ID and optional tenant.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of storage accounts.</returns>
    public override async Task<GetStorageAccountsResponse> GetStorageAccounts(
        GetStorageAccountsRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.SubscriptionId);

            var cacheKey = string.IsNullOrEmpty(request.TenantId)
                ? $"storage_accounts_{request.SubscriptionId}"
                : $"storage_accounts_{request.SubscriptionId}_{request.TenantId}";

            if (_cache.TryGetValue(cacheKey, out List<string>? cachedAccounts) && cachedAccounts != null)
            {
                var cachedResponse = new GetStorageAccountsResponse { IsSuccess = true };
                cachedResponse.StorageAccounts.AddRange(cachedAccounts);
                return cachedResponse;
            }

            var subscriptions = await GetSubscriptionsAsync(request.TenantId);
            var subscription = subscriptions.FirstOrDefault(s => s.SubscriptionId == request.SubscriptionId);
            if (subscription == null)
            {
                return new GetStorageAccountsResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Subscription '{request.SubscriptionId}' not found"
                };
            }

            var armClient = CreateArmClient(request.TenantId);
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));

            var accounts = new List<string>();
            await foreach (var account in subscriptionResource.GetStorageAccountsAsync())
            {
                if (account?.Data?.Name != null)
                {
                    accounts.Add(account.Data.Name);
                }
            }

            _cache.Set(cacheKey, accounts, TimeSpan.FromHours(1));

            var response = new GetStorageAccountsResponse { IsSuccess = true };
            response.StorageAccounts.AddRange(accounts);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get storage accounts for subscription: {SubscriptionId}", request.SubscriptionId);
            return new GetStorageAccountsResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets storage account keys for a specific storage account.
    /// </summary>
    /// <param name="request">The request containing account name, subscription ID and optional tenant.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the storage account keys.</returns>
    public override async Task<GetStorageAccountKeysResponse> GetStorageAccountKeys(
        GetStorageAccountKeysRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.AccountName, request.SubscriptionId);

            var subscriptions = await GetSubscriptionsAsync(request.TenantId);
            var subscription = subscriptions.FirstOrDefault(s => s.SubscriptionId == request.SubscriptionId);
            if (subscription == null)
            {
                return new GetStorageAccountKeysResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Subscription '{request.SubscriptionId}' not found"
                };
            }

            var armClient = CreateArmClient(request.TenantId);
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));

            StorageAccountResource? storageAccount = null;
            await foreach (var account in subscriptionResource.GetStorageAccountsAsync())
            {
                if (account.Data.Name == request.AccountName)
                {
                    storageAccount = account;
                    break;
                }
            }

            if (storageAccount == null)
            {
                return new GetStorageAccountKeysResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Storage account '{request.AccountName}' not found in subscription '{request.SubscriptionId}'"
                };
            }

            var response = new GetStorageAccountKeysResponse { IsSuccess = true };
            await foreach (var key in storageAccount.GetKeysAsync())
            {
                response.Keys.Add(new Grpc.StorageAccountKey
                {
                    KeyName = key.KeyName ?? string.Empty,
                    KeyValue = key.Value ?? string.Empty,
                    Permissions = key.Permissions?.ToString() ?? string.Empty
                });
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get storage account keys for account: {AccountName}", request.AccountName);
            return new GetStorageAccountKeysResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets connection string for a specific storage account.
    /// </summary>
    /// <param name="request">The request containing account name, subscription ID and optional tenant.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the storage account connection string.</returns>
    public override async Task<GetStorageAccountConnectionStringResponse> GetStorageAccountConnectionString(
        GetStorageAccountConnectionStringRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.AccountName, request.SubscriptionId);

            var subscriptions = await GetSubscriptionsAsync(request.TenantId);
            var subscription = subscriptions.FirstOrDefault(s => s.SubscriptionId == request.SubscriptionId);
            if (subscription == null)
            {
                return new GetStorageAccountConnectionStringResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Subscription '{request.SubscriptionId}' not found"
                };
            }

            var armClient = CreateArmClient(request.TenantId);
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));
            StorageAccountResource? storageAccount = null;
            await foreach (var account in subscriptionResource.GetStorageAccountsAsync())
            {
                if (account.Data.Name == request.AccountName)
                {
                    storageAccount = account;
                    break;
                }
            }

            if (storageAccount == null)
            {
                return new GetStorageAccountConnectionStringResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Storage account '{request.AccountName}' not found in subscription '{request.SubscriptionId}'"
                };
            }

            await foreach (var key in storageAccount.GetKeysAsync())
            {
                var connectionString = $"DefaultEndpointsProtocol=https;AccountName={request.AccountName};AccountKey={key.Value};EndpointSuffix=core.windows.net";
                return new GetStorageAccountConnectionStringResponse
                {
                    IsSuccess = true,
                    ConnectionString = connectionString
                };
            }

            return new GetStorageAccountConnectionStringResponse
            {
                IsSuccess = false,
                ErrorMessage = $"No keys found for storage account '{request.AccountName}'"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connection string for storage account: {AccountName}", request.AccountName);
            return new GetStorageAccountConnectionStringResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets Cosmos DB accounts for a subscription.
    /// </summary>
    /// <param name="request">The request containing subscription ID and optional tenant.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of Cosmos DB accounts.</returns>
    public override async Task<GetCosmosAccountsResponse> GetCosmosAccounts(
        GetCosmosAccountsRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.SubscriptionId);

            var subscriptions = await GetSubscriptionsAsync(request.TenantId);
            var subscription = subscriptions.FirstOrDefault(s => s.SubscriptionId == request.SubscriptionId);
            if (subscription == null)
            {
                return new GetCosmosAccountsResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Subscription '{request.SubscriptionId}' not found"
                };
            }

            var armClient = CreateArmClient(request.TenantId);
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));
            var accounts = new List<string>();
            await foreach (var account in subscriptionResource.GetCosmosDBAccountsAsync())
            {
                if (account?.Data?.Name != null)
                {
                    accounts.Add(account.Data.Name);
                }
            }

            var response = new GetCosmosAccountsResponse { IsSuccess = true };
            response.CosmosAccounts.AddRange(accounts);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Cosmos DB accounts for subscription: {SubscriptionId}", request.SubscriptionId);
            return new GetCosmosAccountsResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets a specific Cosmos DB account details.
    /// </summary>
    /// <param name="request">The request containing account name, subscription ID and optional tenant.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the Cosmos DB account details.</returns>
    public override async Task<GetCosmosAccountResponse> GetCosmosAccount(
        GetCosmosAccountRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.AccountName, request.SubscriptionId);

            var subscriptions = await GetSubscriptionsAsync(request.TenantId);
            var subscription = subscriptions.FirstOrDefault(s => s.SubscriptionId == request.SubscriptionId);
            if (subscription == null)
            {
                return new GetCosmosAccountResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Subscription '{request.SubscriptionId}' not found"
                };
            }

            var armClient = CreateArmClient(request.TenantId);
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));

            CosmosDBAccountResource? cosmosAccount = null;
            await foreach (var account in subscriptionResource.GetCosmosDBAccountsAsync())
            {
                if (account.Data.Name == request.AccountName)
                {
                    cosmosAccount = account;
                    break;
                }
            }

            if (cosmosAccount == null)
            {
                return new GetCosmosAccountResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Cosmos DB account '{request.AccountName}' not found in subscription '{request.SubscriptionId}'"
                };
            }

            var accountData = new CosmosAccountData
            {
                Name = cosmosAccount.Data.Name,
                Id = cosmosAccount.Data.Id.ToString(),
                Location = cosmosAccount.Data.Location.Name ?? string.Empty,
                AccountType = cosmosAccount.Data.Kind?.ToString() ?? "DocumentDB",
                ResourceGroup = cosmosAccount.Data.Id.ResourceGroupName ?? string.Empty,
                ProvisioningState = cosmosAccount.Data.ProvisioningState?.ToString() ?? "Unknown",
                DocumentEndpoint = cosmosAccount.Data.DocumentEndpoint ?? string.Empty
            };

            return new GetCosmosAccountResponse
            {
                IsSuccess = true,
                Account = accountData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Cosmos DB account: {AccountName}", request.AccountName);
            return new GetCosmosAccountResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #region Subscription related internal methods.

    private const string CacheGroup = "subscription";
    private const string CacheKey = "subscriptions";
    private const string SubscriptionCacheKey = "subscription";
    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromHours(12);

    private async Task<List<Azure.ResourceManager.Resources.SubscriptionData>> GetSubscriptionsAsync(string? tenant = null)
    {
        var cacheKey = string.IsNullOrEmpty(tenant) ? CacheKey : $"{CacheKey}_{tenant}";
        var fullCacheKey = $"{CacheGroup}_{cacheKey}";

        if (_cache.TryGetValue(fullCacheKey, out List<Azure.ResourceManager.Resources.SubscriptionData>? cachedResults) && cachedResults != null)
        {
            _logger.LogDebug("Retrieved {Count} subscriptions from cache for tenant: {Tenant}", cachedResults.Count, tenant ?? "default");
            return cachedResults;
        }

        var armClient = CreateArmClient(tenant);
        var subscriptions = armClient.GetSubscriptions();
        var results = new List<Azure.ResourceManager.Resources.SubscriptionData>();

        await foreach (var subscription in subscriptions)
        {
            results.Add(subscription.Data);
        }

        _cache.Set(fullCacheKey, results, s_cacheDuration);
        _logger.LogDebug("Cached {Count} subscriptions for tenant: {Tenant}", results.Count, tenant ?? "default");

        return results;
    }

    private async Task<SubscriptionResource> GetSubscriptionAsync(string subscription, string? tenant = null)
    {
        ValidateRequiredParameters(subscription);

        var subscriptionId = await GetSubscriptionIdAsync(subscription, tenant);
        var cacheKey = string.IsNullOrEmpty(tenant)
            ? $"{SubscriptionCacheKey}_{subscriptionId}"
            : $"{SubscriptionCacheKey}_{subscriptionId}_{tenant}";
        var fullCacheKey = $"{CacheGroup}_{cacheKey}";

        if (_cache.TryGetValue(fullCacheKey, out SubscriptionResource? cachedSubscription) && cachedSubscription != null)
        {
            _logger.LogDebug("Retrieved subscription {SubscriptionId} from cache", subscriptionId);
            return cachedSubscription;
        }

        var armClient = CreateArmClient(tenant);
        var response = await armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscriptionId)).GetAsync();
        if (response?.Value == null)
        {
            throw new Exception($"Could not retrieve subscription {subscription}");
        }

        _cache.Set(fullCacheKey, response.Value, s_cacheDuration);
        _logger.LogDebug("Cached subscription {SubscriptionId}", subscriptionId);
        return response.Value;
    }

    private static bool IsSubscriptionId(string subscription, string? tenant = null)
    {
        return Guid.TryParse(subscription, out _);
    }

    private async Task<string> GetSubscriptionIdByNameAsync(string subscriptionName, string? tenant = null)
    {
        var subscriptions = await GetSubscriptionsAsync(tenant);
        var subscription = subscriptions.FirstOrDefault(s => s.DisplayName.Equals(subscriptionName, StringComparison.OrdinalIgnoreCase)) ??
            throw new Exception($"Could not find subscription with name {subscriptionName}");

        return subscription.SubscriptionId;
    }

    private async Task<string> GetSubscriptionNameByIdAsync(string subscriptionId, string? tenant = null)
    {
        var subscriptions = await GetSubscriptionsAsync(tenant);
        var subscription = subscriptions.FirstOrDefault(s => s.SubscriptionId.Equals(subscriptionId, StringComparison.OrdinalIgnoreCase)) ??
            throw new Exception($"Could not find subscription with ID {subscriptionId}");

        return subscription.DisplayName;
    }

    private async Task<string> GetSubscriptionIdAsync(string subscription, string? tenant)
    {
        if (IsSubscriptionId(subscription))
        {
            return subscription;
        }

        return await GetSubscriptionIdByNameAsync(subscription, tenant);
    }

    private static void ValidateRequiredParameters(params string[] parameters)
    {
        foreach (var parameter in parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter))
            {
                throw new ArgumentException("Required parameter cannot be null or empty", nameof(parameter));
            }
        }
    }

    #endregion
}
