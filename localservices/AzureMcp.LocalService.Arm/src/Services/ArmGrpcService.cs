// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.LocalService.Arm.Grpc;
using Grpc.Core;

namespace AzureMcp.LocalService.Arm.Services;

/// <summary>
/// gRPC service for providing Azure Resource Manager operations.
/// </summary>
public class ArmGrpcService : ArmService.ArmServiceBase
{
    private readonly ILogger<ArmGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArmGrpcService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ArmGrpcService(ILogger<ArmGrpcService> logger)
    {
        _logger = logger;
    }

    // TODO: Add ARM operation methods here
}
