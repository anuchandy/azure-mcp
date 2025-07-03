// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureMcp.LocalServiceClient;

/// <summary>
/// Defines a contract for service clients that support async initialization.
/// </summary>
public interface IInitializableServiceClient
{
    /// <summary>
    /// Ensures the service client for local service is fully initialized and ready for use.
    /// Safe to call multiple times - subsequent calls return the same task.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the initialization operation and returns the local service endpoint URL.</returns>
    Task<string> EnsureInitializedAsync(CancellationToken cancellationToken = default);
}
