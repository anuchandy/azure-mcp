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

    private const string DefaultSubscription = "Azure SDK Developer Playground";

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
            Assert.Fail($"Expected gRPC call to succeed or return a proper error response, but got exception: {ex.GetType().Name}: {ex.Message}");
        }

        Assert.NotNull(_identityServiceHost);
        Assert.NotNull(_armServiceHost);
        Assert.True(_identityServiceHost.IsRunning);
        Assert.True(_armServiceHost.IsRunning);
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
            Assert.Fail($"Expected gRPC call to succeed or return a proper error response, but got exception: {ex.GetType().Name}: {ex.Message}");
        }

        Assert.NotNull(_identityServiceHost);
        Assert.NotNull(_armServiceHost);
        Assert.True(_identityServiceHost.IsRunning);
        Assert.True(_armServiceHost.IsRunning);
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
            if (!result.IsSuccess)
            {
                Assert.Fail($"Expected GetStorageAccountsAsync call to succeed, but got error: {result.ErrorMessage}");
            }
            Assert.True(result.StorageAccounts.Count > 0, $"Should have at least one storage account in {DefaultSubscription} subscription");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Expected gRPC call to succeed or return a proper error response, but got exception: {ex.GetType().Name}: {ex.Message}");
        }

        Assert.NotNull(_identityServiceHost);
        Assert.NotNull(_armServiceHost);
        Assert.True(_identityServiceHost.IsRunning);
        Assert.True(_armServiceHost.IsRunning);
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

            var accountsResult = await _armServiceClient.GetCosmosAccountsAsync(
                subscriptionId: subscriptionId,
                tenantId: null, 
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(accountsResult);
            if (!accountsResult.IsSuccess)
            {
                Assert.Fail($"Expected GetCosmosAccountsAsync call to succeed, but got error: {accountsResult.ErrorMessage}");
            }
            Assert.NotNull(accountsResult.CosmosAccounts);
            Assert.True(accountsResult.CosmosAccounts.Count > 0, $"Should have at least one Cosmos DB account in {DefaultSubscription} subscription");

            var firstAccountName = accountsResult.CosmosAccounts[0];
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
        }
        catch (Exception ex)
        {
            Assert.Fail($"Expected gRPC call to succeed or return a proper error response, but got exception: {ex.GetType().Name}: {ex.Message}");
        }

        Assert.NotNull(_identityServiceHost);
        Assert.NotNull(_armServiceHost);
        Assert.True(_identityServiceHost.IsRunning);
        Assert.True(_armServiceHost.IsRunning);
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

            var accountsResult = await _armServiceClient.GetAppConfigAccountsAsync(
                subscriptionId: subscriptionId,
                tenantId: null, 
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(accountsResult);
            if (!accountsResult.IsSuccess)
            {
                Assert.Fail($"Expected GetAppConfigAccountsAsync call to succeed, but got error: {accountsResult.ErrorMessage}");
            }
            Assert.NotNull(accountsResult.AppConfigAccounts);
            Assert.True(accountsResult.AppConfigAccounts.Count > 0, $"Should have at least one App Configuration account in {DefaultSubscription} subscription");

            var firstAccountName = accountsResult.AppConfigAccounts.First().Name;
            Assert.False(string.IsNullOrEmpty(firstAccountName), "App Configuration account name should not be empty");

            var endpointResult = await _armServiceClient.GetAppConfigAccountEndpointAsync(
                accountName: firstAccountName,
                subscriptionId: subscriptionId,
                tenantId: null,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(endpointResult);
            if (!endpointResult.IsSuccess)
            {
                Assert.Fail($"Expected GetAppConfigAccountEndpointAsync call to succeed, but got error: {endpointResult.ErrorMessage}");
            }
            Assert.False(string.IsNullOrEmpty(endpointResult.Endpoint), "App Configuration account endpoint should not be empty");
            Assert.True(Uri.TryCreate(endpointResult.Endpoint, UriKind.Absolute, out _), "Endpoint should be a valid absolute URI");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Expected gRPC call to succeed or return a proper error response, but got exception: {ex.GetType().Name}: {ex.Message}");
        }

        Assert.NotNull(_identityServiceHost);
        Assert.NotNull(_armServiceHost);
        Assert.True(_identityServiceHost.IsRunning);
        Assert.True(_armServiceHost.IsRunning);
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

            var clustersResult = await _armServiceClient.GetKustoClustersAsync(
                subscriptionId: subscriptionId,
                tenantId: null, 
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(clustersResult);
            if (!clustersResult.IsSuccess)
            {
                Assert.Fail($"Expected GetKustoClustersAsync call to succeed, but got error: {clustersResult.ErrorMessage}");
            }
            Assert.NotNull(clustersResult.KustoClusters);
            Assert.True(clustersResult.KustoClusters.Count > 0, $"Should have at least one Kusto cluster in {DefaultSubscription} subscription");

            var firstClusterName = clustersResult.KustoClusters[0];
            var clusterResult = await _armServiceClient.GetKustoClusterAsync(
                clusterName: firstClusterName,
                subscriptionId: subscriptionId,
                tenantId: null, 
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(clusterResult);
            if (!clusterResult.IsSuccess)
            {
                Assert.Fail($"Expected GetKustoClusterAsync call to succeed, but got error: {clusterResult.ErrorMessage}");
            }
            Assert.NotNull(clusterResult.Cluster);
            Assert.Equal(firstClusterName, clusterResult.Cluster.ClusterName);
            Assert.False(string.IsNullOrEmpty(clusterResult.Cluster.ClusterUri), "Kusto cluster URI should not be empty");
            Assert.False(string.IsNullOrEmpty(clusterResult.Cluster.Location), "Kusto cluster location should not be empty");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Expected gRPC call to succeed or return a proper error response, but got exception: {ex.GetType().Name}: {ex.Message}");
        }

        Assert.NotNull(_identityServiceHost);
        Assert.NotNull(_armServiceHost);
        Assert.True(_identityServiceHost.IsRunning);
        Assert.True(_armServiceHost.IsRunning);
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

    public void Dispose()
    {
        _armServiceClient?.Dispose();
        _identityServiceClient?.Dispose();
        _armServiceHost?.Dispose();
        _identityServiceHost?.Dispose();
        _loggerFactory?.Dispose();
    }
}
