// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureMcp.GrpcClient;

/// <summary>
/// Configuration for a gRPC service extension.
/// </summary>
public sealed class GrpcServiceConfig
{
    /// <summary>
    /// The name of the service (used for logging).
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// The relative path to the extension executable from the base directory.
    /// Example: "ext/AzureMcp.Ext.Credential"
    /// </summary>
    public required string ExtensionPath { get; init; }

    /// <summary>
    /// Possible executable names to search for (platform-specific).
    /// If not provided, defaults to {ExtensionName}.exe and {ExtensionName}
    /// </summary>
    public string[]? ExecutableNames { get; init; }

    /// <summary>
    /// The health check endpoint path. Defaults to "/ishealthy".
    /// </summary>
    public string HealthEndpoint { get; init; } = "/ishealthy";

    /// <summary>
    /// Maximum time to wait for the service to become ready (in seconds). Default 30 seconds.
    /// </summary>
    public int StartupTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Additional environment variables to set for the process.
    /// </summary>
    public Dictionary<string, string>? EnvironmentVariables { get; init; }
}
