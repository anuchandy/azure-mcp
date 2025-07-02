// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.Extensions.Logging;

namespace AzureMcp.GrpcClient;

/// <summary>
/// Service for managing credential acquisition with support for both direct and gRPC modes.
/// </summary>
public sealed class CredentialGrpcClient : ICredentialGrpcClient, IDisposable
{
    private readonly GrpcServiceHost _serviceHost;
    private readonly ILogger<CredentialGrpcClient> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<string, GrpcTokenCredential> credentialsCache = new();
    private bool _disposed;

    public CredentialGrpcClient(ILoggerFactory loggerFactory)
        : this(loggerFactory, CreateDefaultServiceHost(loggerFactory))
    {
    }

    public CredentialGrpcClient(ILoggerFactory loggerFactory, GrpcServiceHost serviceHost)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _serviceHost = serviceHost ?? throw new ArgumentNullException(nameof(serviceHost));
        _logger = CreateLogger<CredentialGrpcClient>();
    }

    private static GrpcServiceHost CreateDefaultServiceHost(ILoggerFactory loggerFactory)
    {
        var config = new GrpcServiceConfig
        {
            ServiceName = "Credential",
            ExtensionPath = Path.Combine("ext", "AzureMcp.Ext.Credential"),
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
            var endpointUrl = await _serviceHost.StartServiceAsync(cancellationToken);
            credential = new GrpcTokenCredential(endpointUrl, CreateLogger<GrpcTokenCredential>(), tenantId);
            credentialsCache[cacheKey] = credential;
            _logger.LogDebug("Created gRPC credential for tenant: {TenantId}", tenantId ?? "default");
        }
        return credential;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var credential in credentialsCache.Values)
            {
                credential.Dispose();
            }
            credentialsCache.Clear();
            _serviceHost.Dispose();
            _disposed = true;
        }
    }

    private ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();
}
