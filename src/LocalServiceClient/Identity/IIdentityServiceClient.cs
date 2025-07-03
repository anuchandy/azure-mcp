// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;

namespace AzureMcp.LocalServiceClient.Identity;

/// <summary>
/// Service for managing credential acquisition with support for both direct and gRPC modes.
/// </summary>
public interface IIdentityServiceClient : IInitializableServiceClient
{
    /// <summary>
    /// Gets a TokenCredential for the specified tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A TokenCredential instance</returns>
    Task<TokenCredential> GetCredentialAsync(string? tenantId = null, CancellationToken cancellationToken = default);
}
