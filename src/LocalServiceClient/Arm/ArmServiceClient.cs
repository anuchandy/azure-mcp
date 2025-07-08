// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Areas.AppConfig.Models;
using AzureMcp.Areas.Authorization.Models;
using AzureMcp.Areas.Redis.Models.CacheForRedis;
using AzureMcp.Areas.Redis.Models.ManagedRedis;
using AzureMcp.Commands.Kusto;
using AzureMcp.LocalServiceClient.Identity;
using AzureMcp.LocalServiceClient.Arm.Grpc;
using AzureMcp.Models.Identity;
using AzureMcp.Models.ResourceGroup;
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
        : this(loggerFactory, identityServiceClient, IServiceClient.CreateDefaultServiceHost(loggerFactory, LocalServiceName))
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
                [LocalServiceEnvVars.Arm.IdentityServiceEndpoint] = identityEndpoint
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

    public async Task<List<TenantData>> GetTenantsAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);
            var request = new GetTenantsRequest();
            var response = await _client!.GetTenantsAsync(request, cancellationToken: cancellationToken);
            
            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("GetTenants", GetErrorMessage(response.ErrorMessage));
            }
            
            return response.Tenants.Select(t => new TenantData
            {
                Id = t.Id,
                TenantId = t.TenantId,
                TenantCategory = t.TenantCategory,
                Country = t.Country,
                CountryCode = t.CountryCode,
                DisplayName = t.DisplayName,
                Domains = t.Domains.ToList(),
                DefaultDomain = t.DefaultDomain,
                TenantType = t.TenantType,
                TenantBrandingLogoUri = t.TenantBrandingLogoUri
            }).ToList();
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("GetTenants", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<List<SubscriptionData>> ListSubscriptionsAsync(
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
            
            return response.Subscriptions.Select(s => new SubscriptionData
            {
                SubscriptionId = s.SubscriptionId,
                DisplayName = s.DisplayName,
                TenantId = s.TenantId,
                State = s.State
            }).ToList();
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

    public async Task<CosmosAccountData> GetCosmosAccountAsync(
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

            if (response.Account == null)
            {
                throw new LocalServiceCallException("GetCosmosAccount", "No account data returned from service");
            }

            return new CosmosAccountData
            {
                Name = response.Account.Name,
                Id = response.Account.Id,
                Location = response.Account.Location,
                AccountType = response.Account.AccountType,
                ResourceGroup = response.Account.ResourceGroup,
                ProvisioningState = response.Account.ProvisioningState,
                DocumentEndpoint = response.Account.DocumentEndpoint,
                PrimaryMasterKey = response.Account.PrimaryMasterKey
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

    public async Task<List<AppConfigurationAccount>> GetAppConfigAccountsAsync(
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

            var accounts = response.AppConfigAccounts.Select(a => new AppConfigurationAccount
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
                ManagedIdentity = a.ManagedIdentity == null ? null : new AzureMcp.Models.Identity.ManagedIdentityInfo
                {
                    SystemAssignedIdentity = a.ManagedIdentity.SystemAssignedIdentity == null ? null : new AzureMcp.Models.Identity.SystemAssignedIdentityInfo
                    {
                        Enabled = a.ManagedIdentity.SystemAssignedIdentity.Enabled,
                        TenantId = string.IsNullOrEmpty(a.ManagedIdentity.SystemAssignedIdentity.TenantId) ? null : a.ManagedIdentity.SystemAssignedIdentity.TenantId,
                        PrincipalId = string.IsNullOrEmpty(a.ManagedIdentity.SystemAssignedIdentity.PrincipalId) ? null : a.ManagedIdentity.SystemAssignedIdentity.PrincipalId
                    },
                    UserAssignedIdentities = a.ManagedIdentity.UserAssignedIdentities.Select(u => new AzureMcp.Models.Identity.UserAssignedIdentityInfo
                    {
                        ClientId = string.IsNullOrEmpty(u.ClientId) ? null : u.ClientId,
                        PrincipalId = string.IsNullOrEmpty(u.PrincipalId) ? null : u.PrincipalId
                    }).ToArray()
                },
                Encryption = a.Encryption == null ? null : new AzureMcp.Areas.AppConfig.Models.EncryptionProperties
                {
                    KeyIdentifier = string.IsNullOrEmpty(a.Encryption.KeyIdentifier) ? null : a.Encryption.KeyIdentifier,
                    IdentityClientId = string.IsNullOrEmpty(a.Encryption.IdentityClientId) ? null : a.Encryption.IdentityClientId,
                    IsKeyVaultKeyIdentifierValid = a.Encryption.IsKeyVaultKeyIdentifierValid,
                    IsIdentityClientIdValid = a.Encryption.IsIdentityClientIdValid
                }
            }).ToList();

            return accounts;
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

    public async Task<string> GetAppConfigAccountEndpointAsync(
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

            if (string.IsNullOrEmpty(response.Endpoint))
            {
                throw new LocalServiceCallException("GetAppConfigAccountEndpoint", $"No endpoint found for App Configuration account '{accountName}'");
            }

            return response.Endpoint;
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

    public async Task<List<string>> GetKustoClustersAsync(
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

            return response.KustoClusters.ToList();
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

    public async Task<KustoClusterResourceProxy> GetKustoClusterAsync(
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

            if (response.Cluster == null)
            {
                throw new LocalServiceCallException("GetKustoCluster", $"Kusto cluster '{clusterName}' not found");
            }

            return new KustoClusterResourceProxy
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

    public async Task<List<ResourceGroupInfo>> GetResourceGroupsAsync(
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

            return response.ResourceGroups.Select(rg => new ResourceGroupInfo(
                rg.Name,
                rg.Id,
                rg.Location
            )).ToList();
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

    public async Task<ResourceGroupInfo> GetResourceGroupAsync(
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

            if (response.ResourceGroup == null)
            {
                throw new LocalServiceCallException("GetResourceGroup", $"Resource group '{resourceGroupName}' not found");
            }

            return new ResourceGroupInfo(
                response.ResourceGroup.Name,
                response.ResourceGroup.Id,
                response.ResourceGroup.Location
            );
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

    public async Task<List<Cache>> ListRedisCachesAsync(
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

            return response.RedisCaches.Select(c => new Cache
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
                LinkedServers = c.LinkedServers?.ToArray(),
                MinimumTlsVersion = c.MinimumTlsVersion,
                PrivateEndpointConnections = c.PrivateEndpointConnections?.ToArray(),
                ReplicasPerPrimary = c.ReplicasPerPrimary,
                UpdateChannel = c.UpdateChannel,
                ZonalAllocationPolicy = c.ZonalAllocationPolicy,
                Zones = c.Zones?.ToArray(),
                Configuration = c.Configuration != null ? new CacheConfiguration
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
                    AuthNotRequired = c.Configuration.AuthNotRequired,
                    StorageSubscriptionId = c.Configuration.StorageSubscriptionId,
                    IsEntraIDAuthEnabled = c.Configuration.IsEntraIdAuthEnabled
                } : null,
                Identity = c.Identity != null ? new AzureMcp.Models.Identity.ManagedIdentityInfo
                {
                    SystemAssignedIdentity = c.Identity.SystemAssignedIdentity != null ? new AzureMcp.Models.Identity.SystemAssignedIdentityInfo
                    {
                        Enabled = c.Identity.SystemAssignedIdentity.Enabled,
                        TenantId = c.Identity.SystemAssignedIdentity.TenantId,
                        PrincipalId = c.Identity.SystemAssignedIdentity.PrincipalId
                    } : null,
                    UserAssignedIdentities = c.Identity.UserAssignedIdentities?.Select(u => new AzureMcp.Models.Identity.UserAssignedIdentityInfo
                    {
                        ClientId = u.ClientId,
                        PrincipalId = u.PrincipalId
                    }).ToArray()
                } : null,
                Tags = c.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            }).ToList();
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

    public async Task<List<AccessPolicyAssignment>> ListRedisAccessPolicyAssignmentsAsync(
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

            return response.RedisAccessPolicyAssignments.Select(a => new AccessPolicyAssignment
            {
                AccessPolicyName = a.AccessPolicyName,
                IdentityName = a.IdentityName,
                ProvisioningState = a.ProvisioningState
            }).ToList();
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

    public async Task<List<AzureMcp.Areas.Redis.Models.ManagedRedis.Cluster>> ListRedisClustersAsync(
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

            return response.RedisClusters.Select(c => new AzureMcp.Areas.Redis.Models.ManagedRedis.Cluster
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
                PrivateEndpointConnections = c.PrivateEndpointConnections?.ToArray(),
                Zones = c.Zones?.ToArray(),
                Identity = c.Identity != null ? new AzureMcp.Models.Identity.ManagedIdentityInfo
                {
                    SystemAssignedIdentity = c.Identity.SystemAssignedIdentity != null ? new AzureMcp.Models.Identity.SystemAssignedIdentityInfo
                    {
                        Enabled = c.Identity.SystemAssignedIdentity.Enabled,
                        TenantId = c.Identity.SystemAssignedIdentity.TenantId,
                        PrincipalId = c.Identity.SystemAssignedIdentity.PrincipalId
                    } : null,
                    UserAssignedIdentities = c.Identity.UserAssignedIdentities?.Select(u => new AzureMcp.Models.Identity.UserAssignedIdentityInfo
                    {
                        ClientId = u.ClientId,
                        PrincipalId = u.PrincipalId
                    }).ToArray()
                } : null,
                Tags = c.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            }).ToList();
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

    public async Task<List<AzureMcp.Areas.Redis.Models.ManagedRedis.Database>> ListRedisDatabasesAsync(
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

            return response.RedisDatabases.Select(d => new AzureMcp.Areas.Redis.Models.ManagedRedis.Database
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
                Modules = d.Modules.Select(m => new AzureMcp.Areas.Redis.Models.ManagedRedis.Module
                {
                    Name = m.Name,
                    Args = m.Args,
                    Version = m.Version
                }).ToArray(),
                GeoReplicationGroupNickname = d.GeoReplicationGroupNickname,
                GeoReplicationLinkedDatabases = d.GeoReplicationLinkedDatabases.ToArray()
            }).ToList();
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

    public async Task<List<string>> ListPostgreSqlServersAsync(
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

            return response.ServerNames.ToList();
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

    public async Task<string> GetPostgreSqlServerConfigAsync(
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

            if (response.ServerConfig == null)
            {
                throw new LocalServiceCallException("GetPostgreSqlServerConfig", "No server configuration data returned from service");
            }

            // Format the configuration similar to PostgresService.GetServerConfigAsync
            var result = $"Server Name: {response.ServerConfig.Name}\n" +
                        $"Location: {response.ServerConfig.Location}\n" +
                        $"Version: {response.ServerConfig.Version}\n" +
                        $"SKU: {response.ServerConfig.SkuName}\n" +
                        $"Storage Size (GB): {response.ServerConfig.StorageSizeGb}\n" +
                        $"Backup Retention Days: {response.ServerConfig.BackupRetentionDays}\n" +
                        $"Geo-Redundant Backup: {response.ServerConfig.GeoRedundantBackup}";

            return result;
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

    public async Task<string> GetPostgreSqlServerParameterAsync(
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

            return response.ParameterValue;
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

    public async Task<string> SetPostgreSqlServerParameterAsync(
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

            return response.Message;
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

    public async Task<AzureMcp.Areas.Sql.Models.SqlDatabase> GetSqlDatabaseAsync(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string databaseName,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new GetSqlDatabaseRequest
            {
                SubscriptionId = subscriptionId,
                ResourceGroupName = resourceGroupName,
                ServerName = serverName,
                DatabaseName = databaseName,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.GetSqlDatabaseAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("GetSqlDatabase", GetErrorMessage(response.ErrorMessage));
            }

            if (response.Database == null)
            {
                throw new LocalServiceCallException("GetSqlDatabase", "No database information returned from service");
            }

            var database = response.Database;
            
            var sku = database.Sku != null ? new AzureMcp.Areas.Sql.Models.DatabaseSku(
                Name: database.Sku.Name,
                Tier: database.Sku.Tier,
                Capacity: database.Sku.Capacity,
                Family: database.Sku.Family,
                Size: database.Sku.Size
            ) : null;

            var creationDate = database.CreationDate > 0 
                ? DateTimeOffset.FromUnixTimeSeconds(database.CreationDate) 
                : (DateTimeOffset?)null;

            var earliestRestoreDate = database.EarliestRestoreDate > 0 
                ? DateTimeOffset.FromUnixTimeSeconds(database.EarliestRestoreDate) 
                : (DateTimeOffset?)null;

            var maxSizeBytes = database.MaxSizeBytes > 0 ? database.MaxSizeBytes : (long?)null;

            return new AzureMcp.Areas.Sql.Models.SqlDatabase(
                Name: database.Name,
                Id: database.Id,
                Type: database.Type,
                Location: string.IsNullOrEmpty(database.Location) ? null : database.Location,
                Sku: sku,
                Status: string.IsNullOrEmpty(database.Status) ? null : database.Status,
                Collation: string.IsNullOrEmpty(database.Collation) ? null : database.Collation,
                CreationDate: creationDate,
                MaxSizeBytes: maxSizeBytes,
                ServiceLevelObjective: string.IsNullOrEmpty(database.ServiceLevelObjective) ? null : database.ServiceLevelObjective,
                Edition: string.IsNullOrEmpty(database.Edition) ? null : database.Edition,
                ElasticPoolName: string.IsNullOrEmpty(database.ElasticPoolName) ? null : database.ElasticPoolName,
                EarliestRestoreDate: earliestRestoreDate,
                ReadScale: string.IsNullOrEmpty(database.ReadScale) ? null : database.ReadScale,
                ZoneRedundant: database.ZoneRedundant
            );
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("GetSqlDatabase", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<List<string>> ListSearchServicesAsync(
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

            return response.ServiceNames.ToList();
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

    public async Task<List<string>> ListMonitoredDatadogResourcesAsync(
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

            return response.MonitoredResourceNames.ToList();
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

    public async Task<List<AzureMcp.Areas.Authorization.Models.RoleAssignment>> ListRoleAssignmentsAsync(
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

            return roleAssignments;
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

    public async Task<List<MonitorWorkspaceInfo>> ListMonitorWorkspacesAsync(
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

            return response.Workspaces.Select(w => new MonitorWorkspaceInfo
            {
                Name = w.Name,
                CustomerId = w.CustomerId,
                ArmId = w.ArmId
            }).ToList();
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

    public async Task<List<string>> ListMonitorTablesAsync(
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

            return response.TableNames.ToList();
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

    public async Task<List<string>> ListMonitorTableTypesAsync(
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

            return response.TableTypes.ToList();
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

    public async Task<string> DeployModelAsync(
        string deploymentName,
        string modelName,
        string modelFormat,
        string azureAiServicesName,
        string resourceGroup,
        string subscriptionId,
        string? modelVersion = null,
        string? modelSource = null,
        string? skuName = null,
        int? skuCapacity = null,
        string? scaleType = null,
        int? scaleCapacity = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new DeployModelRequest
            {
                DeploymentName = deploymentName,
                ModelName = modelName,
                ModelFormat = modelFormat,
                AzureAiServicesName = azureAiServicesName,
                ResourceGroup = resourceGroup,
                SubscriptionId = subscriptionId,
                ModelVersion = modelVersion ?? string.Empty,
                ModelSource = modelSource ?? string.Empty,
                SkuName = skuName ?? string.Empty,
                SkuCapacity = skuCapacity ?? 0,
                ScaleType = scaleType ?? string.Empty,
                ScaleCapacity = scaleCapacity ?? 0,
                TenantId = tenantId ?? string.Empty
            };

            var response = await _client!.DeployModelAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("DeployModel", GetErrorMessage(response.ErrorMessage));
            }

            return response.DeploymentData;
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("DeployModel", ArmLocalServiceConnectError, ex);
        }
    }

    public async Task<string> ResolveResourceIdAsync(
        string subscription,
        string? resourceGroup,
        string? resourceType,
        string resourceName,
        string? tenant = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await EnsureServiceStartedAsync(cancellationToken);
        try
        {
            EnsureClient(endpoint);

            var request = new ResolveResourceIdRequest
            {
                Subscription = subscription,
                ResourceGroup = resourceGroup ?? string.Empty,
                ResourceType = resourceType ?? string.Empty,
                ResourceName = resourceName,
                Tenant = tenant ?? string.Empty
            };

            var response = await _client!.ResolveResourceIdAsync(request, cancellationToken: cancellationToken);

            if (!response.IsSuccess)
            {
                throw new LocalServiceCallException("ResolveResourceId", GetErrorMessage(response.ErrorMessage));
            }

            return response.ResourceId;
        }
        catch (LocalServiceCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LocalServiceCallException("ResolveResourceId", ArmLocalServiceConnectError, ex);
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

    private static string GetErrorMessage(string? responseErrorMessage)
    {
        return string.IsNullOrEmpty(responseErrorMessage) ? "Unknown error" : responseErrorMessage;
    }
}
