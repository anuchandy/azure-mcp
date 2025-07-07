// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.LocalServiceClient.CosmosDB.Grpc;
using AzureMcp.LocalServiceClient.Arm;
using AzureMcp.LocalServiceClient.Identity;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace AzureMcp.LocalServiceClient.CosmosDB;

/// <summary>
/// Service for managing Cosmos DB operations via gRPC.
/// </summary>
public sealed class CosmosDBServiceClient : ICosmosDBServiceClient, IDisposable
{
    private const string LocalServiceName = "AzureMcp.LocalService.CosmosDB";
    private const string CosmosDBLocalServiceConnectError = "CosmosDBLocalServiceConnectError";
    
    private readonly GrpcServiceHost _cosmosDBServiceHost;
    private readonly IServiceClient _identityService;
    private readonly IServiceClient _armService;
    private readonly ILogger<CosmosDBServiceClient> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Lazy<Task<string>> _initServiceTask;
    private GrpcChannel? _channel;
    private CosmosDBService.CosmosDBServiceClient? _client;
    private bool _disposed;

    public CosmosDBServiceClient(ILoggerFactory loggerFactory, IIdentityServiceClient identityServiceClient, IArmServiceClient armServiceClient)
        : this(loggerFactory, identityServiceClient, armServiceClient, IServiceClient.CreateDefaultServiceHost(loggerFactory, LocalServiceName, Path.Combine("localservices", LocalServiceName)))
    {
    }

    public CosmosDBServiceClient(ILoggerFactory loggerFactory, IIdentityServiceClient identityServiceClient, IArmServiceClient armServiceClient, GrpcServiceHost cosmosDBServiceHost)
    {
        _loggerFactory = ValidateNotNull(loggerFactory, nameof(loggerFactory));
        _identityService = ValidateNotNull(identityServiceClient, nameof(identityServiceClient));
        _armService = ValidateNotNull(armServiceClient, nameof(armServiceClient));
        _cosmosDBServiceHost = ValidateNotNull(cosmosDBServiceHost, nameof(cosmosDBServiceHost));
        _logger = CreateLogger<CosmosDBServiceClient>();

        _initServiceTask = new Lazy<Task<string>>(async () =>
        {
            var logInit = !_cosmosDBServiceHost.IsRunning;
            if (logInit)
            {
                _logger.LogDebug("Starting {LocalServiceName}", LocalServiceName);
            }
            var identityEndpoint = await _identityService.EnsureServiceStartedAsync();
            var armEndpoint = await _armService.EnsureServiceStartedAsync();
            var envVars = new Dictionary<string, string>
            {
                [LocalServiceEnvVars.CosmosDB.IdentityServiceEndpoint] = identityEndpoint,
                [LocalServiceEnvVars.CosmosDB.ArmServiceEndpoint] = armEndpoint
            };
            var endpoint = await _cosmosDBServiceHost.StartServiceAsync(envVars);
            if (logInit)
            {
                _logger.LogInformation("{LocalServiceName} initialized at {Endpoint} with Identity service at {IdentityEndpoint} and ARM service at {ArmEndpoint}", 
                    LocalServiceName, endpoint, identityEndpoint, armEndpoint);
            }
            return endpoint;
        });
    }

    public async Task<string> EnsureServiceStartedAsync(CancellationToken cancellationToken = default)
    {
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        return await _initServiceTask.Value.WaitAsync(combined.Token);
    }

    public async Task<List<string>> ListDatabasesAsync(
        string accountName,
        string subscriptionId,
        string? authMethod = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        ValidateStringArgument(accountName, nameof(accountName));
        ValidateStringArgument(subscriptionId, nameof(subscriptionId));

        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);
            var request = new ListDatabasesRequest
            {
                AccountName = accountName,
                SubscriptionId = subscriptionId,
                AuthMethod = authMethod ?? string.Empty,
                TenantId = tenantId ?? string.Empty
            };
            var response = await _client!.ListDatabasesAsync(request, cancellationToken: cancellationToken);
            
            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("ListDatabases", GetErrorMessage(response.ErrorMessage));
            }
            
            return response.DatabaseNames.ToList();
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("ListDatabases", CosmosDBLocalServiceConnectError, ex);
        }
    }

    public async Task<List<string>> ListContainersAsync(
        string accountName,
        string databaseName,
        string subscriptionId,
        string? authMethod = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        ValidateStringArgument(accountName, nameof(accountName));
        ValidateStringArgument(databaseName, nameof(databaseName));
        ValidateStringArgument(subscriptionId, nameof(subscriptionId));

        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);
            var request = new ListContainersRequest
            {
                AccountName = accountName,
                DatabaseName = databaseName,
                SubscriptionId = subscriptionId,
                AuthMethod = authMethod ?? string.Empty,
                TenantId = tenantId ?? string.Empty
            };
            var response = await _client!.ListContainersAsync(request, cancellationToken: cancellationToken);
            
            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("ListContainers", GetErrorMessage(response.ErrorMessage));
            }
            
            return response.ContainerNames.ToList();
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("ListContainers", CosmosDBLocalServiceConnectError, ex);
        }
    }

    public async Task<List<string>> QueryItemsAsync(
        string accountName,
        string databaseName,
        string containerName,
        string query,
        string subscriptionId,
        string? authMethod = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        ValidateStringArgument(accountName, nameof(accountName));
        ValidateStringArgument(databaseName, nameof(databaseName));
        ValidateStringArgument(containerName, nameof(containerName));
        ValidateStringArgument(query, nameof(query));
        ValidateStringArgument(subscriptionId, nameof(subscriptionId));

        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);
            var request = new QueryItemsRequest
            {
                AccountName = accountName,
                DatabaseName = databaseName,
                ContainerName = containerName,
                Query = query,
                SubscriptionId = subscriptionId,
                AuthMethod = authMethod ?? string.Empty,
                TenantId = tenantId ?? string.Empty
            };
            
            var results = new List<string>();
            var call = _client!.QueryItems(request, cancellationToken: cancellationToken);
            
            while (await call.ResponseStream.MoveNext(cancellationToken))
            {
                var response = call.ResponseStream.Current;
                
                if (!response.IsSuccess)
                {
                    throw new LocalServiceCallException("QueryItems", GetErrorMessage(response.ErrorMessage));
                }
                
                if (response.IsLast)
                {
                    break;
                }
                
                if (!string.IsNullOrEmpty(response.ItemJson))
                {
                    results.Add(response.ItemJson);
                }
            }
            
            return results;
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("QueryItems", CosmosDBLocalServiceConnectError, ex);
        }
    }

    private void EnsureClient(string endpoint)
    {
        if (_client != null)
        {
            return;
        }

        _channel = GrpcChannel.ForAddress(endpoint);
        _client = new CosmosDBService.CosmosDBServiceClient(_channel);
    }

    private ILogger<T> CreateLogger<T>()
    {
        return _loggerFactory.CreateLogger<T>();
    }

    private static string GetErrorMessage(string? responseErrorMessage)
    {
        return string.IsNullOrEmpty(responseErrorMessage) ? "Unknown error" : responseErrorMessage;
    }

    private static void ValidateStringArgument(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} cannot be null or empty.", parameterName);
        }
    }

    private static T ValidateNotNull<T>(T? value, string parameterName) where T : class
    {
        return value ?? throw new ArgumentNullException(parameterName);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _channel?.Dispose();
        _cosmosDBServiceHost?.Dispose();
        _disposed = true;
    }
}
