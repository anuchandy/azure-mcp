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