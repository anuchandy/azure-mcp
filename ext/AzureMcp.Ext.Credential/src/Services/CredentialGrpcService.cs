// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Identity;
using AzureMcp.Ext.Credential.Grpc;
using Grpc.Core;

namespace AzureMcp.Ext.Credential.Services;

/// <summary>
/// gRPC service for providing Azure credential token acquisition.
/// </summary>
public class CredentialGrpcService : CredentialService.CredentialServiceBase
{
    private readonly ILogger<CredentialGrpcService> _logger;
    private readonly CustomChainedCredential _credential;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialGrpcService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public CredentialGrpcService(ILogger<CredentialGrpcService> logger)
    {
        _logger = logger;
        _credential = new CustomChainedCredential();
    }

    /// <summary>
    /// Gets an access token.
    /// </summary>
    /// <param name="request">The token request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The token response.</returns>
    public override Task<TokenResponse> GetToken(TokenRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogDebug("Received GetToken request for scopes: {Scopes}", string.Join(", ", request.Scopes));

            var requestContext = ToTokenRequestContext(request);
            var cancellationToken = CreateCancellationToken(request, context);

            var accessToken = _credential.GetToken(requestContext, cancellationToken);

            var response = ToTokenResponse(accessToken);
            _logger.LogDebug("Successfully obtained token, expires at: {ExpiresOn}", 
                new DateTimeOffset(response.ExpiresOnTicks, TimeSpan.Zero));

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get access token");
            return Task.FromResult(CreateErrorResponse(ex));
        }
    }

    private static TokenRequestContext ToTokenRequestContext(TokenRequest request)
    {
        var scopes = request.Scopes.ToArray();
        var parentRequestId = request.HasParentRequestId ? request.ParentRequestId : null;
        var claims = request.HasClaims ? request.Claims : null;
        var tenantId = request.HasTenantId ? request.TenantId : null;

        return new TokenRequestContext(scopes, parentRequestId, claims, tenantId);
    }

    private static TokenResponse ToTokenResponse(AccessToken accessToken)
    {
        return new TokenResponse
        {
            Token = accessToken.Token,
            ExpiresOnTicks = accessToken.ExpiresOn.UtcTicks,
            Success = true
        };
    }

    private static TokenResponse CreateErrorResponse(Exception exception)
    {
        var response = new TokenResponse
        {
            Success = false,
            ErrorMessage = exception.Message,
            ErrorType = exception.GetType().Name
        };

        if (exception is Azure.Identity.AuthenticationFailedException authEx)
        {
            response.ErrorDetails = new ErrorDetails
            {
                IsAuthenticationFailure = true,
                ErrorCode = "AUTHENTICATION_FAILED",
                ErrorContext = authEx.Source ?? "Azure.Identity",
                IsRetryable = false 
            };
        }
        else if (exception is Azure.Identity.CredentialUnavailableException credEx)
        {
            response.ErrorDetails = new ErrorDetails
            {
                IsAuthenticationFailure = false,
                ErrorCode = "CREDENTIAL_UNAVAILABLE",
                ErrorContext = credEx.Source ?? "Azure.Identity",
                IsRetryable = true
            };
        }
        else if (exception is Azure.RequestFailedException reqEx)
        {
            response.ErrorDetails = new ErrorDetails
            {
                IsAuthenticationFailure = reqEx.Status == 401 || reqEx.Status == 403,
                ErrorCode = $"REQUEST_FAILED_{reqEx.Status}",
                ErrorContext = $"HTTP {reqEx.Status}: {reqEx.ErrorCode}",
                IsRetryable = reqEx.Status >= 500
            };
        }
        else
        {
            response.ErrorDetails = new ErrorDetails
            {
                IsAuthenticationFailure = false,
                ErrorCode = "UNKNOWN_ERROR",
                ErrorContext = exception.Source ?? "Unknown",
                IsRetryable = false
            };
        }
        return response;
    }

    private static CancellationToken CreateCancellationToken(TokenRequest request, ServerCallContext context)
    {
        if (!request.HasTimeoutSeconds || request.TimeoutSeconds <= 0)
        {
            return context.CancellationToken;
        }
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(request.TimeoutSeconds));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
            context.CancellationToken, 
            timeoutCts.Token);
        return combinedCts.Token;
    }
}
