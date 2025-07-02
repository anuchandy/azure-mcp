// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Identity;
using AzureMcp.LocalService.Identity.Grpc;
using Grpc.Core;

namespace AzureMcp.LocalService.Identity.Services;

/// <summary>
/// gRPC service for providing Azure identity token acquisition.
/// </summary>
public class IdentityGrpcService : IdentityService.IdentityServiceBase
{
    private readonly ILogger<IdentityGrpcService> _logger;
    private readonly CustomChainedCredential _credential;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityGrpcService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public IdentityGrpcService(ILogger<IdentityGrpcService> logger)
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

        response.ErrorDetails = exception switch
        {
            Azure.Identity.CredentialUnavailableException ex => new ErrorDetails
            {
                IsAuthenticationFailure = false,
                ErrorCode = "0",
                ErrorContext = ex.Source ?? "Azure.Identity",
                IsRetryable = true
            },
            Azure.Identity.AuthenticationFailedException ex => new ErrorDetails
            {
                IsAuthenticationFailure = true,
                ErrorCode = "1",
                ErrorContext = ex.Source ?? "Azure.Identity",
                IsRetryable = false
            },
            Azure.RequestFailedException ex => new ErrorDetails
            {
                IsAuthenticationFailure = ex.Status == 401 || ex.Status == 403,
                ErrorCode = $"2_{ex.Status}",
                ErrorContext = $"HTTP {ex.Status}: {ex.ErrorCode}",
                IsRetryable = ex.Status >= 500
            },
            _ => new ErrorDetails
            {
                IsAuthenticationFailure = false,
                ErrorCode = "3",
                ErrorContext = exception.Source ?? "Unknown",
                IsRetryable = false
            }
        };
        
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
