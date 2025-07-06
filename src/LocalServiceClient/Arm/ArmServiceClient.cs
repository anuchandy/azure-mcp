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
    
    private readonly GrpcServiceHost _armServiceHost;
    private readonly IServiceClient _identityService;
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

    public ArmServiceClient(ILoggerFactory loggerFactory, IIdentityServiceClient identityServiceClient, GrpcServiceHost armServiceHost)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _identityService = identityServiceClient ?? throw new ArgumentNullException(nameof(identityServiceClient));
        _armServiceHost = armServiceHost ?? throw new ArgumentNullException(nameof(armServiceHost));
        _logger = CreateLogger<ArmServiceClient>();

        _initServiceTask = new Lazy<Task<string>>(async () =>
        {
            var logInit = !_armServiceHost.IsRunning;
            if (logInit)
            {
                _logger.LogDebug("Starting {LocalServiceName}", LocalServiceName);
            }
            var identityEndpoint = await _identityService.EnsureServiceStartedAsync();
            var envVars = new Dictionary<string, string>
            {
                ["AzureMcp__LocalService__Arm__IdentityServiceEndpoint"] = identityEndpoint
            };
            var endpoint = await _armServiceHost.StartServiceAsync(envVars);
            if (logInit)
            {
                _logger.LogInformation("{LocalServiceName} initialized at {Endpoint} with Identity service at {IdentityEndpoint}", 
                    LocalServiceName, endpoint, identityEndpoint);
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

    public async Task<ListSubscriptionsResult> ListSubscriptionsAsync(
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);
            var request = new ListSubscriptionsRequest
            {
                TenantId = tenantId ?? string.Empty
            };
            
            var response = await _client!.ListSubscriptionsAsync(request, cancellationToken: cancellationToken);
            
            var subscriptions = response.Subscriptions.Select(s => new SubscriptionData
            {
                SubscriptionId = s.SubscriptionId,
                DisplayName = s.DisplayName,
                TenantId = s.TenantId,
                State = s.State
            }).ToList();

            return new ListSubscriptionsResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage,
                Subscriptions = subscriptions
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call ARM LocalService for subscription list");
            return new ListSubscriptionsResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Subscriptions = Array.Empty<SubscriptionData>()
            };
        }
    }

    public async Task<GetStorageAccountsResult> GetStorageAccountsAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);
            var request = new GetStorageAccountsRequest
            {
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.GetStorageAccountsAsync(request, cancellationToken: cancellationToken);

            return new GetStorageAccountsResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage,
                StorageAccounts = response.StorageAccounts.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call ARM LocalService for storage accounts");
            return new GetStorageAccountsResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                StorageAccounts = Array.Empty<string>()
            };
        }
    }

    public async Task<GetStorageAccountKeysResult> GetStorageAccountKeysAsync(
        string accountName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);
            var request = new GetStorageAccountKeysRequest
            {
                AccountName = accountName,
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.GetStorageAccountKeysAsync(request, cancellationToken: cancellationToken);

            var keys = response.Keys.Select(k => new StorageAccountKeyData
            {
                KeyName = k.KeyName,
                KeyValue = k.KeyValue,
                Permissions = k.Permissions
            }).ToList();

            return new GetStorageAccountKeysResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage,
                Keys = keys
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call ARM LocalService for storage account keys");
            return new GetStorageAccountKeysResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Keys = Array.Empty<StorageAccountKeyData>()
            };
        }
    }

    public async Task<GetStorageAccountConnectionStringResult> GetStorageAccountConnectionStringAsync(
        string accountName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);
            var request = new GetStorageAccountConnectionStringRequest
            {
                AccountName = accountName,
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.GetStorageAccountConnectionStringAsync(request, cancellationToken: cancellationToken);

            return new GetStorageAccountConnectionStringResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage,
                ConnectionString = string.IsNullOrEmpty(response.ConnectionString) ? null : response.ConnectionString
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call ARM LocalService for storage account connection string");
            return new GetStorageAccountConnectionStringResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ConnectionString = null
            };
        }
    }

    public async Task<GetCosmosAccountsResult> GetCosmosAccountsAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);
            var request = new GetCosmosAccountsRequest
            {
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.GetCosmosAccountsAsync(request, cancellationToken: cancellationToken);

            return new GetCosmosAccountsResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage,
                CosmosAccounts = response.CosmosAccounts.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call ARM LocalService for Cosmos DB accounts");
            return new GetCosmosAccountsResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                CosmosAccounts = Array.Empty<string>()
            };
        }
    }

    public async Task<GetCosmosAccountResult> GetCosmosAccountAsync(
        string accountName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);
            var request = new GetCosmosAccountRequest
            {
                AccountName = accountName,
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.GetCosmosAccountAsync(request, cancellationToken: cancellationToken);

            CosmosAccountData? accountData = null;
            if (response.Account != null)
            {
                accountData = new CosmosAccountData
                {
                    Name = response.Account.Name,
                    Id = response.Account.Id,
                    Location = response.Account.Location,
                    AccountType = response.Account.AccountType,
                    ResourceGroup = response.Account.ResourceGroup,
                    ProvisioningState = response.Account.ProvisioningState,
                    DocumentEndpoint = response.Account.DocumentEndpoint
                };
            }

            return new GetCosmosAccountResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage,
                Account = accountData
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call ARM LocalService for Cosmos DB account");
            return new GetCosmosAccountResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Account = null
            };
        }
    }

    public async Task<GetAppConfigAccountsResult> GetAppConfigAccountsAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);
            var request = new GetAppConfigAccountsRequest
            {
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.GetAppConfigAccountsAsync(request, cancellationToken: cancellationToken);

            var accounts = response.AppConfigAccounts.Select(a => new AppConfigAccountData
            {
                Name = a.Name,
                Location = a.Location,
                Endpoint = a.Endpoint,
                CreationDate = DateTimeOffset.FromUnixTimeSeconds(a.CreationDate).DateTime,
                PublicNetworkAccess = a.PublicNetworkAccess,
                Sku = string.IsNullOrEmpty(a.Sku) ? null : a.Sku,
                Tags = a.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                DisableLocalAuth = a.DisableLocalAuth,
                SoftDeleteRetentionInDays = a.SoftDeleteRetentionInDays,
                EnablePurgeProtection = a.EnablePurgeProtection,
                CreateMode = string.IsNullOrEmpty(a.CreateMode) ? null : a.CreateMode,
                ManagedIdentity = a.ManagedIdentity == null ? null : new ManagedIdentityData
                {
                    SystemAssignedIdentity = a.ManagedIdentity.SystemAssignedIdentity == null ? null : new SystemAssignedIdentityData
                    {
                        Enabled = a.ManagedIdentity.SystemAssignedIdentity.Enabled,
                        TenantId = string.IsNullOrEmpty(a.ManagedIdentity.SystemAssignedIdentity.TenantId) ? null : a.ManagedIdentity.SystemAssignedIdentity.TenantId,
                        PrincipalId = string.IsNullOrEmpty(a.ManagedIdentity.SystemAssignedIdentity.PrincipalId) ? null : a.ManagedIdentity.SystemAssignedIdentity.PrincipalId
                    },
                    UserAssignedIdentities = a.ManagedIdentity.UserAssignedIdentities.Select(u => new UserAssignedIdentityData
                    {
                        ClientId = string.IsNullOrEmpty(u.ClientId) ? null : u.ClientId,
                        PrincipalId = string.IsNullOrEmpty(u.PrincipalId) ? null : u.PrincipalId
                    }).ToArray()
                },
                Encryption = a.Encryption == null ? null : new EncryptionData
                {
                    KeyIdentifier = string.IsNullOrEmpty(a.Encryption.KeyIdentifier) ? null : a.Encryption.KeyIdentifier,
                    IdentityClientId = string.IsNullOrEmpty(a.Encryption.IdentityClientId) ? null : a.Encryption.IdentityClientId,
                    IsKeyVaultKeyIdentifierValid = a.Encryption.IsKeyVaultKeyIdentifierValid,
                    IsIdentityClientIdValid = a.Encryption.IsIdentityClientIdValid
                }
            }).ToList();

            return new GetAppConfigAccountsResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage,
                AppConfigAccounts = accounts
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call ARM LocalService for App Configuration accounts");
            return new GetAppConfigAccountsResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                AppConfigAccounts = Array.Empty<AppConfigAccountData>()
            };
        }
    }

    public async Task<GetAppConfigAccountEndpointResult> GetAppConfigAccountEndpointAsync(
        string accountName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);
            var request = new GetAppConfigAccountEndpointRequest
            {
                AccountName = accountName,
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.GetAppConfigAccountEndpointAsync(request, cancellationToken: cancellationToken);

            return new GetAppConfigAccountEndpointResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage,
                Endpoint = string.IsNullOrEmpty(response.Endpoint) ? null : response.Endpoint
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call ARM LocalService for App Configuration account endpoint");
            return new GetAppConfigAccountEndpointResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Endpoint = null
            };
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _channel?.Dispose();
            _armServiceHost.Dispose();
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
