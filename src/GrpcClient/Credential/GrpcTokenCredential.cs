// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System.Net;

namespace AzureMcp.GrpcClient.Credential;

/// <summary>
/// TokenCredential implementation that communicates with the credential gRPC service.
/// </summary>
public sealed class GrpcTokenCredential(
    string serviceEndpoint, 
    ILogger<GrpcTokenCredential> logger, 
    string? tenantId = null) : TokenCredential, IDisposable
{
    private readonly string _serviceEndpoint = serviceEndpoint ?? throw new ArgumentNullException(nameof(serviceEndpoint));
    private readonly ILogger<GrpcTokenCredential> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly string? _tenantId = tenantId;
    private GrpcChannel? _channel;
    private AzureMcp.Grpc.Client.CredentialService.CredentialServiceClient? _client;
    private bool _disposed;

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

        _logger.LogDebug("Acquiring token with scopes: {Scopes}", string.Join(", ", requestContext.Scopes));
        var response = await _client!.GetTokenAsync(request, cancellationToken: cancellationToken);
        return ParseResponse(response, _logger);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _channel?.Dispose();
            _disposed = true;
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
            _channel = GrpcChannel.ForAddress(_serviceEndpoint, channelOptions);
            _client = new Grpc.Client.CredentialService.CredentialServiceClient(_channel);
        }
    }

    private static AccessToken ParseResponse(AzureMcp.Grpc.Client.TokenResponse response, ILogger logger)
    {
        if (!response.Success)
        {
            var error = response.ErrorMessage ?? "Unknown error occurred.";
            logger.LogError("Token acquisition failed: {ErrorMessage}.", error);
            throw ToException(error, response.ErrorDetails);
        }
        
        var expiresOn = new DateTimeOffset(response.ExpiresOnTicks, TimeSpan.Zero);
        logger.LogDebug("Token acquired successfully, expires at: {ExpiresOn}.", expiresOn);
        return new AccessToken(response.Token, expiresOn);
    }

    private static Exception ToException(string errorMessage, Grpc.Client.ErrorDetails errorDetails)
    {
        var errorCode = errorDetails.ErrorCode;
        var errorContext = errorDetails.ErrorContext;
        var isRetryable = errorDetails.IsRetryable;

        if (errorCode == "0")
        {
            return new CredentialUnavailableException(errorMessage, errorCode, errorContext, isRetryable);
        }
        else if (errorCode == "1")
        {
            return new AuthenticationFailedException(errorMessage, errorCode, errorContext, isRetryable);
        }
        else if (errorCode.StartsWith("2_"))
        {
            if (int.TryParse(errorCode.AsSpan(2), out var statusCode))
            {
                return new Azure.RequestFailedException(statusCode, errorMessage);
            }
            else
            {
                return new AuthenticationFailedException(errorMessage, errorCode, errorContext, isRetryable);
            }
        }
        else
        {
            return new AuthenticationFailedException(errorMessage, errorCode, errorContext, isRetryable);
        }
    }
}
