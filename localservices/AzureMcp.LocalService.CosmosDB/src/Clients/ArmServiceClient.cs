// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Grpc.Net.Client;
using AzureMcp.LocalService.Arm.Grpc;

namespace AzureMcp.LocalService.CosmosDB.Clients;

/// <summary>
/// Minimal client for connecting to the ARM gRPC service to fetch Cosmos account information.
/// </summary>
public sealed class ArmServiceClient : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly ArmService.ArmServiceClient _client;
    private readonly ILogger<ArmServiceClient> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArmServiceClient"/> class.
    /// </summary>
    /// <param name="armServiceEndpoint">The ARM service gRPC endpoint URL.</param>
    /// <param name="logger">The logger instance.</param>
    public ArmServiceClient(string armServiceEndpoint, ILogger<ArmServiceClient> logger)
    {
        if (string.IsNullOrWhiteSpace(armServiceEndpoint))
            throw new ArgumentException("ARM service endpoint cannot be null or empty", nameof(armServiceEndpoint));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _channel = GrpcChannel.ForAddress(armServiceEndpoint);
        _client = new ArmService.ArmServiceClient(_channel);
        
        _logger.LogInformation("ARM client initialized for endpoint: {Endpoint}", armServiceEndpoint);
    }

    /// <summary>
    /// Gets a specific Cosmos DB account details from the ARM service.
    /// </summary>
    /// <param name="accountName">The Cosmos DB account name.</param>
    /// <param name="subscriptionId">The subscription ID.</param>
    /// <param name="tenantId">Optional tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Cosmos DB account data.</returns>
    public async Task<CosmosAccountData> GetCosmosAccountAsync(
        string accountName, 
        string subscriptionId, 
        string? tenantId = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetCosmosAccountRequest
            {
                AccountName = accountName,
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            _logger.LogDebug("Requesting Cosmos account: {AccountName} in subscription: {SubscriptionId}", 
                accountName, subscriptionId);

            var response = await _client.GetCosmosAccountAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to get Cosmos account: {response.ErrorMessage}");
            }

            return response.Account;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Cosmos account {AccountName} from ARM service", accountName);
            throw;
        }
    }

    /// <summary>
    /// Disposes the client resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _channel?.Dispose();
            _disposed = true;
        }
    }
}