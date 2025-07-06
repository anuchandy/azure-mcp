// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Areas.Authorization.Models;
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
    private const string ArmLocalServiceConnectError = "ARMLocalServiceConnectError";
    
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
            
            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("GetIdentityServiceStatus", GetErrorMessage(response.ErrorMessage));
            }
            
            return new IdentityServiceStatusResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage,
                Details = string.IsNullOrEmpty(response.Details) ? null : response.Details
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("GetIdentityServiceStatus", ArmLocalServiceConnectError, ex);
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
            
            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("ListSubscriptions", GetErrorMessage(response.ErrorMessage));
            }
            
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
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("ListSubscriptions", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<List<string>> GetStorageAccountsAsync(
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

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("GetStorageAccounts", GetErrorMessage(response.ErrorMessage));
            }

            return response.StorageAccounts.ToList();
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("GetStorageAccounts", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<string> GetStorageAccountKeysAsync(
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

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("GetStorageAccountKeys", GetErrorMessage(response.ErrorMessage));
            }

            var firstKey = response.Keys.FirstOrDefault();
            if (firstKey == null)
            {
                throw new LocalServiceCallException("GetStorageAccountKeys", $"No keys found for storage account '{accountName}'");
            }

            return firstKey.KeyValue;
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call ARM LocalService for storage account keys");
            throw new LocalServiceCallException("GetStorageAccountKeys", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<string> GetStorageAccountConnectionStringAsync(
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

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("GetStorageAccountConnectionString", GetErrorMessage(response.ErrorMessage));
            }

            if (string.IsNullOrEmpty(response.ConnectionString))
            {
                throw new LocalServiceCallException("GetStorageAccountConnectionString", $"No connection string found for storage account '{accountName}'");
            }

            return response.ConnectionString;
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("GetStorageAccountConnectionString", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<List<string>> GetCosmosAccountsAsync(
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

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("GetCosmosAccounts", GetErrorMessage(response.ErrorMessage));
            }

            return response.CosmosAccounts.ToList();
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call ARM LocalService for Cosmos DB accounts");
            throw new LocalServiceCallException("GetCosmosAccounts", ArmLocalServiceConnectError, ex);
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

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("GetCosmosAccount", GetErrorMessage(response.ErrorMessage));
            }

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
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("GetCosmosAccount", ArmLocalServiceConnectError, ex);
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

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("GetAppConfigAccounts", GetErrorMessage(response.ErrorMessage));
            }

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
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("GetAppConfigAccounts", ArmLocalServiceConnectError, ex);
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

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("GetAppConfigAccountEndpoint", GetErrorMessage(response.ErrorMessage));
            }

            return new GetAppConfigAccountEndpointResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage,
                Endpoint = string.IsNullOrEmpty(response.Endpoint) ? null : response.Endpoint
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("GetAppConfigAccountEndpoint", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<GetKustoClustersResult> GetKustoClustersAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new GetKustoClustersRequest
            {
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.GetKustoClustersAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("GetKustoClusters", GetErrorMessage(response.ErrorMessage));
            }

            return new GetKustoClustersResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage,
                KustoClusters = response.KustoClusters.ToArray()
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("GetKustoClusters", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<GetKustoClusterResult> GetKustoClusterAsync(
        string clusterName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new GetKustoClusterRequest
            {
                ClusterName = clusterName,
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.GetKustoClusterAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("GetKustoCluster", GetErrorMessage(response.ErrorMessage));
            }

            return new GetKustoClusterResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage,
                Cluster = response.Cluster == null ? null : new KustoClusterData
                {
                    ClusterName = response.Cluster.ClusterName,
                    ClusterUri = response.Cluster.ClusterUri,
                    Location = response.Cluster.Location,
                    ResourceGroupName = response.Cluster.ResourceGroupName,
                    SubscriptionId = response.Cluster.SubscriptionId,
                    Sku = response.Cluster.Sku,
                    Zones = response.Cluster.Zones,
                    Identity = response.Cluster.Identity,
                    ETag = response.Cluster.Etag,
                    State = response.Cluster.State,
                    ProvisioningState = response.Cluster.ProvisioningState,
                    DataIngestionUri = response.Cluster.DataIngestionUri,
                    StateReason = response.Cluster.StateReason,
                    IsStreamingIngestEnabled = response.Cluster.IsStreamingIngestEnabled,
                    EngineType = response.Cluster.EngineType,
                    IsAutoStopEnabled = response.Cluster.IsAutoStopEnabled
                }
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call ARM LocalService for Kusto cluster");
            throw new LocalServiceCallException("GetKustoCluster", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<GetResourceGroupsResult> GetResourceGroupsAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new GetResourceGroupsRequest
            {
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.GetResourceGroupsAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("GetResourceGroups", GetErrorMessage(response.ErrorMessage));
            }

            return new GetResourceGroupsResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage,
                ResourceGroups = response.ResourceGroups.Select(rg => new ResourceGroupData
                {
                    Name = rg.Name,
                    Id = rg.Id,
                    Location = rg.Location
                }).ToList()
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("GetResourceGroups", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<GetResourceGroupResult> GetResourceGroupAsync(
        string resourceGroupName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new GetResourceGroupRequest
            {
                ResourceGroupName = resourceGroupName,
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.GetResourceGroupAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("GetResourceGroup", GetErrorMessage(response.ErrorMessage));
            }

            return new GetResourceGroupResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage,
                ResourceGroup = response.ResourceGroup == null ? null : new ResourceGroupData
                {
                    Name = response.ResourceGroup.Name,
                    Id = response.ResourceGroup.Id,
                    Location = response.ResourceGroup.Location
                }
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("GetResourceGroup", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<ListRedisCachesResult> ListRedisCachesAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new ListRedisCachesRequest
            {
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.ListRedisCachesAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("ListRedisCaches", GetErrorMessage(response.ErrorMessage));
            }

            return new ListRedisCachesResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = response.ErrorMessage,
                RedisCaches = response.RedisCaches.Select(c => new RedisCacheData
                {
                    Name = c.Name,
                    ResourceGroupName = c.ResourceGroupName,
                    SubscriptionId = c.SubscriptionId,
                    Location = c.Location,
                    Sku = c.Sku,
                    ProvisioningState = c.ProvisioningState,
                    RedisVersion = c.RedisVersion,
                    HostName = c.HostName,
                    SslPort = c.SslPort,
                    Port = c.Port,
                    ShardCount = c.ShardCount,
                    SubnetId = c.SubnetId,
                    PublicNetworkAccess = c.PublicNetworkAccess,
                    EnableNonSslPort = c.EnableNonSslPort,
                    IsAccessKeyAuthenticationDisabled = c.IsAccessKeyAuthenticationDisabled,
                    LinkedServers = c.LinkedServers.ToList(),
                    MinimumTlsVersion = c.MinimumTlsVersion,
                    PrivateEndpointConnections = c.PrivateEndpointConnections.ToList(),
                    ReplicasPerPrimary = c.ReplicasPerPrimary,
                    UpdateChannel = c.UpdateChannel,
                    ZonalAllocationPolicy = c.ZonalAllocationPolicy,
                    Zones = c.Zones.ToList(),
                    Configuration = c.Configuration != null ? new RedisCacheConfigurationData
                    {
                        IsRdbBackupEnabled = c.Configuration.IsRdbBackupEnabled,
                        RdbBackupFrequency = c.Configuration.RdbBackupFrequency,
                        RdbBackupMaxSnapshotCount = c.Configuration.RdbBackupMaxSnapshotCount,
                        IsAofBackupEnabled = c.Configuration.IsAofBackupEnabled,
                        MaxFragmentationMemoryReserved = c.Configuration.MaxFragmentationMemoryReserved,
                        MaxMemoryPolicy = c.Configuration.MaxMemoryPolicy,
                        MaxMemoryReserved = c.Configuration.MaxMemoryReserved,
                        MaxMemoryDelta = c.Configuration.MaxMemoryDelta,
                        MaxClients = c.Configuration.MaxClients,
                        NotifyKeyspaceEvents = c.Configuration.NotifyKeyspaceEvents,
                        PreferredDataArchiveAuthMethod = c.Configuration.PreferredDataArchiveAuthMethod,
                        PreferredDataPersistenceAuthMethod = c.Configuration.PreferredDataPersistenceAuthMethod,
                        ZonalConfiguration = c.Configuration.ZonalConfiguration,
                        AuthNotRequired = c.Configuration.AuthNotRequired
                    } : null,
                    Identity = c.Identity != null ? new ManagedIdentityData
                    {
                        SystemAssignedIdentity = c.Identity.SystemAssignedIdentity != null ? new SystemAssignedIdentityData
                        {
                            Enabled = c.Identity.SystemAssignedIdentity.Enabled,
                            TenantId = c.Identity.SystemAssignedIdentity.TenantId,
                            PrincipalId = c.Identity.SystemAssignedIdentity.PrincipalId
                        } : null,
                        UserAssignedIdentities = c.Identity.UserAssignedIdentities.Select(u => new UserAssignedIdentityData
                        {
                            ClientId = u.ClientId,
                            PrincipalId = u.PrincipalId
                        }).ToList()
                    } : null,
                    Tags = c.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                }).ToList()
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("ListRedisCaches", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<ListRedisAccessPolicyAssignmentsResult> ListRedisAccessPolicyAssignmentsAsync(
        string cacheName,
        string resourceGroupName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new ListRedisAccessPolicyAssignmentsRequest
            {
                CacheName = cacheName,
                ResourceGroupName = resourceGroupName,
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.ListRedisAccessPolicyAssignmentsAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("ListRedisAccessPolicyAssignments", GetErrorMessage(response.ErrorMessage));
            }

            return new ListRedisAccessPolicyAssignmentsResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = response.ErrorMessage,
                RedisAccessPolicyAssignments = response.RedisAccessPolicyAssignments.Select(a => new RedisAccessPolicyAssignmentData
                {
                    AccessPolicyName = a.AccessPolicyName,
                    IdentityName = a.IdentityName,
                    ProvisioningState = a.ProvisioningState
                }).ToList()
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("ListRedisAccessPolicyAssignments", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<ListRedisClustersResult> ListRedisClustersAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new ListRedisClustersRequest
            {
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.ListRedisClustersAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("ListRedisClusters", GetErrorMessage(response.ErrorMessage));
            }

            return new ListRedisClustersResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = response.ErrorMessage,
                RedisClusters = response.RedisClusters.Select(c => new RedisClusterData
                {
                    Name = c.Name,
                    SubscriptionId = c.SubscriptionId,
                    ResourceGroupName = c.ResourceGroupName,
                    Location = c.Location,
                    Sku = c.Sku,
                    ProvisioningState = c.ProvisioningState,
                    ResourceState = c.ResourceState,
                    RedisVersion = c.RedisVersion,
                    HostName = c.HostName,
                    MinimumTlsVersion = c.MinimumTlsVersion,
                    PrivateEndpointConnections = c.PrivateEndpointConnections.ToList(),
                    Zones = c.Zones.ToList(),
                    Identity = c.Identity != null ? new ManagedIdentityData
                    {
                        SystemAssignedIdentity = c.Identity.SystemAssignedIdentity != null ? new SystemAssignedIdentityData
                        {
                            Enabled = c.Identity.SystemAssignedIdentity.Enabled,
                            TenantId = c.Identity.SystemAssignedIdentity.TenantId,
                            PrincipalId = c.Identity.SystemAssignedIdentity.PrincipalId
                        } : null,
                        UserAssignedIdentities = c.Identity.UserAssignedIdentities.Select(u => new UserAssignedIdentityData
                        {
                            ClientId = u.ClientId,
                            PrincipalId = u.PrincipalId
                        }).ToList()
                    } : null,
                    Tags = c.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                }).ToList()
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("ListRedisClusters", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<ListRedisDatabasesResult> ListRedisDatabasesAsync(
        string clusterName,
        string resourceGroupName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new ListRedisDatabasesRequest
            {
                ClusterName = clusterName,
                ResourceGroupName = resourceGroupName,
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.ListRedisDatabasesAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("ListRedisDatabases", GetErrorMessage(response.ErrorMessage));
            }

            return new ListRedisDatabasesResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = response.ErrorMessage,
                RedisDatabases = response.RedisDatabases.Select(d => new RedisDatabaseData
                {
                    Name = d.Name,
                    ClusterName = d.ClusterName,
                    ResourceGroupName = d.ResourceGroupName,
                    SubscriptionId = d.SubscriptionId,
                    ClientProtocol = d.ClientProtocol,
                    Port = d.Port,
                    ProvisioningState = d.ProvisioningState,
                    ResourceState = d.ResourceState,
                    ClusteringPolicy = d.ClusteringPolicy,
                    EvictionPolicy = d.EvictionPolicy,
                    IsAofEnabled = d.IsAofEnabled,
                    IsRdbEnabled = d.IsRdbEnabled,
                    AofFrequency = d.AofFrequency,
                    RdbFrequency = d.RdbFrequency,
                    Modules = d.Modules.Select(m => new RedisModuleData
                    {
                        Name = m.Name,
                        Args = m.Args,
                        Version = m.Version
                    }).ToList(),
                    GeoReplicationGroupNickname = d.GeoReplicationGroupNickname,
                    GeoReplicationLinkedDatabases = d.GeoReplicationLinkedDatabases.ToList()
                }).ToList()
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("ListRedisDatabases", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<ListPostgreSqlServersResult> ListPostgreSqlServersAsync(
        string subscriptionId,
        string resourceGroupName,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new ListPostgreSqlServersRequest
            {
                SubscriptionId = subscriptionId,
                ResourceGroupName = resourceGroupName,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.ListPostgreSqlServersAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("ListPostgreSqlServers", GetErrorMessage(response.ErrorMessage));
            }

            return new ListPostgreSqlServersResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = response.ErrorMessage,
                ServerNames = response.ServerNames.ToList()
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("ListPostgreSqlServers", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<GetPostgreSqlServerConfigResult> GetPostgreSqlServerConfigAsync(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new GetPostgreSqlServerConfigRequest
            {
                SubscriptionId = subscriptionId,
                ResourceGroupName = resourceGroupName,
                ServerName = serverName,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.GetPostgreSqlServerConfigAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("GetPostgreSqlServerConfig", GetErrorMessage(response.ErrorMessage));
            }

            return new GetPostgreSqlServerConfigResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = response.ErrorMessage,
                ServerConfig = response.ServerConfig != null ? new PostgreSqlServerConfigData
                {
                    Name = response.ServerConfig.Name,
                    Location = response.ServerConfig.Location,
                    Version = response.ServerConfig.Version,
                    SkuName = response.ServerConfig.SkuName,
                    StorageSizeGb = response.ServerConfig.StorageSizeGb,
                    BackupRetentionDays = response.ServerConfig.BackupRetentionDays,
                    GeoRedundantBackup = response.ServerConfig.GeoRedundantBackup
                } : null
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("GetPostgreSqlServerConfig", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<GetPostgreSqlServerParameterResult> GetPostgreSqlServerParameterAsync(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string parameterName,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new GetPostgreSqlServerParameterRequest
            {
                SubscriptionId = subscriptionId,
                ResourceGroupName = resourceGroupName,
                ServerName = serverName,
                ParameterName = parameterName,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.GetPostgreSqlServerParameterAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("GetPostgreSqlServerParameter", GetErrorMessage(response.ErrorMessage));
            }

            return new GetPostgreSqlServerParameterResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = response.ErrorMessage,
                ParameterValue = response.ParameterValue
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("GetPostgreSqlServerParameter", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<SetPostgreSqlServerParameterResult> SetPostgreSqlServerParameterAsync(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string parameterName,
        string parameterValue,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new SetPostgreSqlServerParameterRequest
            {
                SubscriptionId = subscriptionId,
                ResourceGroupName = resourceGroupName,
                ServerName = serverName,
                ParameterName = parameterName,
                ParameterValue = parameterValue,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.SetPostgreSqlServerParameterAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("SetPostgreSqlServerParameter", GetErrorMessage(response.ErrorMessage));
            }

            return new SetPostgreSqlServerParameterResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = response.ErrorMessage,
                Message = response.Message
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("SetPostgreSqlServerParameter", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<ListSearchServicesResult> ListSearchServicesAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new ListSearchServicesRequest
            {
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.ListSearchServicesAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("ListSearchServices", GetErrorMessage(response.ErrorMessage));
            }

            return new ListSearchServicesResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = response.ErrorMessage,
                ServiceNames = response.ServiceNames.ToList()
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("ListSearchServices", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<ListMonitoredDatadogResourcesResult> ListMonitoredDatadogResourcesAsync(
        string subscriptionId,
        string resourceGroupName,
        string datadogResourceName,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new ListMonitoredDatadogResourcesRequest
            {
                SubscriptionId = subscriptionId,
                ResourceGroupName = resourceGroupName,
                DatadogResourceName = datadogResourceName,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.ListMonitoredDatadogResourcesAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("ListMonitoredDatadogResources", GetErrorMessage(response.ErrorMessage));
            }

            return new ListMonitoredDatadogResourcesResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = response.ErrorMessage,
                MonitoredResourceNames = response.MonitoredResourceNames.ToList()
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("ListMonitoredDatadogResources", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<ListRoleAssignmentsResult> ListRoleAssignmentsAsync(
        string scope,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);
            var request = new ListRoleAssignmentsRequest
            {
                Scope = scope,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.ListRoleAssignmentsAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("ListRoleAssignments", GetErrorMessage(response.ErrorMessage));
            }

            var roleAssignments = response.RoleAssignments.Select(ra => new AzureMcp.Areas.Authorization.Models.RoleAssignment
            {
                Id = ra.Id,
                Name = ra.Name,
                RoleDefinitionId = ra.RoleDefinitionId,
                Scope = ra.Scope,
                PrincipalId = string.IsNullOrEmpty(ra.PrincipalId) ? null : Guid.Parse(ra.PrincipalId),
                PrincipalType = ra.PrincipalType,
                Description = ra.Description,
                DelegatedManagedIdentityResourceId = ra.DelegatedManagedIdentityResourceId,
                Condition = ra.Condition
            }).ToList();

            return new ListRoleAssignmentsResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? string.Empty : response.ErrorMessage,
                RoleAssignments = roleAssignments
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("ListRoleAssignments", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<ListMonitorWorkspacesResult> ListMonitorWorkspacesAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new ListMonitorWorkspacesRequest
            {
                SubscriptionId = subscriptionId,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.ListMonitorWorkspacesAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("ListMonitorWorkspaces", GetErrorMessage(response.ErrorMessage));
            }

            return new ListMonitorWorkspacesResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = response.ErrorMessage,
                Workspaces = response.Workspaces.Select(w => new MonitorWorkspaceInfo
                {
                    Name = w.Name,
                    CustomerId = w.CustomerId,
                    ArmId = w.ArmId
                }).ToList()
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("ListMonitorWorkspaces", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<ListMonitorTablesResult> ListMonitorTablesAsync(
        string subscriptionId,
        string resourceGroupName,
        string workspaceName,
        string? tableType = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new ListMonitorTablesRequest
            {
                SubscriptionId = subscriptionId,
                ResourceGroupName = resourceGroupName,
                WorkspaceName = workspaceName,
                TableType = tableType ?? string.Empty,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.ListMonitorTablesAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("ListMonitorTables", GetErrorMessage(response.ErrorMessage));
            }

            return new ListMonitorTablesResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = response.ErrorMessage,
                TableNames = response.TableNames.ToList()
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("ListMonitorTables", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<ListMonitorTableTypesResult> ListMonitorTableTypesAsync(
        string subscriptionId,
        string resourceGroupName,
        string workspaceName,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new ListMonitorTableTypesRequest
            {
                SubscriptionId = subscriptionId,
                ResourceGroupName = resourceGroupName,
                WorkspaceName = workspaceName,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.ListMonitorTableTypesAsync(request, cancellationToken: cancellationToken);


            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("ListMonitorTableTypes", GetErrorMessage(response.ErrorMessage));
            }

            return new ListMonitorTableTypesResult
            {
                IsSuccess = response.IsSuccess,
                ErrorMessage = response.ErrorMessage,
                TableTypes = response.TableTypes.ToList()
            };
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("ListMonitorTableTypes", ArmLocalServiceConnectError, ex);
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

    private static string GetErrorMessage(string? responseErrorMessage)
    {
        return string.IsNullOrEmpty(responseErrorMessage) ? "Unknown error" : responseErrorMessage;
    }
}
