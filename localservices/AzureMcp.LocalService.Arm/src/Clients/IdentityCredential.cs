// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;

namespace AzureMcp.LocalService.Arm.Clients;

/// <summary>
/// A TokenCredential implementation that uses the Identity gRPC service for authentication.
/// </summary>
public sealed class IdentityCredential : TokenCredential, IDisposable
{
    private readonly IdentityClient _identityClient;
    private readonly string? _tenantId;
    private readonly ILogger<IdentityCredential> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityCredential"/> class.
    /// </summary>
    /// <param name="identityClient">The Identity client instance.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="tenantId">Optional tenant ID to use for all token requests.</param>
    public IdentityCredential(IdentityClient identityClient, ILogger<IdentityCredential> logger, string? tenantId = null)
    {
        _identityClient = identityClient ?? throw new ArgumentNullException(nameof(identityClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tenantId = tenantId;

        _logger.LogDebug("IdentityCredential created for tenant: {TenantId}", tenantId ?? "default");
    }

    /// <summary>
    /// Gets an access token for the specified request.
    /// </summary>
    /// <param name="requestContext">The token request context containing scopes and other details.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An access token.</returns>
    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Getting token for scopes: {Scopes}", string.Join(", ", requestContext.Scopes));
            var tenantId = !string.IsNullOrEmpty(requestContext.TenantId) ? requestContext.TenantId : _tenantId;

            var token = await _identityClient.GetTokenAsync(
                requestContext.Scopes.ToArray(), 
                tenantId, 
                cancellationToken);

            _logger.LogDebug("Successfully obtained token for scopes: {Scopes}", string.Join(", ", requestContext.Scopes));
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get token for scopes: {Scopes}", string.Join(", ", requestContext.Scopes));
            throw;
        }
    }

    /// <summary>
    /// Gets an access token for the specified request synchronously.
    /// </summary>
    /// <param name="requestContext">The token request context containing scopes and other details.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An access token.</returns>
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        try
        {
            return GetTokenAsync(requestContext, cancellationToken).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get token synchronously for scopes: {Scopes}", string.Join(", ", requestContext.Scopes));
            throw;
        }
    }

    /// <summary>
    /// Disposes the IdentityCredential and releases associated resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _identityClient?.Dispose();
            _disposed = true;
        }
    }
}
