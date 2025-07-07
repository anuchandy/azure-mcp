// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Cosmos;
using Grpc.Core;
using AzureMcp.LocalService.CosmosDB.Grpc;
using AzureMcp.LocalService.CosmosDB.Clients;
using System.Collections.Concurrent;

namespace AzureMcp.LocalService.CosmosDB.Services;

/// <summary>
/// gRPC service for providing Cosmos DB data operations.
/// </summary>
public class CosmosDBGrpcService : CosmosDBService.CosmosDBServiceBase, IDisposable
{
    private const string ErrorAccountNameRequired = "Account name cannot be null or empty";
    private const string ErrorDatabaseNameRequired = "Database name cannot be null or empty";
    private const string ErrorSubscriptionIdRequired = "Subscription ID cannot be null or empty";
    private const string ErrorInvalidAuthMethod = "Authentication method must be 'Key', 'Credential', or 'ConnectionString'";
    private const string CosmosBaseUri = "https://{0}.documents.azure.com:443/";

    private static readonly HashSet<string> ValidAuthMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Key", "Credential", "ConnectionString"
    };

    private readonly ILogger<CosmosDBGrpcService> _logger;
    private readonly ArmServiceClient _armClient;
    private readonly IdentityClient _identityClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, CosmosClient> _cosmosClients = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDBGrpcService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="armClient">The ARM service client.</param>
    /// <param name="identityClient">The identity service client.</param>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    public CosmosDBGrpcService(ILogger<CosmosDBGrpcService> logger, ArmServiceClient armClient, IdentityClient identityClient, ILoggerFactory loggerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _armClient = armClient ?? throw new ArgumentNullException(nameof(armClient));
        _identityClient = identityClient ?? throw new ArgumentNullException(nameof(identityClient));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// Lists all databases in a Cosmos DB account.
    /// </summary>
    /// <param name="request">The request containing account name, subscription ID, and authentication options.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of database names.</returns>
    public override async Task<ListDatabasesResponse> ListDatabases(
        ListDatabasesRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.AccountName))
        {
            return new ListDatabasesResponse
            {
                IsSuccess = false,
                ErrorMessage = ErrorAccountNameRequired
            };
        }

        if (string.IsNullOrWhiteSpace(request.SubscriptionId))
        {
            return new ListDatabasesResponse
            {
                IsSuccess = false,
                ErrorMessage = ErrorSubscriptionIdRequired
            };
        }

        var authMethod = NormalizeAuthMethod(request.AuthMethod);
        if (!ValidAuthMethods.Contains(authMethod))
        {
            return new ListDatabasesResponse
            {
                IsSuccess = false,
                ErrorMessage = ErrorInvalidAuthMethod
            };
        }

        try
        {
            var cosmosClient = await GetCosmosClientAsync(
                request.AccountName,
                request.SubscriptionId,
                authMethod,
                request.TenantId,
                context.CancellationToken);

            var databases = new List<string>();
            var iterator = cosmosClient.GetDatabaseQueryIterator<DatabaseProperties>();
            
            while (iterator.HasMoreResults)
            {
                var results = await iterator.ReadNextAsync(context.CancellationToken);
                databases.AddRange(results.Select(r => r.Id));
            }

            var response = new ListDatabasesResponse { IsSuccess = true };
            response.DatabaseNames.AddRange(databases);

            return response;
        }
        catch (Exception ex)
        {
            return new ListDatabasesResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Lists all containers in a Cosmos DB database.
    /// </summary>
    /// <param name="request">The request containing account name, database name, subscription ID, and authentication options.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A response containing the list of container names.</returns>
    public override async Task<ListContainersResponse> ListContainers(
        ListContainersRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.AccountName))
        {
            return new ListContainersResponse
            {
                IsSuccess = false,
                ErrorMessage = ErrorAccountNameRequired
            };
        }

        if (string.IsNullOrWhiteSpace(request.DatabaseName))
        {
            return new ListContainersResponse
            {
                IsSuccess = false,
                ErrorMessage = ErrorDatabaseNameRequired
            };
        }

        if (string.IsNullOrWhiteSpace(request.SubscriptionId))
        {
            return new ListContainersResponse
            {
                IsSuccess = false,
                ErrorMessage = ErrorSubscriptionIdRequired
            };
        }

        var authMethod = NormalizeAuthMethod(request.AuthMethod);
        if (!ValidAuthMethods.Contains(authMethod))
        {
            return new ListContainersResponse
            {
                IsSuccess = false,
                ErrorMessage = ErrorInvalidAuthMethod
            };
        }

        try
        {
            var cosmosClient = await GetCosmosClientAsync(
                request.AccountName,
                request.SubscriptionId,
                authMethod,
                request.TenantId,
                context.CancellationToken);

            var database = cosmosClient.GetDatabase(request.DatabaseName);
            var containers = new List<string>();
            var iterator = database.GetContainerQueryIterator<ContainerProperties>();
            
            while (iterator.HasMoreResults)
            {
                var results = await iterator.ReadNextAsync(context.CancellationToken);
                containers.AddRange(results.Select(r => r.Id));
            }

            var response = new ListContainersResponse { IsSuccess = true };
            response.ContainerNames.AddRange(containers);

            return response;
        }
        catch (Exception ex)
        {
            return new ListContainersResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Creates a Cosmos DB client with the specified authentication method.
    /// </summary>
    /// <param name="accountName">The account name.</param>
    /// <param name="subscriptionId">The subscription ID.</param>
    /// <param name="authMethod">The authentication method to use.</param>
    /// <param name="tenantId">Optional tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A configured Cosmos DB client.</returns>
    private async Task<CosmosClient> CreateCosmosClientWithAuth(
        string accountName,
        string subscriptionId,
        string authMethod,
        string? tenantId,
        CancellationToken cancellationToken)
    {
        var clientOptions = new CosmosClientOptions 
        { 
            AllowBulkExecution = true,
        };
        clientOptions.CosmosClientTelemetryOptions.DisableDistributedTracing = false;

        CosmosClient cosmosClient;
        
        switch (authMethod.ToLowerInvariant())
        {
            case "key":
                var cosmosAccount = await _armClient.GetCosmosAccountAsync(accountName, subscriptionId, tenantId, cancellationToken);
                cosmosClient = new CosmosClient(
                    string.Format(CosmosBaseUri, accountName),
                    cosmosAccount.PrimaryMasterKey,
                    clientOptions);
                break;

            case "credential":
            default:
                var credentialLogger = _loggerFactory.CreateLogger<IdentityCredential>();
                var credential = new IdentityCredential(_identityClient, credentialLogger, tenantId);
                cosmosClient = new CosmosClient(
                    string.Format(CosmosBaseUri, accountName),
                    credential,
                    clientOptions);
                break;
        }

        // Validate the client by performing a lightweight operation
        await ValidateCosmosClientAsync(cosmosClient, cancellationToken);

        return cosmosClient;
    }

    /// <summary>
    /// Gets or creates a Cosmos DB client for the specified account.
    /// </summary>
    /// <param name="accountName">The account name.</param>
    /// <param name="subscriptionId">The subscription ID.</param>
    /// <param name="authMethod">The authentication method to use. Supports "Key" and "Credential".</param>
    /// <param name="tenantId">Optional tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A configured Cosmos DB client.</returns>
    private async Task<CosmosClient> GetCosmosClientAsync(
        string accountName,
        string subscriptionId,
        string authMethod,
        string? tenantId,
        CancellationToken cancellationToken)
    {
        var clientKey = accountName;
        
        if (_cosmosClients.TryGetValue(clientKey, out var existingClient))
        {
            return existingClient;
        }

        CosmosClient cosmosClient;

        try
        {
            // First attempt with requested auth method
            cosmosClient = await CreateCosmosClientWithAuth(
                accountName,
                subscriptionId,
                authMethod,
                tenantId,
                cancellationToken);

            _cosmosClients[clientKey] = cosmosClient;
            return cosmosClient;
        }
        catch (Exception ex) when (
            authMethod.ToLowerInvariant() == "credential" &&
            (ex.Message.Contains("401") || ex.Message.Contains("403")))
        {
            // If credential auth fails with 401/403, try key auth
            cosmosClient = await CreateCosmosClientWithAuth(
                accountName,
                subscriptionId,
                "key",
                tenantId,
                cancellationToken);

            _cosmosClients[clientKey] = cosmosClient;
            return cosmosClient;
        }
    }

    /// <summary>
    /// Validates the Cosmos DB client by performing a lightweight read operation.
    /// </summary>
    /// <param name="client">The Cosmos DB client to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the validation operation.</returns>
    private static async Task ValidateCosmosClientAsync(CosmosClient client, CancellationToken cancellationToken)
    {
        try
        {
            await client.ReadAccountAsync();
        }
        catch (CosmosException ex)
        {
            throw new InvalidOperationException($"Failed to validate CosmosClient: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unexpected error while validating CosmosClient: {ex.Message}", ex);
        }
    }

    private static string NormalizeAuthMethod(string? authMethod)
    {
        return string.IsNullOrWhiteSpace(authMethod) ? "Credential" : authMethod;
    }

    /// <summary>
    /// Disposes the service and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
    }

    /// <summary>
    /// Disposes the service and cleans up resources.
    /// </summary>
    /// <param name="disposing">True if disposing from Dispose method, false if from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var client in _cosmosClients.Values)
            {
                client?.Dispose();
            }
            _cosmosClients.Clear();
            _armClient?.Dispose();
            _identityClient?.Dispose();
        }
    }
}