// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using AzureMcp.Grpc.Client;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System.Net;

namespace AzureMcp.GrpcClient;

/// <summary>
/// TokenCredential implementation that communicates with the credential gRPC service.
/// </summary>
public sealed class GrpcTokenCredential : TokenCredential, IDisposable
{
    private readonly string _endpointUrl;
    private readonly ILogger<GrpcTokenCredential> _logger;
    private readonly string? _tenantId;
    private GrpcChannel? _channel;
    private AzureMcp.Grpc.Client.CredentialService.CredentialServiceClient? _client;
    private bool _disposed;

    public GrpcTokenCredential(string endpointUrl, ILogger<GrpcTokenCredential> logger, string? tenantId = null)
    {
        _endpointUrl = endpointUrl ?? throw new ArgumentNullException(nameof(endpointUrl));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tenantId = tenantId;
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return GetTokenAsync(requestContext, cancellationToken).GetAwaiter().GetResult();
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        EnsureClient();
        var request = new AzureMcp.Grpc.Client.TokenRequest();
        request.Scopes.AddRange(requestContext.Scopes);
        if (!string.IsNullOrEmpty(requestContext.ParentRequestId))
        {
            request.ParentRequestId = requestContext.ParentRequestId;
        }
        if (!string.IsNullOrEmpty(requestContext.Claims))
        {
            request.Claims = requestContext.Claims;
        }
        if (!string.IsNullOrEmpty(_tenantId))
        {
            request.TenantId = _tenantId;
        }
        request.TimeoutSeconds = 30;

        try
        {
            _logger.LogDebug("Requesting token with scopes: {Scopes}", string.Join(", ", requestContext.Scopes));
            var response = await _client!.GetTokenAsync(request, cancellationToken: cancellationToken);
            if (!response.Success)
            {
                var error = response.ErrorMessage ?? "Unknown error occurred.";
                _logger.LogError("Token request failed: {ErrorMessage}.", error);
                ThrowException(response);
            }
            var expiresOn = new DateTimeOffset(response.ExpiresOnTicks, TimeSpan.Zero);
            _logger.LogDebug("Token acquired successfully, expires at: {ExpiresOn}.", expiresOn);
            return new AccessToken(response.Token, expiresOn);
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            _logger.LogError(ex, "Failed to acquire token.");
            throw new InvalidOperationException($"Failed to acquire token: {ex.Message}", ex);
        }
    }

    private void EnsureClient()
    {
        if (_client == null)
        {
            var channelOptions = new GrpcChannelOptions
            {
                HttpClient = new HttpClient(new HttpClientHandler())
                {
                    DefaultRequestVersion = HttpVersion.Version20,
                    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
                }
            };
            _channel = GrpcChannel.ForAddress(_endpointUrl, channelOptions);
            _client = new AzureMcp.Grpc.Client.CredentialService.CredentialServiceClient(_channel);
        }
    }

    private static void ThrowException(AzureMcp.Grpc.Client.TokenResponse response)
    {
        var errorMessage = response.ErrorMessage ?? "Unknown error occurred.";
        var errorCode = response.ErrorDetails?.ErrorCode;
        var errorContext = response.ErrorDetails?.ErrorContext;
        var isRetryable = response.ErrorDetails?.IsRetryable ?? false;
        
        if (response.ErrorDetails != null)
        {
            if (response.ErrorDetails.IsAuthenticationFailure)
            {
                if (response.ErrorDetails.ErrorCode == "AUTHENTICATION_FAILED")
                {
                    throw new AuthenticationFailedException(errorMessage, errorCode, errorContext, isRetryable);
                }
                if (response.ErrorDetails.ErrorCode?.StartsWith("REQUEST_FAILED_") == true)
                {
                    if (int.TryParse(response.ErrorDetails.ErrorCode.Substring("REQUEST_FAILED_".Length), out var statusCode))
                    {
                        throw new Azure.RequestFailedException(statusCode, errorMessage);
                    }
                }
            }
            if (response.ErrorDetails.ErrorCode == "CREDENTIAL_UNAVAILABLE")
            {
                throw new CredentialUnavailableException(errorMessage, errorCode, errorContext, isRetryable);
            }
        }
        throw new AuthenticationFailedException(errorMessage, errorCode, errorContext, isRetryable);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _channel?.Dispose();
            _disposed = true;
        }
    }
}
