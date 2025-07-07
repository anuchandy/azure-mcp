// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace AzureMcp.LocalServiceClient;

/// <summary>
/// Defines a contract for service clients that support async service startup.
/// </summary>
public interface IServiceClient
{
    /// <summary>
    /// Ensures the service client for local service is fully initialized and ready for use.
    /// Safe to call multiple times - subsequent calls return the same task.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the initialization operation and returns the local service endpoint URL.</returns>
    Task<string> EnsureServiceStartedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a default GrpcServiceHost for a local service.
    /// </summary>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <param name="localServiceName">The full name of the local service (e.g., "AzureMcp.LocalService.Arm").</param>
    /// <param name="extensionPath">The path to the local service executable.</param>
    /// <returns>A configured GrpcServiceHost instance.</returns>
    static GrpcServiceHost CreateDefaultServiceHost(ILoggerFactory loggerFactory, string localServiceName, string extensionPath)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(localServiceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(extensionPath);

        var serviceName = localServiceName.StartsWith("AzureMcp.LocalService.")
            ? localServiceName["AzureMcp.LocalService.".Length..]
            : localServiceName;

        var config = new GrpcServiceConfig
        {
            ServiceName = serviceName,
            ExtensionPath = extensionPath,
            ExecutableNames = new[]
            {
                $"{localServiceName}.exe",
                localServiceName
            },
            HealthEndpoint = "/ishealthy",
            StartupTimeoutSeconds = 30
        };
        return new GrpcServiceHost(loggerFactory.CreateLogger<GrpcServiceHost>(), config);
    }
}
