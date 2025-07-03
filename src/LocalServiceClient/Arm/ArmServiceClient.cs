// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.LocalServiceClient.Identity;
using AzureMcp.LocalService.Arm.Grpc;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System.Net;

namespace AzureMcp.LocalServiceClient.Arm;

/// <summary>
/// Service for managing Azure Resource Manager operations via gRPC.
/// </summary>
public sealed class ArmServiceClient : IArmServiceClient, IDisposable
{
    private const string LocalServiceName = "AzureMcp.LocalService.Arm";
    
    private readonly GrpcServiceHost _serviceHost;
    private readonly IServiceClient _identityServiceClient;
    private readonly ILogger<ArmServiceClient> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Lazy<Task<string>> _initServiceTask;
    private GrpcChannel? _channel;
    private ArmService.ArmServiceClient? _client;
    private bool _disposed;

    public ArmServiceClient(ILoggerFactory loggerFactory, IIdentityServiceClient identityServiceClient)
        : this(loggerFactory, identityServiceClient, CreateDefaultServiceHost(loggerFactory))
    {
    }

    public ArmServiceClient(ILoggerFactory loggerFactory, IIdentityServiceClient identityServiceClient, GrpcServiceHost serviceHost)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _identityServiceClient = identityServiceClient ?? throw new ArgumentNullException(nameof(identityServiceClient));
        _serviceHost = serviceHost ?? throw new ArgumentNullException(nameof(serviceHost));
        _logger = CreateLogger<ArmServiceClient>();

        _initServiceTask = new Lazy<Task<string>>(async () =>
        {
            var logInit = !_serviceHost.IsRunning;
            if (logInit)
            {
                _logger.LogDebug("Starting {LocalServiceName}", LocalServiceName);
            }
            var identityEndpoint = await _identityServiceClient.EnsureServiceStartedAsync();
            var endpoint = await _serviceHost.StartServiceAsync();
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

    public async Task<IdentityServiceStatusResult> GetIdentityServiceStatusAsync(
        string? tenantId = null, 
        string[]? scopes = null, 
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);
            var request = new IdentityServiceStatusRequest
            {
                TenantId = tenantId ?? string.Empty
            };
            if (scopes != null)
            {
                request.Scopes.AddRange(scopes);
            }
            var response = await _client!.GetIdentityServiceStatusAsync(request, cancellationToken: cancellationToken);
            return new IdentityServiceStatusResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage,
                Details = string.IsNullOrEmpty(response.Details) ? null : response.Details
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call ARM LocalService for Identity service status check");
            return new IdentityServiceStatusResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Details = "Failed to communicate with ARM LocalService"
            };
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _channel?.Dispose();
            _serviceHost.Dispose();
            _disposed = true;
        }
    }

    private void EnsureClient(string endpoint)
    {
        if (_client == null)
        {
            var channelOptions = new GrpcChannelOptions
            {
                HttpClient = new HttpClient(new HttpClientHandler())
                {
                    DefaultRequestVersion = HttpVersion.Version20,
                    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
                }
            };
            _channel = GrpcChannel.ForAddress(endpoint, channelOptions);
            _client = new ArmService.ArmServiceClient(_channel);
        }
    }

    private ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();

    private static GrpcServiceHost CreateDefaultServiceHost(ILoggerFactory loggerFactory)
    {
        var config = new GrpcServiceConfig
        {
            ServiceName = "Arm",
            ExtensionPath = Path.Combine("localservices", LocalServiceName),
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
