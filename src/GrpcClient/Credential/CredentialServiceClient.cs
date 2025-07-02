// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace AzureMcp.GrpcClient.Credential;

/// <summary>
/// Service for managing credential acquisition with gRPC.
/// </summary>
public sealed class CredentialServiceClient : ICredentialServiceClient, IDisposable
{
    private readonly GrpcServiceHost _serviceHost;
    private readonly ILogger<CredentialServiceClient> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<string, TokenCredential> credentialsCache = new();
    private bool _disposed;

    public CredentialServiceClient(ILoggerFactory loggerFactory)
        : this(loggerFactory, CreateDefaultServiceHost(loggerFactory))
    {
    }

    public CredentialServiceClient(ILoggerFactory loggerFactory, GrpcServiceHost serviceHost)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _serviceHost = serviceHost ?? throw new ArgumentNullException(nameof(serviceHost));
        _logger = CreateLogger<CredentialServiceClient>();
    }

    private static GrpcServiceHost CreateDefaultServiceHost(ILoggerFactory loggerFactory)
    {
        var config = new GrpcServiceConfig
        {
            ServiceName = "Credential",
            ExtensionPath = "/Users/anuchandy/code/azure-mcp/ext/AzureMcp.Ext.Credential/bin/Debug/net9.0/",  // Path.Combine("ext", "AzureMcp.Ext.Credential"),
            ExecutableNames = new[]
            {
                "AzureMcp.Ext.Credential.exe",
                "AzureMcp.Ext.Credential"
            },
            HealthEndpoint = "/ishealthy",
            StartupTimeoutSeconds = 30
        };

        return new GrpcServiceHost(loggerFactory.CreateLogger<GrpcServiceHost>(), config);
    }

    public async Task<TokenCredential> GetCredentialAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = tenantId ?? string.Empty;
        if (!credentialsCache.TryGetValue(cacheKey, out var credential))
        {
            var serviceEndpoint = await _serviceHost.StartServiceAsync(cancellationToken);
            credential = new GrpcTokenCredential(serviceEndpoint, CreateLogger<GrpcTokenCredential>());
            credentialsCache[cacheKey] = credential;
            _logger.LogDebug("Obtained credential for tenant: {TenantId}", tenantId ?? "default");
        }
        return credential;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var credential in credentialsCache.Values)
            {
                if (credential is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            credentialsCache.Clear();
            _serviceHost.Dispose();
            _disposed = true;
        }
    }

    private ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();
}
