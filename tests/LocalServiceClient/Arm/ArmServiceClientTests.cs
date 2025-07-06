// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using AzureMcp.LocalServiceClient;
using AzureMcp.LocalServiceClient.Identity;
using AzureMcp.LocalServiceClient.Arm;

namespace AzureMcp.Tests.LocalServiceClient.Arm;

public class ArmServiceClientTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ArmServiceClientTests> _logger;
    private GrpcServiceHost? _identityServiceHost;
    private GrpcServiceHost? _armServiceHost;
    private IdentityServiceClient? _identityServiceClient;
    private ArmServiceClient? _armServiceClient;

    private const string DefaultSubscriptionId = "faa080af-c1d8-40ad-9cce-e1a450ca5b57";
    private const string DefaultSubscription = "Azure SDK Developer Playground";
    private const string PostgreSqlTestResourceGroup = "anuchan-entra-4433";
    private const string PostgreSqlTestServerName = "td08288e8c7e88f73";
    private const string AuthorizationTestScope = "/subscriptions/faa080af-c1d8-40ad-9cce-e1a450ca5b57/resourceGroups/anuchan-entra-4433";
    private const string DatadogTestResourceGroup = "anuchan-entra-4433";
    private const string DatadogTestMonitorName = "test-datadog-monitor";
    private const string MonitorTestResourceGroup = "anuchan-entra-4433";
    private const string MonitorTestWorkspaceName = "DefaultWorkspace-faa080af-c1d8-40ad-9cce-e1a450ca5b57-EUS"; // Common Log Analytics workspace name pattern

    public ArmServiceClientTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = _loggerFactory.CreateLogger<ArmServiceClientTests>();
    }

    private void SetupServices()
    {
        var testAssemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var testDirectory = Path.GetDirectoryName(testAssemblyLocation)!;
        
        var workspaceRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(testDirectory))))!;
        var identityServicePath = Path.Combine(workspaceRoot, "localservices", "AzureMcp.LocalService.Identity", "bin", "Debug", "net9.0");
        var armServicePath = Path.Combine(workspaceRoot, "localservices", "AzureMcp.LocalService.Arm", "bin", "Debug", "net9.0");

        // Set up Identity service
        var identityConfig = new GrpcServiceConfig
        {
            ServiceName = "Identity",
            ExtensionPath = identityServicePath,
            ExecutableNames = new[]
            {
                "AzureMcp.LocalService.Identity.exe",
                "AzureMcp.LocalService.Identity"
            },
            StartupTimeoutSeconds = 15
        };

        var identityLogger = _loggerFactory.CreateLogger<GrpcServiceHost>();
        _identityServiceHost = new GrpcServiceHost(identityLogger, identityConfig);
        _identityServiceClient = new IdentityServiceClient(_loggerFactory, _identityServiceHost);

        // Set up ARM service
        var armConfig = new GrpcServiceConfig
        {
            ServiceName = "Arm",
            ExtensionPath = armServicePath,
            ExecutableNames = new[]
            {
                "AzureMcp.LocalService.Arm.exe",
                "AzureMcp.LocalService.Arm"
            },
            StartupTimeoutSeconds = 15
        };

        var armLogger = _loggerFactory.CreateLogger<GrpcServiceHost>();
        _armServiceHost = new GrpcServiceHost(armLogger, armConfig);
        _armServiceClient = new ArmServiceClient(_loggerFactory, _identityServiceClient, _armServiceHost);
    }

    private async Task<string> GetTargetSubscriptionIdAsync(ArmServiceClient armServiceClient, CancellationToken cancellationToken)
    {
        var subscriptionsResult = await armServiceClient.ListSubscriptionsAsync(
            tenantId: null, 
            cancellationToken: cancellationToken);

        Assert.NotNull(subscriptionsResult);
        if (!subscriptionsResult.IsSuccess)
        {
            Assert.Fail($"Expected ListSubscriptionsAsync call to succeed, but got error: {subscriptionsResult.ErrorMessage}");
        }
        Assert.NotNull(subscriptionsResult.Subscriptions);
        Assert.True(subscriptionsResult.Subscriptions.Count > 0, "Should have at least one subscription");

        var targetSubscription = subscriptionsResult.Subscriptions.FirstOrDefault(s => 
            s.DisplayName.Equals(DefaultSubscription, StringComparison.OrdinalIgnoreCase));
        
        if (targetSubscription == null)
        {
            Assert.Fail($"Could not find '{DefaultSubscription}' subscription. Available subscriptions: " + 
                string.Join(", ", subscriptionsResult.Subscriptions.Select(s => s.DisplayName)));
        }

        return targetSubscription.SubscriptionId;
    }

    private void AssertServicesAreRunning()
    {
        Assert.NotNull(_identityServiceHost);
        Assert.NotNull(_armServiceHost);
        Assert.True(_identityServiceHost.IsRunning);
        Assert.True(_armServiceHost.IsRunning);
    }

    private static void FailOnException(Exception ex)
    {
        Assert.Fail($"Expected gRPC call to succeed or return a proper error response, but got exception: {ex.GetType().Name}: {ex.Message}");
    }

    private static string? ExtractResourceGroupFromArmId(string armId)
    {
        if (string.IsNullOrEmpty(armId))
            return null;

        var segments = armId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // ARM ID format: subscriptions/{sub}/resourceGroups/{rg}/providers/{provider}/...
        if (segments.Length >= 4 && 
            segments[0].Equals("subscriptions", StringComparison.OrdinalIgnoreCase) &&
            segments[2].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase))
        {
            return segments[3];
        }

        return null;
    }

    [Fact]
    public async Task CanAttemptToGetIdentityServiceStatusThroughGrpcCall()
    {
        // Arrange
        SetupServices();

        // Act
        try
        {
            Assert.NotNull(_armServiceClient);
            var status = await _armServiceClient.GetIdentityServiceStatusAsync(
                tenantId: null, 
                scopes: new[] { "https://management.azure.com/.default" }, 
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(status);
            _logger.LogInformation("Identity service status: IsSuccess={IsSuccess}, Details={Details}", 
                status.IsSuccess, status.Details);
            if (!status.IsSuccess && !string.IsNullOrEmpty(status.ErrorMessage))
            {
                Assert.Fail($"Expected authentication status check to succeed, but got error {status.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanAttemptToListSubscriptionsThroughGrpcCall()
    {
        // Arrange
        SetupServices();

        // Act
        try
        {
            Assert.NotNull(_armServiceClient);
            var result = await _armServiceClient.ListSubscriptionsAsync(
                tenantId: null, 
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            if (!result.IsSuccess)
            {
                Assert.Fail($"Expected subscription list to succeed, but got error: {result.ErrorMessage}");
            }
            Assert.True(result.IsSuccess, "ListSubscriptionsAsync should succeed");
            Assert.NotNull(result.Subscriptions);
            Assert.True(result.Subscriptions.Count > 0, "Should have at least one subscription");
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanAttemptToGetStorageAccountsThroughGrpcCall()
    {
        // Arrange
        SetupServices();

        // Act
        try
        {
            Assert.NotNull(_armServiceClient);
            var subscriptionId = await GetTargetSubscriptionIdAsync(_armServiceClient, TestContext.Current.CancellationToken);

            var result = await _armServiceClient.GetStorageAccountsAsync(
                subscriptionId: subscriptionId,
                tenantId: null, 
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Count > 0, $"Should have at least one storage account in {DefaultSubscription} subscription");

            var firstAccountName = result[0];
            var keyResult = await _armServiceClient.GetStorageAccountKeysAsync(
                accountName: firstAccountName,
                subscriptionId: subscriptionId,
                tenantId: null, 
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(keyResult);
            Assert.False(string.IsNullOrEmpty(keyResult), "Storage account key should not be null or empty");

            var connectionString = await _armServiceClient.GetStorageAccountConnectionStringAsync(
                accountName: firstAccountName,
                subscriptionId: subscriptionId,
                tenantId: null, 
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(connectionString);
            Assert.False(string.IsNullOrEmpty(connectionString), "Storage account connection string should not be null or empty");
            Assert.Contains("AccountName", connectionString, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AccountKey", connectionString, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanAttemptToGetCosmosAccountThroughGrpcCall()
    {
        // Arrange
        SetupServices();

        // Act
        try
        {
            Assert.NotNull(_armServiceClient);
            var subscriptionId = await GetTargetSubscriptionIdAsync(_armServiceClient, TestContext.Current.CancellationToken);

            var cosmosAccounts = await _armServiceClient.GetCosmosAccountsAsync(
                subscriptionId: subscriptionId,
                tenantId: null, 
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(cosmosAccounts);
            Assert.True(cosmosAccounts.Count > 0, $"Should have at least one Cosmos DB account in {DefaultSubscription} subscription");

            var firstAccountName = cosmosAccounts[0];
            var accountResult = await _armServiceClient.GetCosmosAccountAsync(
                accountName: firstAccountName,
                subscriptionId: subscriptionId,
                tenantId: null, 
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(accountResult);
            if (!accountResult.IsSuccess)
            {
                Assert.Fail($"Expected GetCosmosAccountAsync call to succeed, but got error: {accountResult.ErrorMessage}");
            }
            Assert.NotNull(accountResult.Account);
            Assert.Equal(firstAccountName, accountResult.Account.Name);
            
            Assert.NotNull(accountResult.Account.PrimaryMasterKey);
            Assert.NotEmpty(accountResult.Account.PrimaryMasterKey);
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanAttemptToGetAppConfigAccountThroughGrpcCall()
    {
        // Arrange
        SetupServices();

        // Act
        try
        {
            Assert.NotNull(_armServiceClient);
            var subscriptionId = await GetTargetSubscriptionIdAsync(_armServiceClient, TestContext.Current.CancellationToken);

            var accounts = await _armServiceClient.GetAppConfigAccountsAsync(
                subscriptionId: subscriptionId,
                tenantId: null, 
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(accounts);
            Assert.True(accounts.Count > 0, $"Should have at least one App Configuration account in {DefaultSubscription} subscription");

            var firstAccount = accounts.First();
            var firstAccountName = firstAccount.Name;
            Assert.False(string.IsNullOrEmpty(firstAccountName), "App Configuration account name should not be empty");

            var endpoint = await _armServiceClient.GetAppConfigAccountEndpointAsync(
                accountName: firstAccountName,
                subscriptionId: subscriptionId,
                tenantId: null,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(endpoint);
            Assert.False(string.IsNullOrEmpty(endpoint), "App Configuration account endpoint should not be empty");
            Assert.True(Uri.TryCreate(endpoint, UriKind.Absolute, out _), "Endpoint should be a valid absolute URI");
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanAttemptToGetKustoClusterThroughGrpcCall()
    {
        // Arrange
        SetupServices();

        // Act
        try
        {
            Assert.NotNull(_armServiceClient);
            var subscriptionId = await GetTargetSubscriptionIdAsync(_armServiceClient, TestContext.Current.CancellationToken);

            var clusters = await _armServiceClient.GetKustoClustersAsync(
                subscriptionId: subscriptionId,
                tenantId: null, 
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(clusters);
            Assert.True(clusters.Count > 0, $"Should have at least one Kusto cluster in {DefaultSubscription} subscription");

            var firstClusterName = clusters[0];
            var cluster = await _armServiceClient.GetKustoClusterAsync(
                clusterName: firstClusterName,
                subscriptionId: subscriptionId,
                tenantId: null, 
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(cluster);
            Assert.Equal(firstClusterName, cluster.ClusterName);
            Assert.False(string.IsNullOrEmpty(cluster.ClusterUri), "Kusto cluster URI should not be empty");
            Assert.False(string.IsNullOrEmpty(cluster.Location), "Kusto cluster location should not be empty");
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanAttemptToGetResourceGroupsThroughGrpcCall()
    {
        SetupServices();
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token;

        try
        {
            var subscriptionId = await GetTargetSubscriptionIdAsync(_armServiceClient!, cancellationToken);

            var resourceGroups = await _armServiceClient!.GetResourceGroupsAsync(
                subscriptionId,
                tenantId: null,
                cancellationToken: cancellationToken);

            Assert.NotNull(resourceGroups);
            Assert.True(resourceGroups.Count > 0, $"Should have at least one resource group in {DefaultSubscription} subscription");

            var firstResourceGroup = resourceGroups.First();
            Assert.False(string.IsNullOrEmpty(firstResourceGroup.Name), "Resource group name should not be empty");
            Assert.False(string.IsNullOrEmpty(firstResourceGroup.Id), "Resource group ID should not be empty");
            Assert.False(string.IsNullOrEmpty(firstResourceGroup.Location), "Resource group location should not be empty");

            var resourceGroup = await _armServiceClient!.GetResourceGroupAsync(
                firstResourceGroup.Name,
                subscriptionId,
                tenantId: null,
                cancellationToken: cancellationToken);

            Assert.NotNull(resourceGroup);
            Assert.Equal(firstResourceGroup.Name, resourceGroup.Name);
            Assert.Equal(firstResourceGroup.Id, resourceGroup.Id);
            Assert.Equal(firstResourceGroup.Location, resourceGroup.Location);
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanAttemptToListRedisCachesThroughGrpcCall()
    {
        // Arrange
        SetupServices();
        var subscriptionId = await GetTargetSubscriptionIdAsync(_armServiceClient!, TestContext.Current.CancellationToken);

        // Act
        try
        {
            Assert.NotNull(_armServiceClient);
            var result = await _armServiceClient.ListRedisCachesAsync(
                subscriptionId: subscriptionId,
                tenantId: null,
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            if (!result.IsSuccess)
            {
                Assert.Fail($"ListRedisCachesAsync should succeed, but got: {result.ErrorMessage}");
            }
            Assert.NotNull(result.RedisCaches);
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanAttemptToListRedisClustersThroughGrpcCall()
    {
        // Arrange
        SetupServices();
        var subscriptionId = await GetTargetSubscriptionIdAsync(_armServiceClient!, TestContext.Current.CancellationToken);

        // Act
        try
        {
            Assert.NotNull(_armServiceClient);
            var result = await _armServiceClient.ListRedisClustersAsync(
                subscriptionId: subscriptionId,
                tenantId: null,
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            if (!result.IsSuccess)
            {
                Assert.Fail($"ListRedisClustersAsync should succeed, but got: {result.ErrorMessage}");
            }
            Assert.NotNull(result.RedisClusters);
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanAttemptToListPostgreSqlServersThroughGrpcCall()
    {
        // Arrange
        SetupServices();
        var subscriptionId = await GetTargetSubscriptionIdAsync(_armServiceClient!, TestContext.Current.CancellationToken);

        // Act
        try
        {
            Assert.NotNull(_armServiceClient);
            var result = await _armServiceClient.ListPostgreSqlServersAsync(
                subscriptionId: subscriptionId,
                resourceGroupName: PostgreSqlTestResourceGroup,
                tenantId: null,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            if (!result.IsSuccess)
            {
                Assert.Fail($"ListPostgreSqlServersAsync should succeed, but got: {result.ErrorMessage}");
            }
            Assert.NotNull(result.ServerNames);
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanAttemptToGetPostgreSqlServerConfigThroughGrpcCall()
    {
        // Arrange
        SetupServices();
        var subscriptionId = await GetTargetSubscriptionIdAsync(_armServiceClient!, TestContext.Current.CancellationToken);

        // Act
        try
        {
            Assert.NotNull(_armServiceClient);
            var result = await _armServiceClient.GetPostgreSqlServerConfigAsync(
                subscriptionId: subscriptionId,
                resourceGroupName: PostgreSqlTestResourceGroup,
                serverName: PostgreSqlTestServerName,
                tenantId: null,
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            if (!result.IsSuccess)
            {
                Assert.Fail($"GetPostgreSqlServerConfigAsync should succeed, but got: {result.ErrorMessage}");
            }
            Assert.NotNull(result.ServerConfig);
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanAttemptToGetPostgreSqlServerParameterThroughGrpcCall()
    {
        // Arrange
        SetupServices();
        var subscriptionId = await GetTargetSubscriptionIdAsync(_armServiceClient!, TestContext.Current.CancellationToken);

        // Act
        try
        {
            Assert.NotNull(_armServiceClient);
            var result = await _armServiceClient.GetPostgreSqlServerParameterAsync(
                subscriptionId: subscriptionId,
                resourceGroupName: PostgreSqlTestResourceGroup,
                serverName: PostgreSqlTestServerName,
                parameterName: "max_connections",
                tenantId: null,
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            if (!result.IsSuccess)
            {
                Assert.Fail($"GetPostgreSqlServerParameterAsync should succeed, but got: {result.ErrorMessage}");
            }
            Assert.False(string.IsNullOrEmpty(result.ParameterValue));
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanAttemptToSetPostgreSqlServerParameterThroughGrpcCall()
    {
        // Arrange
        SetupServices();
        var subscriptionId = await GetTargetSubscriptionIdAsync(_armServiceClient!, TestContext.Current.CancellationToken);

        // Act
        try
        {
            Assert.NotNull(_armServiceClient);
            var result = await _armServiceClient.SetPostgreSqlServerParameterAsync(
                subscriptionId: subscriptionId,
                resourceGroupName: PostgreSqlTestResourceGroup,
                serverName: PostgreSqlTestServerName,
                parameterName: "max_connections",
                parameterValue: "200",
                tenantId: null,
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            if (!result.IsSuccess)
            {
                Assert.Fail($"SetPostgreSqlServerParameterAsync should succeed, but got: {result.ErrorMessage}");
            }
            Assert.False(string.IsNullOrEmpty(result.Message));
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanAttemptToListSearchServicesThroughGrpcCall()
    {
        // Arrange
        SetupServices();
        var subscriptionId = await GetTargetSubscriptionIdAsync(_armServiceClient!, TestContext.Current.CancellationToken);

        // Act
        try
        {
            Assert.NotNull(_armServiceClient);
            var result = await _armServiceClient.ListSearchServicesAsync(
                subscriptionId: subscriptionId,
                tenantId: null,
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            if (!result.IsSuccess)
            {
                Assert.Fail($"ListSearchServicesAsync should succeed, but got: {result.ErrorMessage}");
            }
            Assert.NotNull(result.ServiceNames);
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanAttemptToListRoleAssignmentsThroughGrpcCall()
    {
        SetupServices();

        try
        {
            var result = await _armServiceClient!.ListRoleAssignmentsAsync(
                AuthorizationTestScope,
                cancellationToken: TestContext.Current.CancellationToken
            );

            Assert.NotNull(result);
            if (!result.IsSuccess)
            {
                Assert.Fail($"ListRoleAssignmentsAsync should succeed, but got: {result.ErrorMessage}");
            }
            Assert.NotNull(result.RoleAssignments);
            if (result.RoleAssignments.Any())
            {
                var firstAssignment = result.RoleAssignments.First();
                Assert.NotNull(firstAssignment.Id);
                Assert.NotNull(firstAssignment.Scope);
            }
            else
            {
                Assert.Fail($"Expected to find role assignments in scope {AuthorizationTestScope}, but none were returned");
            }
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanAttemptToListMonitoredDatadogResourcesThroughGrpcCall()
    {
        SetupServices();

        try
        {
            var result = await _armServiceClient!.ListMonitoredDatadogResourcesAsync(
                DefaultSubscriptionId,
                DatadogTestResourceGroup,
                DatadogTestMonitorName,
                cancellationToken: TestContext.Current.CancellationToken
            );

            Assert.NotNull(result);
            Assert.NotNull(result.MonitoredResourceNames);
        }
        catch (LocalServiceCallException ex)
        {
            // We expect this to fail with 404 since no Datadog monitor exists
            Assert.Equal("ListMonitoredDatadogResources", ex.MethodName);
            Assert.Contains("Status: 404 (Not Found)", ex.ServiceErrorMessage);
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanListMonitorWorkspacesThroughGrpcCall()
    {
        SetupServices();

        try
        {
            var result = await _armServiceClient!.ListMonitorWorkspacesAsync(
                DefaultSubscriptionId,
                cancellationToken: TestContext.Current.CancellationToken
            );

            Assert.NotNull(result);
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.NotNull(result.Workspaces);
            Assert.True(result.Workspaces.Count > 0, $"Should have at least one Monitor workspace in {DefaultSubscription} subscription");
            
            var firstWorkspace = result.Workspaces.First();
            Assert.False(string.IsNullOrEmpty(firstWorkspace.ArmId), "Workspace ARM ID should not be empty");
            Assert.True(firstWorkspace.ArmId.Contains("Microsoft.OperationalInsights/workspaces"), "ARM ID should contain the correct resource provider");
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanListMonitorTablesThroughGrpcCall()
    {
        SetupServices();

        try
        {
            var workspacesResult = await _armServiceClient!.ListMonitorWorkspacesAsync(
                DefaultSubscriptionId,
                cancellationToken: TestContext.Current.CancellationToken
            );

            Assert.NotNull(workspacesResult);
            Assert.True(workspacesResult.IsSuccess, workspacesResult.ErrorMessage);
            Assert.NotNull(workspacesResult.Workspaces);
            Assert.True(workspacesResult.Workspaces.Count > 0, "Should have at least one workspace to test with");

            // try tables from the first workspace
            var testWorkspace = workspacesResult.Workspaces.First();
            
            // Extract resource group name from the workspace ARM ID
            var resourceGroupName = ExtractResourceGroupFromArmId(testWorkspace.ArmId);
            Assert.False(string.IsNullOrEmpty(resourceGroupName), $"Could not extract resource group from ARM ID: {testWorkspace.ArmId}");

            var result = await _armServiceClient!.ListMonitorTablesAsync(
                DefaultSubscriptionId,
                resourceGroupName!,
                testWorkspace.Name,
                tableType: null, // "CustomLog"
                cancellationToken: TestContext.Current.CancellationToken
            );

            Assert.NotNull(result);
            if (!result.IsSuccess)
            {
                Assert.True(result.ErrorMessage.Contains("not found"),  $"Expected a 'not found' or workspace-related error, but got: {result.ErrorMessage}");
            }
            else
            {
                Assert.NotNull(result.TableNames);
            }
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public async Task CanListMonitorTableTypesThroughGrpcCall()
    {
        SetupServices();

        try
        {
            var workspacesResult = await _armServiceClient!.ListMonitorWorkspacesAsync(
                DefaultSubscriptionId,
                cancellationToken: TestContext.Current.CancellationToken
            );

            Assert.NotNull(workspacesResult);
            Assert.True(workspacesResult.IsSuccess, workspacesResult.ErrorMessage);
            Assert.NotNull(workspacesResult.Workspaces);
            Assert.True(workspacesResult.Workspaces.Count > 0, "Should have at least one workspace to test with");

            // try table types from the first workspace
            var testWorkspace = workspacesResult.Workspaces.First();
            
            // Extract resource group name from the workspace ARM ID
            var resourceGroupName = ExtractResourceGroupFromArmId(testWorkspace.ArmId);
            Assert.False(string.IsNullOrEmpty(resourceGroupName), $"Could not extract resource group from ARM ID: {testWorkspace.ArmId}");

            var result = await _armServiceClient!.ListMonitorTableTypesAsync(
                DefaultSubscriptionId,
                resourceGroupName!,
                testWorkspace.Name,
                cancellationToken: TestContext.Current.CancellationToken
            );

            Assert.NotNull(result);
            if (!result.IsSuccess)
            {
                Assert.True(result.ErrorMessage.Contains("not found"),  $"Expected a 'not found' or workspace-related error, but got: {result.ErrorMessage}");
            }
            else
            {
                Assert.NotNull(result.TableTypes);
            }
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }

        AssertServicesAreRunning();
    }

    [Fact]
    public void LocalServiceCallException_ShouldHaveCorrectProperties()
    {
        // Test the exception properties
        var methodName = "TestMethod";
        var errorMessage = "Test error message";
        
        var exception = new LocalServiceCallException(methodName, errorMessage);
        
        Assert.Equal(methodName, exception.MethodName);
        Assert.Equal(errorMessage, exception.ServiceErrorMessage);
        Assert.Equal($"Local service call '{methodName}' failed: {errorMessage}", exception.Message);
        
        // Test with inner exception
        var innerException = new InvalidOperationException("Inner error");
        var exceptionWithInner = new LocalServiceCallException(methodName, errorMessage, innerException);
        
        Assert.Equal(methodName, exceptionWithInner.MethodName);
        Assert.Equal(errorMessage, exceptionWithInner.ServiceErrorMessage);
        Assert.Equal($"Local service call '{methodName}' failed: {errorMessage}", exceptionWithInner.Message);
        Assert.Equal(innerException, exceptionWithInner.InnerException);
    }

    public void Dispose()
    {
        _armServiceClient?.Dispose();
        _identityServiceClient?.Dispose();
        _armServiceHost?.Dispose();
        _identityServiceHost?.Dispose();
        _loggerFactory?.Dispose();
    }
}
