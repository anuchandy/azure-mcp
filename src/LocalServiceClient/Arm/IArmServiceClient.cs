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