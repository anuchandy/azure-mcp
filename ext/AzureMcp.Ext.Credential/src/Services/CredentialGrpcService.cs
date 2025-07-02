// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using AzureMcp.Ext.Credential.Grpc;
using Grpc.Core;

namespace AzureMcp.Ext.Credential.Services;

public class CredentialGrpcService : CredentialService.CredentialServiceBase
{
    private readonly ILogger<CredentialGrpcService> _logger;
    private readonly CustomChainedCredential _credential;

    public CredentialGrpcService(ILogger<CredentialGrpcService> logger)
    {
        _logger = logger;
        _credential = new CustomChainedCredential();
    }

    public override Task<TokenResponse> GetToken(TokenRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogDebug("Received GetToken request for scopes: {Scopes}", string.Join(", ", request.Scopes));

            var requestContext = ConvertToTokenRequestContext(request);
            var cancellationToken = CreateCancellationToken(request, context);

            var accessToken = _credential.GetToken(requestContext, cancellationToken);

            var response = ConvertToTokenResponse(accessToken);
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

    public override async Task<TokenResponse> GetTokenAsync(TokenRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogDebug("Received GetTokenAsync request for scopes: {Scopes}", string.Join(", ", request.Scopes));

            var requestContext = ConvertToTokenRequestContext(request);
            var cancellationToken = CreateCancellationToken(request, context);

            var accessToken = await _credential.GetTokenAsync(requestContext, cancellationToken);

            var response = ConvertToTokenResponse(accessToken);
            _logger.LogDebug("Successfully obtained token asynchronously, expires at: {ExpiresOn}", 
                new DateTimeOffset(response.ExpiresOnTicks, TimeSpan.Zero));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get access token asynchronously");
            return CreateErrorResponse(ex);
        }
    }

    private static TokenRequestContext ConvertToTokenRequestContext(TokenRequest request)
    {
        var scopes = request.Scopes.ToArray();
        var parentRequestId = request.HasParentRequestId ? request.ParentRequestId : null;
        var claims = request.HasClaims ? request.Claims : null;
        var tenantId = request.HasTenantId ? request.TenantId : null;

        return new TokenRequestContext(scopes, parentRequestId, claims, tenantId);
    }

    private static TokenResponse ConvertToTokenResponse(AccessToken accessToken)
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
        return new TokenResponse
        {
            Success = false,
            ErrorMessage = exception.Message,
            ErrorType = exception.GetType().Name
        };
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
