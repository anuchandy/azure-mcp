// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Azure.ResourceManager.CosmosDB;
using Azure.ResourceManager.AppConfiguration;
using Azure.ResourceManager.Kusto;
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

    /// <summary>
    /// Gets App Configuration accounts for a subscription.
    /// </summary>
    /// <param name="request">The request containing subscription ID and optional tenant.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of App Configuration accounts.</returns>
    public override async Task<GetAppConfigAccountsResponse> GetAppConfigAccounts(
        GetAppConfigAccountsRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.SubscriptionId);

            var subscriptions = await GetSubscriptionsAsync(request.TenantId);
            var subscription = subscriptions.FirstOrDefault(s => s.SubscriptionId == request.SubscriptionId);
            if (subscription == null)
            {
                return new GetAppConfigAccountsResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Subscription '{request.SubscriptionId}' not found"
                };
            }

            var armClient = CreateArmClient(request.TenantId);
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));
            var accounts = new List<AppConfigAccountData>();
            
            await foreach (var account in subscriptionResource.GetAppConfigurationStoresAsync())
            {
                if (account?.Data?.Name != null)
                {
                    var accountData = new AppConfigAccountData
                    {
                        Name = account.Data.Name,
                        Location = account.Data.Location.Name ?? string.Empty,
                        Endpoint = account.Data.Endpoint ?? string.Empty,
                        CreationDate = account.Data.CreatedOn?.ToUnixTimeSeconds() ?? 0,
                        PublicNetworkAccess = account.Data.PublicNetworkAccess.HasValue &&
                            account.Data.PublicNetworkAccess.Value.ToString().Equals("Enabled", StringComparison.OrdinalIgnoreCase),
                        Sku = account.Data.SkuName ?? string.Empty,
                        DisableLocalAuth = account.Data.DisableLocalAuth ?? false,
                        SoftDeleteRetentionInDays = account.Data.SoftDeleteRetentionInDays ?? 0,
                        EnablePurgeProtection = account.Data.EnablePurgeProtection ?? false,
                        CreateMode = account.Data.CreateMode?.ToString() ?? string.Empty
                    };
                    
                    if (account.Data.Tags != null)
                    {
                        foreach (var tag in account.Data.Tags)
                        {
                            accountData.Tags.Add(tag.Key, tag.Value);
                        }
                    }

                    if (account.Data.Identity != null)
                    {
                        accountData.ManagedIdentity = new ManagedIdentityInfo();
                        
                        accountData.ManagedIdentity.SystemAssignedIdentity = new SystemAssignedIdentityInfo
                        {
                            Enabled = account.Data.Identity != null,
                            TenantId = account.Data.Identity?.TenantId?.ToString() ?? string.Empty,
                            PrincipalId = account.Data.Identity?.PrincipalId?.ToString() ?? string.Empty
                        };

                        if (account.Data.Identity?.UserAssignedIdentities != null)
                        {
                            foreach (var userIdentity in account.Data.Identity.UserAssignedIdentities)
                            {
                                accountData.ManagedIdentity.UserAssignedIdentities.Add(new UserAssignedIdentityInfo
                                {
                                    ClientId = userIdentity.Value.ClientId?.ToString() ?? string.Empty,
                                    PrincipalId = userIdentity.Value.PrincipalId?.ToString() ?? string.Empty
                                });
                            }
                        }
                    }

                    if (account.Data.EncryptionKeyVaultProperties != null)
                    {
                        accountData.Encryption = new EncryptionProperties
                        {
                            KeyIdentifier = account.Data.EncryptionKeyVaultProperties.KeyIdentifier ?? string.Empty,
                            IdentityClientId = account.Data.EncryptionKeyVaultProperties.IdentityClientId ?? string.Empty,
                            IsKeyVaultKeyIdentifierValid = false,
                            IsIdentityClientIdValid = false
                        };
                    }
                    
                    accounts.Add(accountData);
                }
            }

            var response = new GetAppConfigAccountsResponse { IsSuccess = true };
            response.AppConfigAccounts.AddRange(accounts);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get App Configuration accounts for subscription: {SubscriptionId}", request.SubscriptionId);
            return new GetAppConfigAccountsResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets the endpoint for a specific App Configuration account.
    /// </summary>
    /// <param name="request">The request containing account name, subscription ID and optional tenant.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the App Configuration account endpoint.</returns>
    public override async Task<GetAppConfigAccountEndpointResponse> GetAppConfigAccountEndpoint(
        GetAppConfigAccountEndpointRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.AccountName, request.SubscriptionId);

            var subscriptions = await GetSubscriptionsAsync(request.TenantId);
            var subscription = subscriptions.FirstOrDefault(s => s.SubscriptionId == request.SubscriptionId);
            if (subscription == null)
            {
                return new GetAppConfigAccountEndpointResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Subscription '{request.SubscriptionId}' not found"
                };
            }

            var armClient = CreateArmClient(request.TenantId);
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));

            AppConfigurationStoreResource? appConfigAccount = null;
            await foreach (var account in subscriptionResource.GetAppConfigurationStoresAsync())
            {
                if (account.Data.Name == request.AccountName)
                {
                    appConfigAccount = account;
                    break;
                }
            }

            if (appConfigAccount == null)
            {
                return new GetAppConfigAccountEndpointResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"App Configuration account '{request.AccountName}' not found in subscription '{request.SubscriptionId}'"
                };
            }

            return new GetAppConfigAccountEndpointResponse
            {
                IsSuccess = true,
                Endpoint = appConfigAccount.Data.Endpoint ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get App Configuration account endpoint: {AccountName}", request.AccountName);
            return new GetAppConfigAccountEndpointResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets Kusto clusters for a subscription.
    /// </summary>
    /// <param name="request">The request containing subscription ID and optional tenant.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of Kusto clusters.</returns>
    public override async Task<GetKustoClustersResponse> GetKustoClusters(
        GetKustoClustersRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.SubscriptionId);

            var cacheKey = string.IsNullOrEmpty(request.TenantId)
                ? $"kusto_clusters_{request.SubscriptionId}"
                : $"kusto_clusters_{request.SubscriptionId}_{request.TenantId}";

            if (_cache.TryGetValue(cacheKey, out List<string>? cachedClusters) && cachedClusters != null)
            {
                var cachedResponse = new GetKustoClustersResponse { IsSuccess = true };
                cachedResponse.KustoClusters.AddRange(cachedClusters);
                return cachedResponse;
            }

            var subscriptions = await GetSubscriptionsAsync(request.TenantId);
            var subscription = subscriptions.FirstOrDefault(s => s.SubscriptionId == request.SubscriptionId);
            if (subscription == null)
            {
                return new GetKustoClustersResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Subscription '{request.SubscriptionId}' not found"
                };
            }

            var armClient = CreateArmClient(request.TenantId);
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));

            var clusters = new List<string>();
            await foreach (var cluster in subscriptionResource.GetKustoClustersAsync())
            {
                if (cluster?.Data?.Name != null)
                {
                    clusters.Add(cluster.Data.Name);
                }
            }

            _cache.Set(cacheKey, clusters, TimeSpan.FromHours(1));

            var response = new GetKustoClustersResponse { IsSuccess = true };
            response.KustoClusters.AddRange(clusters);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Kusto clusters for subscription: {SubscriptionId}", request.SubscriptionId);
            return new GetKustoClustersResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets a specific Kusto cluster details.
    /// </summary>
    /// <param name="request">The request containing cluster name, subscription ID and optional tenant.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the Kusto cluster details.</returns>
    public override async Task<GetKustoClusterResponse> GetKustoCluster(
        GetKustoClusterRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.ClusterName, request.SubscriptionId);

            var subscriptions = await GetSubscriptionsAsync(request.TenantId);
            var subscription = subscriptions.FirstOrDefault(s => s.SubscriptionId == request.SubscriptionId);
            if (subscription == null)
            {
                return new GetKustoClusterResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Subscription '{request.SubscriptionId}' not found"
                };
            }

            var armClient = CreateArmClient(request.TenantId);
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));

            KustoClusterResource? kustoCluster = null;
            await foreach (var cluster in subscriptionResource.GetKustoClustersAsync())
            {
                if (cluster.Data.Name == request.ClusterName)
                {
                    kustoCluster = cluster;
                    break;
                }
            }

            if (kustoCluster == null)
            {
                return new GetKustoClusterResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Kusto cluster '{request.ClusterName}' not found in subscription '{request.SubscriptionId}'"
                };
            }

            var clusterData = new Grpc.KustoClusterData
            {
                ClusterName = kustoCluster.Data.Name,
                ClusterUri = kustoCluster.Data.ClusterUri?.ToString() ?? string.Empty,
                Location = kustoCluster.Data.Location.Name ?? string.Empty,
                ResourceGroupName = kustoCluster.Data.Id.ResourceGroupName ?? string.Empty,
                SubscriptionId = kustoCluster.Data.Id.SubscriptionId ?? string.Empty,
                Sku = kustoCluster.Data.Sku?.Capacity.ToString() ?? string.Empty,
                Zones = string.Join(",", kustoCluster.Data.Zones?.ToList() ?? new List<string>()),
                Identity = kustoCluster.Data.Identity?.ManagedServiceIdentityType.ToString() ?? string.Empty,
                Etag = kustoCluster.Data.ETag?.ToString() ?? string.Empty,
                State = kustoCluster.Data.State?.ToString() ?? string.Empty,
                ProvisioningState = kustoCluster.Data.ProvisioningState?.ToString() ?? string.Empty,
                DataIngestionUri = kustoCluster.Data.DataIngestionUri?.ToString() ?? string.Empty,
                StateReason = kustoCluster.Data.StateReason ?? string.Empty,
                IsStreamingIngestEnabled = kustoCluster.Data.IsStreamingIngestEnabled ?? false,
                EngineType = kustoCluster.Data.EngineType?.ToString() ?? string.Empty,
                IsAutoStopEnabled = kustoCluster.Data.IsAutoStopEnabled ?? false
            };

            return new GetKustoClusterResponse
            {
                IsSuccess = true,
                Cluster = clusterData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Kusto cluster: {ClusterName}", request.ClusterName);
            return new GetKustoClusterResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #region Resource Group Operations

    /// <summary>
    /// Gets all resource groups for a subscription.
    /// </summary>
    /// <param name="request">The request containing subscription and optional tenant ID.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of resource groups.</returns>
    public override async Task<GetResourceGroupsResponse> GetResourceGroups(
        GetResourceGroupsRequest request,
        ServerCallContext context)
    {
        try
        {
            var subscriptionResource = await GetSubscriptionAsync(request.SubscriptionId, request.TenantId);
            var subscriptionId = subscriptionResource.Data.SubscriptionId;

            var cacheKey = $"resourcegroups_{subscriptionId}_{request.TenantId ?? "default"}";
            if (_cache.TryGetValue(cacheKey, out List<AzureMcp.LocalService.Arm.Grpc.ResourceGroupData>? cachedResourceGroups))
            {
                return new GetResourceGroupsResponse
                {
                    IsSuccess = true,
                    ResourceGroups = { cachedResourceGroups }
                };
            }

            var resourceGroups = new List<AzureMcp.LocalService.Arm.Grpc.ResourceGroupData>();
            await foreach (var rg in subscriptionResource.GetResourceGroups().GetAllAsync())
            {
                var resourceGroupData = new AzureMcp.LocalService.Arm.Grpc.ResourceGroupData
                {
                    Name = rg.Data.Name,
                    Id = rg.Data.Id.ToString(),
                    Location = rg.Data.Location.ToString()
                };

                resourceGroups.Add(resourceGroupData);
            }

            _cache.Set(cacheKey, resourceGroups, s_cacheDuration);

            return new GetResourceGroupsResponse
            {
                IsSuccess = true,
                ResourceGroups = { resourceGroups }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resource groups for subscription {SubscriptionId}", request.SubscriptionId);
            return new GetResourceGroupsResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets a specific resource group.
    /// </summary>
    /// <param name="request">The request containing resource group name, subscription, and optional tenant ID.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the resource group details.</returns>
    public override async Task<GetResourceGroupResponse> GetResourceGroup(
        GetResourceGroupRequest request,
        ServerCallContext context)
    {
        try
        {
            var subscriptionResource = await GetSubscriptionAsync(request.SubscriptionId, request.TenantId);
            var subscriptionId = subscriptionResource.Data.SubscriptionId;

            var cacheKey = $"resourcegroups_{subscriptionId}_{request.TenantId ?? "default"}";
            if (_cache.TryGetValue(cacheKey, out List<AzureMcp.LocalService.Arm.Grpc.ResourceGroupData>? cachedResourceGroups))
            {
                var cachedRg = cachedResourceGroups!.FirstOrDefault(rg => 
                    rg.Name.Equals(request.ResourceGroupName, StringComparison.OrdinalIgnoreCase));
                if (cachedRg != null)
                {
                    return new GetResourceGroupResponse
                    {
                        IsSuccess = true,
                        ResourceGroup = cachedRg
                    };
                }
            }

            var resourceGroupResponse = await subscriptionResource.GetResourceGroups()
                .GetAsync(request.ResourceGroupName)
                .ConfigureAwait(false);

            if (resourceGroupResponse?.Value == null)
            {
                return new GetResourceGroupResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Resource group {request.ResourceGroupName} not found"
                };
            }

            var rg = resourceGroupResponse.Value;
            var resourceGroupData = new AzureMcp.LocalService.Arm.Grpc.ResourceGroupData
            {
                Name = rg.Data.Name,
                Id = rg.Data.Id.ToString(),
                Location = rg.Data.Location.ToString()
            };

            return new GetResourceGroupResponse
            {
                IsSuccess = true,
                ResourceGroup = resourceGroupData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resource group {ResourceGroupName} for subscription {SubscriptionId}", 
                request.ResourceGroupName, request.SubscriptionId);
            return new GetResourceGroupResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #endregion

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
