// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Grpc.Net.Client;
using AzureMcp.LocalService.Identity.Grpc;

namespace AzureMcp.LocalService.CosmosDB.Clients;

/// <summary>
/// Client for connecting to an existing Identity gRPC service.
/// </summary>
public sealed class IdentityClient : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly IdentityService.IdentityServiceClient _client;
    private readonly ILogger<IdentityClient> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityClient"/> class.
    /// </summary>
    /// <param name="identityServiceEndpoint">The Identity service gRPC endpoint URL.</param>
    /// <param name="logger">The logger instance.</param>
    public IdentityClient(string identityServiceEndpoint, ILogger<IdentityClient> logger)
    {
        if (string.IsNullOrWhiteSpace(identityServiceEndpoint))
            throw new ArgumentException("Identity service endpoint cannot be null or empty", nameof(identityServiceEndpoint));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _channel = GrpcChannel.ForAddress(identityServiceEndpoint);
        _client = new IdentityService.IdentityServiceClient(_channel);
        
        _logger.LogInformation("Identity client initialized for endpoint: {Endpoint}", identityServiceEndpoint);
    }

    /// <summary>
    /// Gets an access token from the Identity service.
    /// </summary>
    /// <param name="scopes">The scopes for which to request the token.</param>
    /// <param name="tenantId">Optional tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An access token.</returns>
    public async Task<AccessToken> GetTokenAsync(string[] scopes, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new TokenRequest();
            request.Scopes.AddRange(scopes);
            
            if (!string.IsNullOrEmpty(tenantId))
            {
                request.TenantId = tenantId;
            }

            _logger.LogDebug("Requesting token with scopes: {Scopes}, tenant: {TenantId}", 
                string.Join(", ", scopes), tenantId ?? "default");

            var response = await _client.GetTokenAsync(request, cancellationToken: cancellationToken);

            if (!response.Success)
            {
                throw new InvalidOperationException(
                    response.ErrorMessage ?? "Token acquisition failed");
            }

            var expiresOn = new DateTimeOffset(response.ExpiresOnTicks, TimeSpan.Zero);
            return new AccessToken(response.Token, expiresOn);
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            _logger.LogError(ex, "Failed to get token from Identity service");
            throw new InvalidOperationException("Failed to communicate with Identity service", ex);
        }
    }

    /// <summary>
    /// Disposes the Identity client and releases associated resources.
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
