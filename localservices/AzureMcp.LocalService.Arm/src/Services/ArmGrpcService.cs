// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ResourceManager;
using AzureMcp.LocalService.Arm.Grpc;
using AzureMcp.LocalService.Arm.Clients;
using Grpc.Core;

namespace AzureMcp.LocalService.Arm.Services;

/// <summary>
/// gRPC service for providing Azure Resource Manager operations.
/// </summary>
public class ArmGrpcService : ArmService.ArmServiceBase
{
    private readonly ILogger<ArmGrpcService> _logger;
    private readonly IdentityClient _identityClient;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArmGrpcService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="identityClient">The Identity service client.</param>
    /// <param name="serviceProvider">The service provider for creating additional services.</param>
    public ArmGrpcService(ILogger<ArmGrpcService> logger, IdentityClient identityClient, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _identityClient = identityClient;
        _serviceProvider = serviceProvider;
    }

    // TODO: anu Add ARM operation methods here
    // Example usage in gRPC methods:
    // 
    // public override async Task<GetResourcesResponse> GetResources(GetResourcesRequest request, ServerCallContext context)
    // {
    //     using var armClient = CreateArmClient(request.TenantId);
    //     var subscription = armClient.GetSubscriptionResource(ResourceIdentifier.CreateSubscriptionResourceIdentifier(request.SubscriptionId));
    //     var resources = await subscription.GetGenericResourcesAsync().ToListAsync(context.CancellationToken);
    //     // convert to response etc..
    // }

    /// <summary>
    /// Creates an ArmClient with the Identity credential for the specified tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant ID for the ARM client.</param>
    /// <returns>An ArmClient instance configured with Identity authentication.</returns>
    private ArmClient CreateArmClient(string? tenantId = null)
    {
        var credential = new IdentityCredential(
            _identityClient, 
            _serviceProvider.GetRequiredService<ILogger<IdentityCredential>>(),
            tenantId);
        _logger.LogDebug("Created ArmClient for tenant: {TenantId}", tenantId ?? "default");
        return new ArmClient(credential);
    }
}
