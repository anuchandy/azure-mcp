// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ResourceManager;
using Azure.ResourceManager.Authorization;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Azure.ResourceManager.CosmosDB;
using Azure.ResourceManager.AppConfiguration;
using Azure.ResourceManager.Kusto;
using Azure.ResourceManager.Redis;
using Azure.ResourceManager.RedisEnterprise;
using Azure.ResourceManager.PostgreSql.FlexibleServers;
using Azure.ResourceManager.Search;
using Azure.ResourceManager.Datadog;
using Azure.ResourceManager.OperationalInsights;
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

    #region Redis Methods

    /// <summary>
    /// Lists Redis caches for a subscription.
    /// </summary>
    /// <param name="request">The request containing subscription information.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of Redis caches.</returns>
    public override async Task<ListRedisCachesResponse> ListRedisCaches(
        ListRedisCachesRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.SubscriptionId);

            var armClient = CreateArmClient(request.TenantId);
            var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(request.SubscriptionId));

            var caches = new List<RedisCache>();
            await foreach (var cacheResource in subscription.GetAllRedisAsync())
            {
                if (string.IsNullOrWhiteSpace(cacheResource?.Id.ToString()) || string.IsNullOrWhiteSpace(cacheResource.Data.Name))
                {
                    continue;
                }

                var cache = cacheResource.Data;
                var redisCache = new RedisCache
                {
                    Name = cache.Name ?? string.Empty,
                    ResourceGroupName = cacheResource.Id.ResourceGroupName ?? string.Empty,
                    SubscriptionId = cacheResource.Id.SubscriptionId ?? string.Empty,
                    Location = cache.Location.ToString(),
                    Sku = $"{cache.Sku.Name} {cache.Sku.Family}{cache.Sku.Capacity}",
                    ProvisioningState = cache.ProvisioningState?.ToString() ?? string.Empty,
                    RedisVersion = cache.RedisVersion ?? string.Empty,
                    HostName = cache.HostName ?? string.Empty,
                    SslPort = cache.SslPort ?? 0,
                    Port = cache.Port ?? 0,
                    ShardCount = cache.ShardCount ?? 0,
                    SubnetId = cache.SubnetId?.ToString() ?? string.Empty,
                    PublicNetworkAccess = cache.PublicNetworkAccess != Azure.ResourceManager.Redis.Models.RedisPublicNetworkAccess.Disabled,
                    EnableNonSslPort = cache.EnableNonSslPort ?? false,
                    IsAccessKeyAuthenticationDisabled = cache.IsAccessKeyAuthenticationDisabled ?? false,
                    MinimumTlsVersion = cache.MinimumTlsVersion?.ToString() ?? string.Empty,
                    ReplicasPerPrimary = cache.ReplicasPerPrimary ?? 0,
                    UpdateChannel = cache.UpdateChannel?.ToString() ?? string.Empty,
                    ZonalAllocationPolicy = cache.ZonalAllocationPolicy?.ToString() ?? string.Empty
                };

                if (cache.LinkedServers != null)
                {
                    redisCache.LinkedServers.AddRange(cache.LinkedServers.Select(ls => ls.Id?.ToString() ?? string.Empty));
                }

                if (cache.PrivateEndpointConnections != null)
                {
                    redisCache.PrivateEndpointConnections.AddRange(cache.PrivateEndpointConnections.Select(pec => pec.Id?.ToString() ?? string.Empty));
                }

                if (cache.Zones != null)
                {
                    redisCache.Zones.AddRange(cache.Zones);
                }

                if (cache.RedisConfiguration != null)
                {
                    redisCache.Configuration = new RedisCacheConfiguration
                    {
                        IsRdbBackupEnabled = cache.RedisConfiguration.IsRdbBackupEnabled ?? false,
                        RdbBackupFrequency = cache.RedisConfiguration.RdbBackupFrequency ?? string.Empty,
                        RdbBackupMaxSnapshotCount = cache.RedisConfiguration.RdbBackupMaxSnapshotCount ?? 0,
                        IsAofBackupEnabled = cache.RedisConfiguration.IsAofBackupEnabled ?? false,
                        MaxFragmentationMemoryReserved = cache.RedisConfiguration.MaxFragmentationMemoryReserved ?? string.Empty,
                        MaxMemoryPolicy = cache.RedisConfiguration.MaxMemoryPolicy ?? string.Empty,
                        MaxMemoryReserved = cache.RedisConfiguration.MaxMemoryReserved ?? string.Empty,
                        MaxMemoryDelta = cache.RedisConfiguration.MaxMemoryDelta ?? string.Empty,
                        MaxClients = int.TryParse(cache.RedisConfiguration.MaxClients?.ToString(), out var maxClients) ? maxClients : 0,
                        NotifyKeyspaceEvents = cache.RedisConfiguration.NotifyKeyspaceEvents ?? string.Empty,
                        PreferredDataArchiveAuthMethod = cache.RedisConfiguration.PreferredDataArchiveAuthMethod ?? string.Empty,
                        PreferredDataPersistenceAuthMethod = cache.RedisConfiguration.PreferredDataPersistenceAuthMethod ?? string.Empty,
                        ZonalConfiguration = cache.RedisConfiguration.ZonalConfiguration ?? string.Empty,
                        AuthNotRequired = cache.RedisConfiguration.AuthNotRequired ?? string.Empty,
                        StorageSubscriptionId = cache.RedisConfiguration.StorageSubscriptionId ?? string.Empty,
                        IsEntraIdAuthEnabled = string.IsNullOrWhiteSpace(cache.RedisConfiguration.IsAadEnabled) ? false : StringComparer.OrdinalIgnoreCase.Equals(cache.RedisConfiguration.IsAadEnabled, "True")
                    };
                }

                redisCache.Identity = cache.Identity is null ? null : new ManagedIdentityInfo
                {
                    SystemAssignedIdentity = new SystemAssignedIdentityInfo
                    {
                        Enabled = cache.Identity != null,
                        TenantId = cache.Identity?.TenantId?.ToString() ?? string.Empty,
                        PrincipalId = cache.Identity?.PrincipalId?.ToString() ?? string.Empty
                    },
                    UserAssignedIdentities = { cache.Identity?.UserAssignedIdentities?
                        .Select(identity => new UserAssignedIdentityInfo
                        {
                            ClientId = identity.Value.ClientId?.ToString() ?? string.Empty,
                            PrincipalId = identity.Value.PrincipalId?.ToString() ?? string.Empty
                        }) ?? Enumerable.Empty<UserAssignedIdentityInfo>() }
                };

                if (cache.Tags != null)
                {
                    foreach (var tag in cache.Tags)
                    {
                        redisCache.Tags[tag.Key] = tag.Value;
                    }
                }

                caches.Add(redisCache);
            }

            return new ListRedisCachesResponse
            {
                IsSuccess = true,
                RedisCaches = { caches }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Redis caches for subscription {SubscriptionId}", request.SubscriptionId);
            return new ListRedisCachesResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Lists Redis access policy assignments for a cache.
    /// </summary>
    /// <param name="request">The request containing cache information.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of access policy assignments.</returns>
    public override async Task<ListRedisAccessPolicyAssignmentsResponse> ListRedisAccessPolicyAssignments(
        ListRedisAccessPolicyAssignmentsRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.CacheName, request.ResourceGroupName, request.SubscriptionId);

            var armClient = CreateArmClient(request.TenantId);
            var resourceGroupResponse = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(request.SubscriptionId))
                .GetResourceGroups()
                .GetAsync(request.ResourceGroupName);
            var cacheResponse = (await resourceGroupResponse).Value.GetRedisAsync(request.CacheName);
            var cache = (await cacheResponse).Value;

            var assignments = new List<RedisAccessPolicyAssignment>();
            await foreach (var assignmentResource in cache.GetRedisCacheAccessPolicyAssignments())
            {
                if (string.IsNullOrWhiteSpace(assignmentResource?.Data?.Name))
                {
                    continue;
                }

                var assignment = assignmentResource.Data;
                assignments.Add(new RedisAccessPolicyAssignment
                {
                    AccessPolicyName = assignment.AccessPolicyName ?? string.Empty,
                    IdentityName = assignment.ObjectIdAlias ?? string.Empty,
                    ProvisioningState = assignment.ProvisioningState?.ToString() ?? string.Empty
                });
            }

            return new ListRedisAccessPolicyAssignmentsResponse
            {
                IsSuccess = true,
                RedisAccessPolicyAssignments = { assignments }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Redis access policy assignments for cache {CacheName}", request.CacheName);
            return new ListRedisAccessPolicyAssignmentsResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Lists Redis clusters for a subscription.
    /// </summary>
    /// <param name="request">The request containing subscription information.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of Redis clusters.</returns>
    public override async Task<ListRedisClustersResponse> ListRedisClusters(
        ListRedisClustersRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.SubscriptionId);

            var armClient = CreateArmClient(request.TenantId);
            var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(request.SubscriptionId));

            var clusters = new List<RedisCluster>();
            foreach (var clusterResource in subscription.GetRedisEnterpriseClusters())
            {
                if (string.IsNullOrWhiteSpace(clusterResource?.Id.ToString()) || string.IsNullOrWhiteSpace(clusterResource.Data.Name))
                {
                    continue;
                }

                var cluster = clusterResource.Data;
                var redisCluster = new RedisCluster
                {
                    Name = cluster.Name ?? string.Empty,
                    SubscriptionId = clusterResource.Id.SubscriptionId ?? string.Empty,
                    ResourceGroupName = clusterResource.Id.ResourceGroupName ?? string.Empty,
                    Location = cluster.Location.ToString(),
                    Sku = $"{cluster.Sku.Name} {cluster.Sku.Capacity}",
                    ProvisioningState = cluster.ProvisioningState?.ToString() ?? string.Empty,
                    ResourceState = cluster.ResourceState?.ToString() ?? string.Empty,
                    RedisVersion = cluster.RedisVersion ?? string.Empty,
                    HostName = cluster.HostName ?? string.Empty,
                    MinimumTlsVersion = cluster.MinimumTlsVersion?.ToString() ?? string.Empty
                };

                if (cluster.PrivateEndpointConnections != null)
                {
                    redisCluster.PrivateEndpointConnections.AddRange(cluster.PrivateEndpointConnections.Select(pec => pec.Id?.ToString() ?? string.Empty));
                }

                if (cluster.Zones != null)
                {
                    redisCluster.Zones.AddRange(cluster.Zones);
                }

                redisCluster.Identity = cluster.Identity is null ? null : new ManagedIdentityInfo
                {
                    SystemAssignedIdentity = new SystemAssignedIdentityInfo
                    {
                        Enabled = cluster.Identity != null,
                        TenantId = cluster.Identity?.TenantId?.ToString() ?? string.Empty,
                        PrincipalId = cluster.Identity?.PrincipalId?.ToString() ?? string.Empty
                    },
                    UserAssignedIdentities = { cluster.Identity?.UserAssignedIdentities?
                        .Select(identity => new UserAssignedIdentityInfo
                        {
                            ClientId = identity.Value.ClientId?.ToString() ?? string.Empty,
                            PrincipalId = identity.Value.PrincipalId?.ToString() ?? string.Empty
                        }) ?? Enumerable.Empty<UserAssignedIdentityInfo>() }
                };

                if (cluster.Tags != null)
                {
                    foreach (var tag in cluster.Tags)
                    {
                        redisCluster.Tags[tag.Key] = tag.Value;
                    }
                }

                clusters.Add(redisCluster);
            }

            return await Task.FromResult(new ListRedisClustersResponse
            {
                IsSuccess = true,
                RedisClusters = { clusters }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Redis clusters for subscription {SubscriptionId}", request.SubscriptionId);
            return new ListRedisClustersResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Lists Redis databases for a cluster.
    /// </summary>
    /// <param name="request">The request containing cluster information.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of Redis databases.</returns>
    public override async Task<ListRedisDatabasesResponse> ListRedisDatabases(
        ListRedisDatabasesRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.ClusterName, request.ResourceGroupName, request.SubscriptionId);

            var armClient = CreateArmClient(request.TenantId);
            var resourceGroupResponse = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(request.SubscriptionId))
                .GetResourceGroups()
                .GetAsync(request.ResourceGroupName);
            var clusterResponse = (await resourceGroupResponse).Value.GetRedisEnterpriseClusterAsync(request.ClusterName);
            var cluster = (await clusterResponse).Value;

            var databases = new List<RedisDatabase>();
            await foreach (var databaseResource in cluster.GetRedisEnterpriseDatabases())
            {
                if (string.IsNullOrWhiteSpace(databaseResource?.Data?.Name))
                {
                    continue;
                }

                var database = databaseResource.Data;
                var redisDatabase = new RedisDatabase
                {
                    Name = database.Name ?? string.Empty,
                    ClusterName = request.ClusterName,
                    ResourceGroupName = request.ResourceGroupName,
                    SubscriptionId = request.SubscriptionId,
                    ClientProtocol = database.ClientProtocol?.ToString() ?? string.Empty,
                    Port = database.Port ?? 0,
                    ProvisioningState = database.ProvisioningState?.ToString() ?? string.Empty,
                    ResourceState = database.ResourceState?.ToString() ?? string.Empty,
                    ClusteringPolicy = database.ClusteringPolicy?.ToString() ?? string.Empty,
                    EvictionPolicy = database.EvictionPolicy?.ToString() ?? string.Empty,
                    IsAofEnabled = database.Persistence?.IsAofEnabled ?? false,
                    IsRdbEnabled = database.Persistence?.IsRdbEnabled ?? false,
                    AofFrequency = database.Persistence?.AofFrequency?.ToString() ?? string.Empty,
                    RdbFrequency = database.Persistence?.RdbFrequency?.ToString() ?? string.Empty,
                    GeoReplicationGroupNickname = database.GeoReplication?.GroupNickname ?? string.Empty
                };

                if (database.Modules != null)
                {
                    foreach (var module in database.Modules)
                    {
                        redisDatabase.Modules.Add(new RedisModule
                        {
                            Name = module.Name ?? string.Empty,
                            Args = module.Args ?? string.Empty,
                            Version = module.Version ?? string.Empty
                        });
                    }
                }

                if (database.GeoReplication?.LinkedDatabases != null)
                {
                    redisDatabase.GeoReplicationLinkedDatabases.AddRange(database.GeoReplication.LinkedDatabases.Select(ld => ld.Id?.ToString() ?? string.Empty));
                }

                databases.Add(redisDatabase);
            }

            return new ListRedisDatabasesResponse
            {
                IsSuccess = true,
                RedisDatabases = { databases }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Redis databases for cluster {ClusterName}", request.ClusterName);
            return new ListRedisDatabasesResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #endregion

    #region PostgreSQL Methods

    /// <summary>
    /// Lists PostgreSQL flexible servers in a resource group.
    /// </summary>
    /// <param name="request">The request containing resource group information.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of PostgreSQL servers.</returns>
    public override async Task<ListPostgreSqlServersResponse> ListPostgreSqlServers(
        ListPostgreSqlServersRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.SubscriptionId, request.ResourceGroupName);

            var armClient = CreateArmClient(request.TenantId);
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(request.SubscriptionId));
            var resourceGroup = await subscriptionResource.GetResourceGroups().GetAsync(request.ResourceGroupName);

            if (resourceGroup?.Value == null)
            {
                return new ListPostgreSqlServersResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Resource group '{request.ResourceGroupName}' not found"
                };
            }

            var serverList = new List<string>();
            await foreach (var server in resourceGroup.Value.GetPostgreSqlFlexibleServers())
            {
                serverList.Add(server.Data.Name);
            }

            return new ListPostgreSqlServersResponse
            {
                IsSuccess = true,
                ServerNames = { serverList }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing PostgreSQL servers in resource group {ResourceGroupName}", request.ResourceGroupName);
            return new ListPostgreSqlServersResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets PostgreSQL server configuration details.
    /// </summary>
    /// <param name="request">The request containing server information.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the server configuration.</returns>
    public override async Task<GetPostgreSqlServerConfigResponse> GetPostgreSqlServerConfig(
        GetPostgreSqlServerConfigRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.SubscriptionId, request.ResourceGroupName, request.ServerName);

            var armClient = CreateArmClient(request.TenantId);
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(request.SubscriptionId));
            var resourceGroup = await subscriptionResource.GetResourceGroups().GetAsync(request.ResourceGroupName);

            if (resourceGroup?.Value == null)
            {
                return new GetPostgreSqlServerConfigResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Resource group '{request.ResourceGroupName}' not found"
                };
            }

            var serverResponse = await resourceGroup.Value.GetPostgreSqlFlexibleServerAsync(request.ServerName);
            if (serverResponse?.Value == null)
            {
                return new GetPostgreSqlServerConfigResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"PostgreSQL server '{request.ServerName}' not found"
                };
            }

            var serverData = serverResponse.Value.Data;
            var serverConfig = new PostgreSqlServerConfig
            {
                Name = serverData.Name ?? string.Empty,
                Location = serverData.Location.ToString(),
                Version = serverData.Version?.ToString() ?? string.Empty,
                SkuName = serverData.Sku?.Name ?? string.Empty,
                StorageSizeGb = serverData.Storage?.StorageSizeInGB ?? 0,
                BackupRetentionDays = serverData.Backup?.BackupRetentionDays ?? 0,
                GeoRedundantBackup = serverData.Backup?.GeoRedundantBackup?.ToString() ?? string.Empty
            };

            return new GetPostgreSqlServerConfigResponse
            {
                IsSuccess = true,
                ServerConfig = serverConfig
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting PostgreSQL server config for server {ServerName}", request.ServerName);
            return new GetPostgreSqlServerConfigResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets a specific PostgreSQL server configuration parameter.
    /// </summary>
    /// <param name="request">The request containing server and parameter information.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the parameter value.</returns>
    public override async Task<GetPostgreSqlServerParameterResponse> GetPostgreSqlServerParameter(
        GetPostgreSqlServerParameterRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.SubscriptionId, request.ResourceGroupName, request.ServerName, request.ParameterName);

            var armClient = CreateArmClient(request.TenantId);
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(request.SubscriptionId));
            var resourceGroup = await subscriptionResource.GetResourceGroups().GetAsync(request.ResourceGroupName);

            if (resourceGroup?.Value == null)
            {
                return new GetPostgreSqlServerParameterResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Resource group '{request.ResourceGroupName}' not found"
                };
            }

            var serverResponse = await resourceGroup.Value.GetPostgreSqlFlexibleServerAsync(request.ServerName);
            if (serverResponse?.Value == null)
            {
                return new GetPostgreSqlServerParameterResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"PostgreSQL server '{request.ServerName}' not found"
                };
            }

            var configResponse = await serverResponse.Value.GetPostgreSqlFlexibleServerConfigurationAsync(request.ParameterName);
            if (configResponse?.Value?.Data == null)
            {
                return new GetPostgreSqlServerParameterResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Parameter '{request.ParameterName}' not found"
                };
            }

            return new GetPostgreSqlServerParameterResponse
            {
                IsSuccess = true,
                ParameterValue = configResponse.Value.Data.Value ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting PostgreSQL server parameter {ParameterName} for server {ServerName}", 
                request.ParameterName, request.ServerName);
            return new GetPostgreSqlServerParameterResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Sets a specific PostgreSQL server configuration parameter.
    /// </summary>
    /// <param name="request">The request containing server, parameter, and value information.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the result of the operation.</returns>
    public override async Task<SetPostgreSqlServerParameterResponse> SetPostgreSqlServerParameter(
        SetPostgreSqlServerParameterRequest request,
        ServerCallContext context)
    {
        try
        {
            ValidateRequiredParameters(request.SubscriptionId, request.ResourceGroupName, request.ServerName, 
                request.ParameterName, request.ParameterValue);

            var armClient = CreateArmClient(request.TenantId);
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(request.SubscriptionId));
            var resourceGroup = await subscriptionResource.GetResourceGroups().GetAsync(request.ResourceGroupName);

            if (resourceGroup?.Value == null)
            {
                return new SetPostgreSqlServerParameterResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Resource group '{request.ResourceGroupName}' not found"
                };
            }

            var serverResponse = await resourceGroup.Value.GetPostgreSqlFlexibleServerAsync(request.ServerName);
            if (serverResponse?.Value == null)
            {
                return new SetPostgreSqlServerParameterResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"PostgreSQL server '{request.ServerName}' not found"
                };
            }

            var configResponse = await serverResponse.Value.GetPostgreSqlFlexibleServerConfigurationAsync(request.ParameterName);
            if (configResponse?.Value?.Data == null)
            {
                return new SetPostgreSqlServerParameterResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Parameter '{request.ParameterName}' not found"
                };
            }

            var configData = new Azure.ResourceManager.PostgreSql.FlexibleServers.PostgreSqlFlexibleServerConfigurationData
            {
                Value = request.ParameterValue,
                Source = "user-override"
            };

            var updateOperation = await configResponse.Value.UpdateAsync(Azure.WaitUntil.Completed, configData);
            if (updateOperation.HasCompleted && updateOperation.HasValue)
            {
                return new SetPostgreSqlServerParameterResponse
                {
                    IsSuccess = true,
                    Message = $"Parameter '{request.ParameterName}' updated successfully to '{request.ParameterValue}'"
                };
            }
            else
            {
                return new SetPostgreSqlServerParameterResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to update parameter '{request.ParameterName}' to value '{request.ParameterValue}'"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting PostgreSQL server parameter {ParameterName} to {ParameterValue} for server {ServerName}", 
                request.ParameterName, request.ParameterValue, request.ServerName);
            return new SetPostgreSqlServerParameterResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #endregion

    #region Search Services

    /// <summary>
    /// Lists Azure Search services in a subscription.
    /// </summary>
    /// <param name="request">The request containing subscription ID and optional tenant ID.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of Search service names.</returns>
    public override async Task<ListSearchServicesResponse> ListSearchServices(
        ListSearchServicesRequest request,
        ServerCallContext context)
    {
        try
        {
            var subscription = await GetSubscriptionAsync(request.SubscriptionId, request.TenantId);

            var serviceNames = new List<string>();
            await foreach (var service in subscription.GetSearchServicesAsync())
            {
                if (!string.IsNullOrWhiteSpace(service?.Data?.Name))
                {
                    serviceNames.Add(service.Data.Name);
                }
            }

            return new ListSearchServicesResponse
            {
                IsSuccess = true,
                ServiceNames = { serviceNames }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Search services for subscription {SubscriptionId}", request.SubscriptionId);
            return new ListSearchServicesResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #endregion

    #region Datadog Methods

    /// <summary>
    /// Lists monitored resources for a Datadog monitor.
    /// </summary>
    /// <param name="request">The request containing subscription ID, resource group, and Datadog resource name.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of monitored resource names.</returns>
    public override async Task<ListMonitoredDatadogResourcesResponse> ListMonitoredDatadogResources(
        ListMonitoredDatadogResourcesRequest request,
        ServerCallContext context)
    {
        try
        {
            await Task.Yield(); // Hack (todo: anu) Ensure this method runs asynchronously

            if (string.IsNullOrWhiteSpace(request.SubscriptionId))
            {
                return new ListMonitoredDatadogResourcesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Subscription ID cannot be null or empty"
                };
            }

            if (string.IsNullOrWhiteSpace(request.ResourceGroupName))
            {
                return new ListMonitoredDatadogResourcesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Resource group name cannot be null or empty"
                };
            }

            if (string.IsNullOrWhiteSpace(request.DatadogResourceName))
            {
                return new ListMonitoredDatadogResourcesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Datadog resource name cannot be null or empty"
                };
            }

            var armClient = CreateArmClient(request.TenantId);

            // Construct the resource ID for the Datadog monitor
            var resourceId = $"/subscriptions/{request.SubscriptionId}/resourceGroups/{request.ResourceGroupName}/providers/Microsoft.Datadog/monitors/{request.DatadogResourceName}";

            Azure.Core.ResourceIdentifier id = new Azure.Core.ResourceIdentifier(resourceId);
            var datadogMonitorResource = armClient.GetDatadogMonitorResource(id);
            var monitoredResources = datadogMonitorResource.GetMonitoredResources();

            var resourceNames = new List<string>();
            foreach (var resource in monitoredResources)
            {
                var resourceIdSegments = resource.Id.ToString().Split('/');
                var lastSegment = resourceIdSegments[^1];
                resourceNames.Add(lastSegment);
            }

            return new ListMonitoredDatadogResourcesResponse
            {
                IsSuccess = true,
                MonitoredResourceNames = { resourceNames }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing monitored resources for Datadog resource: {DatadogResource}",
                request.DatadogResourceName);

            return new ListMonitoredDatadogResourcesResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #endregion

    #region Monitor Methods

    /// <summary>
    /// Lists Monitor workspaces for a subscription.
    /// </summary>
    /// <param name="request">The request containing subscription and optional tenant ID.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of Monitor workspaces.</returns>
    public override async Task<ListMonitorWorkspacesResponse> ListMonitorWorkspaces(
        ListMonitorWorkspacesRequest request,
        ServerCallContext context)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.SubscriptionId))
            {
                return new ListMonitorWorkspacesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Subscription ID cannot be null or empty"
                };
            }

            var subscriptions = await GetSubscriptionsAsync(request.TenantId);
            var subscription = subscriptions.FirstOrDefault(s => s.SubscriptionId == request.SubscriptionId);
            if (subscription == null)
            {
                return new ListMonitorWorkspacesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Subscription '{request.SubscriptionId}' not found"
                };
            }

            var armClient = CreateArmClient(request.TenantId);
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));

            var workspaces = new List<MonitorWorkspace>();
            foreach (var workspace in subscriptionResource.GetOperationalInsightsWorkspaces())
            {
                workspaces.Add(new MonitorWorkspace
                {
                    Name = workspace.Data.Name ?? string.Empty,
                    CustomerId = workspace.Data.CustomerId?.ToString() ?? string.Empty,
                    ArmId = workspace.Id.ToString()
                });
            }

            return new ListMonitorWorkspacesResponse
            {
                IsSuccess = true,
                Workspaces = { workspaces }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Monitor workspaces for subscription: {SubscriptionId}", request.SubscriptionId);
            return new ListMonitorWorkspacesResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Lists Monitor tables for a workspace.
    /// </summary>
    /// <param name="request">The request containing subscription, resource group, workspace, and optional table type filter.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of Monitor tables.</returns>
    public override async Task<ListMonitorTablesResponse> ListMonitorTables(
        ListMonitorTablesRequest request,
        ServerCallContext context)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.SubscriptionId))
            {
                return new ListMonitorTablesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Subscription ID cannot be null or empty"
                };
            }

            if (string.IsNullOrWhiteSpace(request.ResourceGroupName))
            {
                return new ListMonitorTablesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Resource group name cannot be null or empty"
                };
            }

            if (string.IsNullOrWhiteSpace(request.WorkspaceName))
            {
                return new ListMonitorTablesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Workspace name cannot be null or empty"
                };
            }

            var subscriptions = await GetSubscriptionsAsync(request.TenantId);
            var subscription = subscriptions.FirstOrDefault(s => s.SubscriptionId == request.SubscriptionId);
            if (subscription == null)
            {
                return new ListMonitorTablesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Subscription '{request.SubscriptionId}' not found"
                };
            }

            var armClient = CreateArmClient(request.TenantId);
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));
            
            var resourceGroupResponse = await subscriptionResource.GetResourceGroups().GetAsync(request.ResourceGroupName);
            if (resourceGroupResponse?.Value == null)
            {
                return new ListMonitorTablesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Resource group '{request.ResourceGroupName}' not found"
                };
            }

            // Resolve workspace name (could be name or ID)
            var (workspaceId, workspaceName) = GetWorkspaceInfoAsync(request.WorkspaceName, subscriptionResource, request.TenantId);
            
            // Get the workspace
            var workspaceResponse = await resourceGroupResponse.Value.GetOperationalInsightsWorkspaceAsync(workspaceName);
            if (workspaceResponse?.Value == null)
            {
                return new ListMonitorTablesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Workspace '{workspaceName}' not found in resource group '{request.ResourceGroupName}'"
                };
            }

            // Get tables from the workspace
            var workspaceResource = workspaceResponse.Value;
            var tableOperations = workspaceResource.GetOperationalInsightsTables();
            var tables = new List<Azure.ResourceManager.OperationalInsights.OperationalInsightsTableResource>();
            await foreach (var table in tableOperations.GetAllAsync())
            {
                tables.Add(table);
            }

            // Filter by table type if specified
            var tableType = string.IsNullOrWhiteSpace(request.TableType) ? "CustomLog" : request.TableType;
            var filteredTables = tables
                .Where(table => string.IsNullOrEmpty(tableType) || table.Data.Schema.TableType?.ToString() == tableType)
                .Select(table => table.Data.Name ?? string.Empty)
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(name => name)
                .ToList();

            return new ListMonitorTablesResponse
            {
                IsSuccess = true,
                TableNames = { filteredTables }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Monitor tables for workspace: {WorkspaceName}", request.WorkspaceName);
            return new ListMonitorTablesResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Lists Monitor table types for a workspace.
    /// </summary>
    /// <param name="request">The request containing subscription, resource group, and workspace information.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of distinct Monitor table types.</returns>
    public override async Task<ListMonitorTableTypesResponse> ListMonitorTableTypes(
        ListMonitorTableTypesRequest request,
        ServerCallContext context)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.SubscriptionId))
            {
                return new ListMonitorTableTypesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Subscription ID cannot be null or empty"
                };
            }

            if (string.IsNullOrWhiteSpace(request.ResourceGroupName))
            {
                return new ListMonitorTableTypesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Resource group name cannot be null or empty"
                };
            }

            if (string.IsNullOrWhiteSpace(request.WorkspaceName))
            {
                return new ListMonitorTableTypesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Workspace name cannot be null or empty"
                };
            }

            var subscriptions = await GetSubscriptionsAsync(request.TenantId);
            var subscription = subscriptions.FirstOrDefault(s => s.SubscriptionId == request.SubscriptionId);
            if (subscription == null)
            {
                return new ListMonitorTableTypesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Subscription '{request.SubscriptionId}' not found"
                };
            }

            var armClient = CreateArmClient(request.TenantId);
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId));
            
            var resourceGroupResponse = await subscriptionResource.GetResourceGroups().GetAsync(request.ResourceGroupName);
            if (resourceGroupResponse?.Value == null)
            {
                return new ListMonitorTableTypesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Resource group '{request.ResourceGroupName}' not found"
                };
            }

            // Resolve workspace name (could be name or ID)
            var (workspaceId, workspaceName) = GetWorkspaceInfoAsync(request.WorkspaceName, subscriptionResource, request.TenantId);
            
            // Get the workspace
            var workspaceResponse = await resourceGroupResponse.Value.GetOperationalInsightsWorkspaceAsync(workspaceName);
            if (workspaceResponse?.Value == null)
            {
                return new ListMonitorTableTypesResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Workspace '{workspaceName}' not found in resource group '{request.ResourceGroupName}'"
                };
            }

            // Get tables from the workspace
            var workspaceResource = workspaceResponse.Value;
            var tableOperations = workspaceResource.GetOperationalInsightsTables();
            var tables = new List<Azure.ResourceManager.OperationalInsights.OperationalInsightsTableResource>();
            await foreach (var table in tableOperations.GetAllAsync())
            {
                tables.Add(table);
            }

            // Get distinct table types
            var tableTypes = tables
                .Select(table => table.Data.Schema.TableType?.ToString() ?? string.Empty)
                .Where(type => !string.IsNullOrEmpty(type))
                .Distinct()
                .OrderBy(type => type)
                .ToList();

            return new ListMonitorTableTypesResponse
            {
                IsSuccess = true,
                TableTypes = { tableTypes }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Monitor table types for workspace: {WorkspaceName}", request.WorkspaceName);
            return new ListMonitorTableTypesResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Helper method to resolve workspace name from either name or ID, similar to the original MonitorService.
    /// </summary>
    /// <param name="workspace">The workspace name or ID</param>
    /// <param name="subscriptionResource">The subscription resource</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <returns>A tuple of (workspaceId, workspaceName)</returns>
    private static (string id, string name) GetWorkspaceInfoAsync(string workspace, SubscriptionResource subscriptionResource, string? tenantId)
    {
        // Check if it's a workspace ID (GUID)
        bool isId = Guid.TryParse(workspace, out _);
        
        // Get all workspaces in the subscription
        var workspaces = new List<MonitorWorkspace>();
        foreach (var workspaceResource in subscriptionResource.GetOperationalInsightsWorkspaces())
        {
            workspaces.Add(new MonitorWorkspace
            {
                Name = workspaceResource.Data.Name ?? string.Empty,
                CustomerId = workspaceResource.Data.CustomerId?.ToString() ?? string.Empty,
                ArmId = workspaceResource.Id.ToString()
            });
        }

        // Find the matching workspace
        var matchingWorkspace = workspaces.FirstOrDefault(w =>
            isId ? w.CustomerId.Equals(workspace, StringComparison.OrdinalIgnoreCase)
                : w.Name.Equals(workspace, StringComparison.OrdinalIgnoreCase));

        if (matchingWorkspace == null)
        {
            throw new Exception($"Could not find workspace with {(isId ? "ID" : "name")} {workspace}");
        }

        return (matchingWorkspace.CustomerId, matchingWorkspace.Name);
    }

    #endregion

    #region Authorization Methods

    /// <summary>
    /// Lists role assignments for a scope.
    /// </summary>
    /// <param name="request">The request containing scope and tenant information.</param>
    /// <param name="context">The gRPC server call context.</param>
    /// <returns>A response containing the list of role assignments.</returns>
    public override async Task<ListRoleAssignmentsResponse> ListRoleAssignments(
        ListRoleAssignmentsRequest request,
        ServerCallContext context)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Scope))
            {
                return new ListRoleAssignmentsResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Scope is required"
                };
            }

            var armClient = CreateArmClient(request.TenantId);
            var scopeResourceId = new Azure.Core.ResourceIdentifier(request.Scope);
            var roleAssignmentCollection = armClient.GetRoleAssignments(scopeResourceId);

            var assignments = new List<RoleAssignment>();
            await foreach (var roleAssignmentResource in roleAssignmentCollection.GetAllAsync())
            {
                var assignment = roleAssignmentResource.Data;
                assignments.Add(new RoleAssignment
                {
                    Id = roleAssignmentResource.Id.ToString(),
                    Name = assignment.Name ?? string.Empty,
                    RoleDefinitionId = assignment.RoleDefinitionId?.ToString() ?? string.Empty,
                    Scope = assignment.Scope ?? string.Empty,
                    PrincipalId = assignment.PrincipalId?.ToString() ?? string.Empty,
                    PrincipalType = assignment.PrincipalType?.ToString() ?? string.Empty,
                    Description = assignment.Description ?? string.Empty,
                    DelegatedManagedIdentityResourceId = assignment.DelegatedManagedIdentityResourceId?.ToString() ?? string.Empty,
                    Condition = assignment.Condition ?? string.Empty
                });
            }

            return new ListRoleAssignmentsResponse
            {
                IsSuccess = true,
                RoleAssignments = { assignments }
            };
        }        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing role assignments for scope {Scope}", request.Scope);
            return new ListRoleAssignmentsResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #endregion
}
