// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.LocalServiceClient;

namespace AzureMcp.LocalServiceClient.Arm;

/// <summary>
/// Service for managing Azure Resource Manager operations via gRPC.
/// </summary>
public interface IArmServiceClient : IServiceClient
{
    /// <summary>
    /// Gets the status of the Identity service connectivity.
    /// </summary>
    /// <param name="tenantId">Optional tenant ID to test with</param>
    /// <param name="scopes">Optional scopes to test with</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response indicating the Identity service status</returns>
    Task<IdentityServiceStatusResult> GetIdentityServiceStatusAsync(
        string? tenantId = null, 
        string[]? scopes = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all accessible subscriptions for the specified tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant ID to list subscriptions for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the list of subscriptions</returns>
    Task<ListSubscriptionsResult> ListSubscriptionsAsync(
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets storage accounts for a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID to get storage accounts for</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the list of storage accounts</returns>
    Task<GetStorageAccountsResult> GetStorageAccountsAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets storage account keys for a specific storage account.
    /// </summary>
    /// <param name="accountName">Storage account name</param>
    /// <param name="subscriptionId">Subscription ID where the storage account exists</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the storage account keys</returns>
    Task<GetStorageAccountKeysResult> GetStorageAccountKeysAsync(
        string accountName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets connection string for a specific storage account.
    /// </summary>
    /// <param name="accountName">Storage account name</param>
    /// <param name="subscriptionId">Subscription ID where the storage account exists</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the storage account connection string</returns>
    Task<GetStorageAccountConnectionStringResult> GetStorageAccountConnectionStringAsync(
        string accountName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets Cosmos DB accounts for a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID to get Cosmos DB accounts for</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the list of Cosmos DB accounts</returns>
    Task<GetCosmosAccountsResult> GetCosmosAccountsAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific Cosmos DB account details.
    /// </summary>
    /// <param name="accountName">Cosmos DB account name</param>
    /// <param name="subscriptionId">Subscription ID where the Cosmos DB account exists</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the Cosmos DB account details</returns>
    Task<GetCosmosAccountResult> GetCosmosAccountAsync(
        string accountName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets App Configuration accounts for a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID to get App Configuration accounts for</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the list of App Configuration accounts</returns>
    Task<GetAppConfigAccountsResult> GetAppConfigAccountsAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the endpoint for a specific App Configuration account.
    /// </summary>
    /// <param name="accountName">App Configuration account name</param>
    /// <param name="subscriptionId">Subscription ID where the App Configuration account exists</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the App Configuration account endpoint</returns>
    Task<GetAppConfigAccountEndpointResult> GetAppConfigAccountEndpointAsync(
        string accountName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets Kusto clusters for a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID to get Kusto clusters for</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the list of Kusto clusters</returns>
    Task<GetKustoClustersResult> GetKustoClustersAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific Kusto cluster details.
    /// </summary>
    /// <param name="clusterName">Kusto cluster name</param>
    /// <param name="subscriptionId">Subscription ID where the Kusto cluster exists</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the Kusto cluster details</returns>
    Task<GetKustoClusterResult> GetKustoClusterAsync(
        string clusterName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all resource groups for a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID to get resource groups for</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the list of resource groups</returns>
    Task<GetResourceGroupsResult> GetResourceGroupsAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific resource group.
    /// </summary>
    /// <param name="resourceGroupName">Resource group name</param>
    /// <param name="subscriptionId">Subscription ID where the resource group exists</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the resource group details</returns>
    Task<GetResourceGroupResult> GetResourceGroupAsync(
        string resourceGroupName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Redis caches for a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID to get Redis caches for</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the list of Redis caches</returns>
    Task<ListRedisCachesResult> ListRedisCachesAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Redis access policy assignments for a cache.
    /// </summary>
    /// <param name="cacheName">Redis cache name</param>
    /// <param name="resourceGroupName">Resource group name containing the cache</param>
    /// <param name="subscriptionId">Subscription ID where the cache exists</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the list of access policy assignments</returns>
    Task<ListRedisAccessPolicyAssignmentsResult> ListRedisAccessPolicyAssignmentsAsync(
        string cacheName,
        string resourceGroupName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Redis clusters for a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID to get Redis clusters for</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the list of Redis clusters</returns>
    Task<ListRedisClustersResult> ListRedisClustersAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Redis databases for a cluster.
    /// </summary>
    /// <param name="clusterName">Redis cluster name</param>
    /// <param name="resourceGroupName">Resource group name containing the cluster</param>
    /// <param name="subscriptionId">Subscription ID where the cluster exists</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the list of Redis databases</returns>
    Task<ListRedisDatabasesResult> ListRedisDatabasesAsync(
        string clusterName,
        string resourceGroupName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists PostgreSQL flexible servers in a resource group.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the servers exist</param>
    /// <param name="resourceGroupName">Resource group name containing the servers</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the list of PostgreSQL server names</returns>
    Task<ListPostgreSqlServersResult> ListPostgreSqlServersAsync(
        string subscriptionId,
        string resourceGroupName,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets PostgreSQL server configuration details.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the server exists</param>
    /// <param name="resourceGroupName">Resource group name containing the server</param>
    /// <param name="serverName">PostgreSQL server name</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the PostgreSQL server configuration</returns>
    Task<GetPostgreSqlServerConfigResult> GetPostgreSqlServerConfigAsync(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific PostgreSQL server configuration parameter value.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the server exists</param>
    /// <param name="resourceGroupName">Resource group name containing the server</param>
    /// <param name="serverName">PostgreSQL server name</param>
    /// <param name="parameterName">Configuration parameter name</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the parameter value</returns>
    Task<GetPostgreSqlServerParameterResult> GetPostgreSqlServerParameterAsync(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string parameterName,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a specific PostgreSQL server configuration parameter value.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the server exists</param>
    /// <param name="resourceGroupName">Resource group name containing the server</param>
    /// <param name="serverName">PostgreSQL server name</param>
    /// <param name="parameterName">Configuration parameter name</param>
    /// <param name="parameterValue">Configuration parameter value to set</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the operation result</returns>
    Task<SetPostgreSqlServerParameterResult> SetPostgreSqlServerParameterAsync(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string parameterName,
        string parameterValue,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Azure Search services in a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the Search services exist</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the list of Search service names</returns>
    Task<ListSearchServicesResult> ListSearchServicesAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of checking Identity service status.
/// </summary>
public class IdentityServiceStatusResult
{
    /// <summary>
    /// Whether the Identity service check was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if check failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Additional details about the check.
    /// </summary>
    public string? Details { get; init; }
}

/// <summary>
/// Result of listing subscriptions.
/// </summary>
public class ListSubscriptionsResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// List of subscription data.
    /// </summary>
    public IReadOnlyList<SubscriptionData> Subscriptions { get; init; } = Array.Empty<SubscriptionData>();
}

/// <summary>
/// Subscription information.
/// </summary>
public class SubscriptionData
{
    /// <summary>
    /// The subscription ID (GUID).
    /// </summary>
    public required string SubscriptionId { get; init; }

    /// <summary>
    /// The display name of the subscription.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The tenant ID this subscription belongs to.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// The subscription state (e.g., "Enabled", "Disabled").
    /// </summary>
    public required string State { get; init; }
}

/// <summary>
/// Result of getting storage accounts.
/// </summary>
public class GetStorageAccountsResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// List of storage account names.
    /// </summary>
    public IReadOnlyList<string> StorageAccounts { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Result of getting storage account keys.
/// </summary>
public class GetStorageAccountKeysResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// List of storage account keys.
    /// </summary>
    public IReadOnlyList<StorageAccountKeyData> Keys { get; init; } = Array.Empty<StorageAccountKeyData>();
}

/// <summary>
/// Result of getting storage account connection string.
/// </summary>
public class GetStorageAccountConnectionStringResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The connection string.
    /// </summary>
    public string? ConnectionString { get; init; }
}

/// <summary>
/// Storage account key information.
/// </summary>
public class StorageAccountKeyData
{
    /// <summary>
    /// The key name (e.g., "key1", "key2").
    /// </summary>
    public required string KeyName { get; init; }

    /// <summary>
    /// The key value.
    /// </summary>
    public required string KeyValue { get; init; }

    /// <summary>
    /// The permissions for this key.
    /// </summary>
    public required string Permissions { get; init; }
}

/// <summary>
/// Result of getting Cosmos DB accounts.
/// </summary>
public class GetCosmosAccountsResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// List of Cosmos DB account names.
    /// </summary>
    public IReadOnlyList<string> CosmosAccounts { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Result of getting a Cosmos DB account.
/// </summary>
public class GetCosmosAccountResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The Cosmos DB account data.
    /// </summary>
    public CosmosAccountData? Account { get; init; }
}

/// <summary>
/// Cosmos DB account information.
/// </summary>
public class CosmosAccountData
{
    /// <summary>
    /// The account name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The account ID (resource ID).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The account location.
    /// </summary>
    public required string Location { get; init; }

    /// <summary>
    /// The account type (e.g., "DocumentDB").
    /// </summary>
    public required string AccountType { get; init; }

    /// <summary>
    /// The resource group name.
    /// </summary>
    public required string ResourceGroup { get; init; }

    /// <summary>
    /// The provisioning state.
    /// </summary>
    public required string ProvisioningState { get; init; }

    /// <summary>
    /// The document endpoint URL.
    /// </summary>
    public required string DocumentEndpoint { get; init; }
}

/// <summary>
/// Result of getting App Configuration accounts.
/// </summary>
public class GetAppConfigAccountsResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// List of App Configuration account data.
    /// </summary>
    public IReadOnlyList<AppConfigAccountData> AppConfigAccounts { get; init; } = Array.Empty<AppConfigAccountData>();
}

/// <summary>
/// Result of getting an App Configuration account.
/// </summary>
/// <summary>
/// Result of getting App Configuration account endpoint.
/// </summary>
public class GetAppConfigAccountEndpointResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The App Configuration account endpoint URL.
    /// </summary>
    public string? Endpoint { get; init; }
}

/// <summary>
/// App Configuration account information.
/// </summary>
public class AppConfigAccountData
{
    /// <summary>
    /// The account name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The account location.
    /// </summary>
    public required string Location { get; init; }

    /// <summary>
    /// The endpoint URL.
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// The creation date.
    /// </summary>
    public DateTime CreationDate { get; init; }

    /// <summary>
    /// Whether public network access is enabled.
    /// </summary>
    public bool PublicNetworkAccess { get; init; }

    /// <summary>
    /// The SKU name.
    /// </summary>
    public string? Sku { get; init; }

    /// <summary>
    /// Resource tags.
    /// </summary>
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Whether local auth is disabled.
    /// </summary>
    public bool DisableLocalAuth { get; init; }

    /// <summary>
    /// Soft delete retention in days.
    /// </summary>
    public int SoftDeleteRetentionInDays { get; init; }

    /// <summary>
    /// Whether purge protection is enabled.
    /// </summary>
    public bool EnablePurgeProtection { get; init; }

    /// <summary>
    /// The create mode.
    /// </summary>
    public string? CreateMode { get; init; }

    /// <summary>
    /// Managed identity information.
    /// </summary>
    public ManagedIdentityData? ManagedIdentity { get; init; }

    /// <summary>
    /// Encryption properties.
    /// </summary>
    public EncryptionData? Encryption { get; init; }
}

/// <summary>
/// Managed identity information.
/// </summary>
public class ManagedIdentityData
{
    /// <summary>
    /// System assigned identity information.
    /// </summary>
    public SystemAssignedIdentityData? SystemAssignedIdentity { get; init; }

    /// <summary>
    /// User assigned identities.
    /// </summary>
    public IReadOnlyList<UserAssignedIdentityData> UserAssignedIdentities { get; init; } = Array.Empty<UserAssignedIdentityData>();
}

/// <summary>
/// System assigned identity information.
/// </summary>
public class SystemAssignedIdentityData
{
    /// <summary>
    /// Whether system assigned identity is enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Tenant ID.
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Principal ID.
    /// </summary>
    public string? PrincipalId { get; init; }
}

/// <summary>
/// User assigned identity information.
/// </summary>
public class UserAssignedIdentityData
{
    /// <summary>
    /// Client ID.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// Principal ID.
    /// </summary>
    public string? PrincipalId { get; init; }
}

/// <summary>
/// Encryption properties.
/// </summary>
public class EncryptionData
{
    /// <summary>
    /// Key identifier.
    /// </summary>
    public string? KeyIdentifier { get; init; }

    /// <summary>
    /// Identity client ID.
    /// </summary>
    public string? IdentityClientId { get; init; }

    /// <summary>
    /// Whether key vault key identifier is valid.
    /// </summary>
    public bool IsKeyVaultKeyIdentifierValid { get; init; }

    /// <summary>
    /// Whether identity client ID is valid.
    /// </summary>
    public bool IsIdentityClientIdValid { get; init; }
}

/// <summary>
/// Result of getting Kusto clusters for a subscription.
/// </summary>
public class GetKustoClustersResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// List of Kusto cluster names.
    /// </summary>
    public IReadOnlyList<string> KustoClusters { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Result of getting a specific Kusto cluster.
/// </summary>
public class GetKustoClusterResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The Kusto cluster data.
    /// </summary>
    public KustoClusterData? Cluster { get; init; }
}

/// <summary>
/// Kusto cluster data.
/// </summary>
public class KustoClusterData
{
    /// <summary>
    /// The cluster name.
    /// </summary>
    public string ClusterName { get; init; } = string.Empty;

    /// <summary>
    /// The cluster URI.
    /// </summary>
    public string ClusterUri { get; init; } = string.Empty;

    /// <summary>
    /// The cluster location.
    /// </summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// The resource group name.
    /// </summary>
    public string ResourceGroupName { get; init; } = string.Empty;

    /// <summary>
    /// The subscription ID.
    /// </summary>
    public string SubscriptionId { get; init; } = string.Empty;

    /// <summary>
    /// The SKU information.
    /// </summary>
    public string Sku { get; init; } = string.Empty;

    /// <summary>
    /// The availability zones.
    /// </summary>
    public string Zones { get; init; } = string.Empty;

    /// <summary>
    /// The identity information.
    /// </summary>
    public string Identity { get; init; } = string.Empty;

    /// <summary>
    /// The ETag.
    /// </summary>
    public string ETag { get; init; } = string.Empty;

    /// <summary>
    /// The cluster state.
    /// </summary>
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// The provisioning state.
    /// </summary>
    public string ProvisioningState { get; init; } = string.Empty;

    /// <summary>
    /// The data ingestion URI.
    /// </summary>
    public string DataIngestionUri { get; init; } = string.Empty;

    /// <summary>
    /// The state reason.
    /// </summary>
    public string StateReason { get; init; } = string.Empty;

    /// <summary>
    /// Whether streaming ingest is enabled.
    /// </summary>
    public bool IsStreamingIngestEnabled { get; init; }

    /// <summary>
    /// The engine type.
    /// </summary>
    public string EngineType { get; init; } = string.Empty;

    /// <summary>
    /// Whether auto stop is enabled.
    /// </summary>
    public bool IsAutoStopEnabled { get; init; }
}

/// <summary>
/// Result of getting resource groups.
/// </summary>
public class GetResourceGroupsResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The list of resource groups.
    /// </summary>
    public List<ResourceGroupData> ResourceGroups { get; init; } = new();
}

/// <summary>
/// Result of getting a specific resource group.
/// </summary>
public class GetResourceGroupResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The resource group data.
    /// </summary>
    public ResourceGroupData? ResourceGroup { get; init; }
}

/// <summary>
/// Resource group data.
/// </summary>
public class ResourceGroupData
{
    /// <summary>
    /// The resource group name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The resource group ID.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// The resource group location.
    /// </summary>
    public string Location { get; init; } = string.Empty;
}

/// <summary>
/// Result of listing Redis caches.
/// </summary>
public class ListRedisCachesResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// List of Redis cache data.
    /// </summary>
    public List<RedisCacheData> RedisCaches { get; init; } = [];
}

/// <summary>
/// Result of listing Redis access policy assignments.
/// </summary>
public class ListRedisAccessPolicyAssignmentsResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// List of Redis access policy assignment data.
    /// </summary>
    public List<RedisAccessPolicyAssignmentData> RedisAccessPolicyAssignments { get; init; } = [];
}

/// <summary>
/// Result of listing Redis clusters.
/// </summary>
public class ListRedisClustersResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// List of Redis cluster data.
    /// </summary>
    public List<RedisClusterData> RedisClusters { get; init; } = [];
}

/// <summary>
/// Result of listing Redis databases.
/// </summary>
public class ListRedisDatabasesResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// List of Redis database data.
    /// </summary>
    public List<RedisDatabaseData> RedisDatabases { get; init; } = [];
}

/// <summary>
/// Redis cache data.
/// </summary>
public class RedisCacheData
{
    /// <summary>
    /// Name of the Redis cache resource.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Name of the resource group containing the Redis cache resource.
    /// </summary>
    public string ResourceGroupName { get; init; } = string.Empty;

    /// <summary>
    /// ID of the Azure subscription containing the Redis cache resource.
    /// </summary>
    public string SubscriptionId { get; init; } = string.Empty;

    /// <summary>
    /// Azure geo-location where the Redis cache resource lives.
    /// </summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// SKU of the Redis cache resource.
    /// </summary>
    public string Sku { get; init; } = string.Empty;

    /// <summary>
    /// Provisioning status of the Redis cache resource.
    /// </summary>
    public string ProvisioningState { get; init; } = string.Empty;

    /// <summary>
    /// Version of Redis server supported by the cache.
    /// </summary>
    public string RedisVersion { get; init; } = string.Empty;

    /// <summary>
    /// DNS host name clients use to connect to the Redis cache.
    /// </summary>
    public string HostName { get; init; } = string.Empty;

    /// <summary>
    /// Port for TLS (aka SSL) client connections to the Redis cache.
    /// </summary>
    public int SslPort { get; init; }

    /// <summary>
    /// Port for unencrypted client connections to the Redis cache.
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// Number of shards in a clustered Redis cache.
    /// </summary>
    public int ShardCount { get; init; }

    /// <summary>
    /// When a Redis cache is VNet-injected this contains the Resource ID of the subnet.
    /// </summary>
    public string SubnetId { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether public network access is allowed for the Redis cache.
    /// </summary>
    public bool PublicNetworkAccess { get; init; }

    /// <summary>
    /// Indicates whether connections are allowed on the non-SSL port for the Redis cache.
    /// </summary>
    public bool EnableNonSslPort { get; init; }

    /// <summary>
    /// Indicates whether access key authentication is disabled for the Redis cache.
    /// </summary>
    public bool IsAccessKeyAuthenticationDisabled { get; init; }

    /// <summary>
    /// Resource IDs of other Redis servers linked to this one for geo-replication.
    /// </summary>
    public List<string> LinkedServers { get; init; } = [];

    /// <summary>
    /// Minimum version of TLS supported for client connections to this Redis cache.
    /// </summary>
    public string MinimumTlsVersion { get; init; } = string.Empty;

    /// <summary>
    /// Resource IDs of private links used for network-isolated client connections to the Redis cache.
    /// </summary>
    public List<string> PrivateEndpointConnections { get; init; } = [];

    /// <summary>
    /// Number of replica nodes per primary node within the Redis cache.
    /// </summary>
    public int ReplicasPerPrimary { get; init; }

    /// <summary>
    /// Either 'Preview' to receive new versions of Redis service components sooner, or 'Stable' to be updated later (default).
    /// </summary>
    public string UpdateChannel { get; init; } = string.Empty;

    /// <summary>
    /// Zonal allocation policy determining how the cache is distributed across availability zones.
    /// </summary>
    public string ZonalAllocationPolicy { get; init; } = string.Empty;

    /// <summary>
    /// The availability zones in which the Redis cache is deployed.
    /// </summary>
    public List<string> Zones { get; init; } = [];

    /// <summary>
    /// Configuration settings for the Redis cache.
    /// </summary>
    public RedisCacheConfigurationData? Configuration { get; init; }

    /// <summary>
    /// System-assigned managed identity of the Redis cache resource.
    /// </summary>
    public ManagedIdentityData? Identity { get; init; }

    /// <summary>
    /// Tags on the Redis cache resource.
    /// </summary>
    public Dictionary<string, string> Tags { get; init; } = [];
}

/// <summary>
/// Redis cache configuration data.
/// </summary>
public class RedisCacheConfigurationData
{
    /// <summary>
    /// Indicates whether RDB (Redis Database Backup) is enabled for the Redis cache.
    /// </summary>
    public bool IsRdbBackupEnabled { get; init; }

    /// <summary>
    /// Number of minutes between RDB backups.
    /// </summary>
    public string RdbBackupFrequency { get; init; } = string.Empty;

    /// <summary>
    /// Indicates the maximum number of snapshots for RDB backup.
    /// </summary>
    public int RdbBackupMaxSnapshotCount { get; init; }

    /// <summary>
    /// Indicates whether AOF (Append Only File) backup is enabled for the Redis cache.
    /// </summary>
    public bool IsAofBackupEnabled { get; init; }

    /// <summary>
    /// Number of megabytes of memory reserved for fragmentation per shard.
    /// </summary>
    public string MaxFragmentationMemoryReserved { get; init; } = string.Empty;

    /// <summary>
    /// The eviction strategy used when your data won't fit within the cache memory limit.
    /// </summary>
    public string MaxMemoryPolicy { get; init; } = string.Empty;

    /// <summary>
    /// Number of megabytes of memory reserved for non-cache usage per shard e.g. failover.
    /// </summary>
    public string MaxMemoryReserved { get; init; } = string.Empty;

    /// <summary>
    /// Number of megabytes of memory reserved for non-cache usage per shard e.g. failover.
    /// </summary>
    public string MaxMemoryDelta { get; init; } = string.Empty;

    /// <summary>
    /// Maximum number of client connections.
    /// </summary>
    public int MaxClients { get; init; }

    /// <summary>
    /// The keyspace events which should be monitored.
    /// </summary>
    public string NotifyKeyspaceEvents { get; init; } = string.Empty;

    /// <summary>
    /// Preferred authentication method to communicate to storage account used for data archive.
    /// </summary>
    public string PreferredDataArchiveAuthMethod { get; init; } = string.Empty;

    /// <summary>
    /// Preferred authentication method to communicate to storage account used for data persistence.
    /// </summary>
    public string PreferredDataPersistenceAuthMethod { get; init; } = string.Empty;

    /// <summary>
    /// Zonal Configuration.
    /// </summary>
    public string ZonalConfiguration { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether client connection authentication is disabled.
    /// </summary>
    public string AuthNotRequired { get; init; } = string.Empty;
}

/// <summary>
/// Redis access policy assignment data.
/// </summary>
public class RedisAccessPolicyAssignmentData
{
    /// <summary>
    /// Name of the access policy.
    /// </summary>
    public string AccessPolicyName { get; init; } = string.Empty;

    /// <summary>
    /// Name of the identity assigned to an access policy.
    /// </summary>
    public string IdentityName { get; init; } = string.Empty;

    /// <summary>
    /// Provisioning status of the access policy assignment.
    /// </summary>
    public string ProvisioningState { get; init; } = string.Empty;
}

/// <summary>
/// Redis cluster data.
/// </summary>
public class RedisClusterData
{
    /// <summary>
    /// Name of the Redis cluster resource.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// ID of the Azure subscription containing the Redis cluster resource.
    /// </summary>
    public string SubscriptionId { get; init; } = string.Empty;

    /// <summary>
    /// Name of the resource group containing the Redis cluster resource.
    /// </summary>
    public string ResourceGroupName { get; init; } = string.Empty;

    /// <summary>
    /// Azure geo-location where the Redis cluster resource lives.
    /// </summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// SKU of the Redis cluster resource.
    /// </summary>
    public string Sku { get; init; } = string.Empty;

    /// <summary>
    /// Provisioning status of the Redis cluster resource.
    /// </summary>
    public string ProvisioningState { get; init; } = string.Empty;

    /// <summary>
    /// Current status of the Redis cluster.
    /// </summary>
    public string ResourceState { get; init; } = string.Empty;

    /// <summary>
    /// Version of Redis server supported by the cluster.
    /// </summary>
    public string RedisVersion { get; init; } = string.Empty;

    /// <summary>
    /// DNS host name clients use to connect to the Redis cluster.
    /// </summary>
    public string HostName { get; init; } = string.Empty;

    /// <summary>
    /// Minimum version of TLS supported for client connections to this Redis cluster.
    /// </summary>
    public string MinimumTlsVersion { get; init; } = string.Empty;

    /// <summary>
    /// Resource IDs of private links used for network-isolated client connections to the Redis cluster.
    /// </summary>
    public List<string> PrivateEndpointConnections { get; init; } = [];

    /// <summary>
    /// The availability zones in which the Redis cluster is deployed.
    /// </summary>
    public List<string> Zones { get; init; } = [];

    /// <summary>
    /// System-assigned managed identity of the Redis cluster resource.
    /// </summary>
    public ManagedIdentityData? Identity { get; init; }

    /// <summary>
    /// Tags on the Redis cluster resource.
    /// </summary>
    public Dictionary<string, string> Tags { get; init; } = [];
}

/// <summary>
/// Redis database data.
/// </summary>
public class RedisDatabaseData
{
    /// <summary>
    /// Name of the Redis database resource.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Name of the Redis cluster containing this database.
    /// </summary>
    public string ClusterName { get; init; } = string.Empty;

    /// <summary>
    /// Name of the resource group containing the Redis cluster that contains this database.
    /// </summary>
    public string ResourceGroupName { get; init; } = string.Empty;

    /// <summary>
    /// ID of the Azure subscription containing the Redis cluster that contains this database.
    /// </summary>
    public string SubscriptionId { get; init; } = string.Empty;

    /// <summary>
    /// Specifies whether redis clients can connect using TLS-encrypted or plaintext redis protocols.
    /// </summary>
    public string ClientProtocol { get; init; } = string.Empty;

    /// <summary>
    /// TCP port of the database endpoint.
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// Provisioning status of the Redis database resource.
    /// </summary>
    public string ProvisioningState { get; init; } = string.Empty;

    /// <summary>
    /// Current status of the Redis database.
    /// </summary>
    public string ResourceState { get; init; } = string.Empty;

    /// <summary>
    /// Clustering policy - default is OSSCluster.
    /// </summary>
    public string ClusteringPolicy { get; init; } = string.Empty;

    /// <summary>
    /// Redis eviction policy - default is VolatileLRU.
    /// </summary>
    public string EvictionPolicy { get; init; } = string.Empty;

    /// <summary>
    /// Sets whether AOF is enabled.
    /// </summary>
    public bool IsAofEnabled { get; init; }

    /// <summary>
    /// Sets whether RDB is enabled.
    /// </summary>
    public bool IsRdbEnabled { get; init; }

    /// <summary>
    /// Sets the frequency at which data is written to disk.
    /// </summary>
    public string AofFrequency { get; init; } = string.Empty;

    /// <summary>
    /// Sets the frequency at which a snapshot of the database is created.
    /// </summary>
    public string RdbFrequency { get; init; } = string.Empty;

    /// <summary>
    /// Optional set of redis modules to enable in this database.
    /// </summary>
    public List<RedisModuleData> Modules { get; init; } = [];

    /// <summary>
    /// Name for the group of geo-linked database resources.
    /// </summary>
    public string GeoReplicationGroupNickname { get; init; } = string.Empty;

    /// <summary>
    /// List of databases linked with this database for geo-replication.
    /// </summary>
    public List<string> GeoReplicationLinkedDatabases { get; init; } = [];
}

/// <summary>
/// Redis module data.
/// </summary>
public class RedisModuleData
{
    /// <summary>
    /// The name of the module, e.g. 'RedisBloom', 'RediSearch', 'RedisTimeSeries'.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Configuration options for the module, e.g. 'ERROR_RATE 0.01 INITIAL_SIZE 400'.
    /// </summary>
    public string Args { get; init; } = string.Empty;

    /// <summary>
    /// The version of the module, e.g. '1.0'.
    /// </summary>
    public string Version { get; init; } = string.Empty;
}

/// <summary>
/// Result of listing PostgreSQL servers.
/// </summary>
public class ListPostgreSqlServersResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// List of PostgreSQL server names.
    /// </summary>
    public List<string> ServerNames { get; init; } = [];
}

/// <summary>
/// Result of getting PostgreSQL server configuration.
/// </summary>
public class GetPostgreSqlServerConfigResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// PostgreSQL server configuration details.
    /// </summary>
    public PostgreSqlServerConfigData? ServerConfig { get; init; }
}

/// <summary>
/// Result of getting PostgreSQL server parameter.
/// </summary>
public class GetPostgreSqlServerParameterResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Parameter value.
    /// </summary>
    public string ParameterValue { get; init; } = string.Empty;
}

/// <summary>
/// Result of setting PostgreSQL server parameter.
/// </summary>
public class SetPostgreSqlServerParameterResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Success message with details.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// PostgreSQL server configuration data.
/// </summary>
public class PostgreSqlServerConfigData
{
    /// <summary>
    /// Server name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Server location.
    /// </summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// PostgreSQL version.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// SKU information.
    /// </summary>
    public string SkuName { get; init; } = string.Empty;

    /// <summary>
    /// Storage size in GB.
    /// </summary>
    public int StorageSizeGb { get; init; }

    /// <summary>
    /// Backup retention days.
    /// </summary>
    public int BackupRetentionDays { get; init; }

    /// <summary>
    /// Geo-redundant backup enabled.
    /// </summary>
    public string GeoRedundantBackup { get; init; } = string.Empty;
}

/// <summary>
/// Result of listing Azure Search services.
/// </summary>
public class ListSearchServicesResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// List of Search service names.
    /// </summary>
    public List<string> ServiceNames { get; init; } = [];
}