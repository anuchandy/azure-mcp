// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.Extensions.Logging;

namespace AzureMcp.LocalServiceClient.Identity;

/// <summary>
/// Service for managing credential acquisition with gRPC.
/// </summary>
public sealed class IdentityServiceClient : IIdentityServiceClient, IDisposable
{
    private const string LocalServiceName = "AzureMcp.LocalService.Identity";
    
    private readonly GrpcServiceHost _identityServiceHost;
    private readonly ILogger<IdentityServiceClient> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<string, TokenCredential> credentialsCache = new();
    private readonly Lazy<Task<string>> _initServiceTask;
    private bool _disposed;

    public IdentityServiceClient(ILoggerFactory loggerFactory)
        : this(loggerFactory, CreateDefaultServiceHost(loggerFactory))
    {
    }

    public IdentityServiceClient(ILoggerFactory loggerFactory, GrpcServiceHost identityServiceHost)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _identityServiceHost = identityServiceHost ?? throw new ArgumentNullException(nameof(identityServiceHost));
        _logger = CreateLogger<IdentityServiceClient>();

        _initServiceTask = new Lazy<Task<string>>(async () =>
        {
            var logInit = !_identityServiceHost.IsRunning;
            if (logInit)
            {
                _logger.LogDebug("Starting {LocalServiceName}", LocalServiceName);
            }
            var endpoint = await _identityServiceHost.StartServiceAsync();
            if (logInit)
            {
                _logger.LogInformation("{LocalServiceName} initialized at {Endpoint}", LocalServiceName, endpoint);
            }
            return endpoint;
        });
    }

    public async Task<string> EnsureServiceStartedAsync(CancellationToken cancellationToken = default)
    {
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        return await _initServiceTask.Value.WaitAsync(combined.Token);
    }

    public async Task<TokenCredential> GetCredentialAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        var cacheKey = tenantId ?? string.Empty;
        if (!credentialsCache.TryGetValue(cacheKey, out var credential))
        {
            credential = new IdentityCredential(endpoint, CreateLogger<IdentityCredential>());
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
            _identityServiceHost.Dispose();
            _disposed = true;
        }
    }

    private ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();

    private static GrpcServiceHost CreateDefaultServiceHost(ILoggerFactory loggerFactory)
    {
        var config = new GrpcServiceConfig
        {
            ServiceName = "Identity",
            ExtensionPath = "/Users/anuchandy/code/azure-mcp/localservices/AzureMcp.LocalService.Identity/bin/Debug/net9.0/", // Path.Combine("localservices", LocalServiceName),
            ExecutableNames = new[]
            {
                $"{LocalServiceName}.exe",
                LocalServiceName
            },
            HealthEndpoint = "/ishealthy",
            StartupTimeoutSeconds = 30
        };
        return new GrpcServiceHost(loggerFactory.CreateLogger<GrpcServiceHost>(), config);
    }
}
