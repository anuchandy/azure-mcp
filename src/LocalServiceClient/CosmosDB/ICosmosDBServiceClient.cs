// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureMcp.LocalServiceClient.CosmosDB;

/// <summary>
/// Service for managing Cosmos DB operations via gRPC.
/// </summary>
public interface ICosmosDBServiceClient : IServiceClient
{
    /// <summary>
    /// Lists all databases in a Cosmos DB account.
    /// </summary>
    /// <param name="accountName">The Cosmos DB account name.</param>
    /// <param name="subscriptionId">The Azure subscription ID.</param>
    /// <param name="authMethod">Authentication method to use ("Key", "Credential", or "ConnectionString").</param>
    /// <param name="tenantId">Optional tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of database names.</returns>
    Task<List<string>> ListDatabasesAsync(
        string accountName,
        string subscriptionId,
        string? authMethod = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all containers in a Cosmos DB database.
    /// </summary>
    /// <param name="accountName">The Cosmos DB account name.</param>
    /// <param name="databaseName">The database name.</param>
    /// <param name="subscriptionId">The Azure subscription ID.</param>
    /// <param name="authMethod">Authentication method to use ("Key", "Credential", or "ConnectionString").</param>
    /// <param name="tenantId">Optional tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of container names.</returns>
    Task<List<string>> ListContainersAsync(
        string accountName,
        string databaseName,
        string subscriptionId,
        string? authMethod = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}
