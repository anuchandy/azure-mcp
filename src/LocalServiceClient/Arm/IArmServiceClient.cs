// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.LocalServiceClient;

namespace AzureMcp.LocalServiceClient.Arm;

/// <summary>
/// Service for managing Azure Resource Manager operations via gRPC.
/// </summary>
public interface IArmServiceClient : IServiceClient
{
    /// <summary>
    /// Gets the status of the Identity service connectivity.
    /// </summary>
    /// <param name="tenantId">Optional tenant ID to test with</param>
    /// <param name="scopes">Optional scopes to test with</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response indicating the Identity service status</returns>
    Task<IdentityServiceStatusResult> GetIdentityServiceStatusAsync(
        string? tenantId = null, 
        string[]? scopes = null, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of checking Identity service status.
/// </summary>
public class IdentityServiceStatusResult
{
    /// <summary>
    /// Whether the Identity service check was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if check failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Additional details about the check.
    /// </summary>
    public string? Details { get; init; }
}