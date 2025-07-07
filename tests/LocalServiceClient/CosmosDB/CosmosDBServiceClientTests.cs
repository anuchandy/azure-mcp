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
using AzureMcp.LocalServiceClient.CosmosDB;

namespace AzureMcp.Tests.LocalServiceClient.CosmosDB;

public class CosmosDBServiceClientTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<CosmosDBServiceClientTests> _logger;
    private GrpcServiceHost? _identityServiceHost;
    private GrpcServiceHost? _armServiceHost;
    private GrpcServiceHost? _cosmosDBServiceHost;
    private IdentityServiceClient? _identityServiceClient;
    private ArmServiceClient? _armServiceClient;
    private CosmosDBServiceClient? _cosmosDBServiceClient;

    private const string DefaultSubscriptionId = "faa080af-c1d8-40ad-9cce-e1a450ca5b57";
    private const string TestCosmosAccountName = "td08288e8c7e88f73";

    public CosmosDBServiceClientTests()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = _loggerFactory.CreateLogger<CosmosDBServiceClientTests>();
    }

    private void SetupServices()
    {
        var testAssemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var testDirectory = Path.GetDirectoryName(testAssemblyLocation)!;

        var workspaceRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(testDirectory))))!;
        var identityServicePath = Path.Combine(workspaceRoot, "localservices", "AzureMcp.LocalService.Identity", "bin", "Debug", "net9.0");
        var armServicePath = Path.Combine(workspaceRoot, "localservices", "AzureMcp.LocalService.Arm", "bin", "Debug", "net9.0");
        var cosmosDBServicePath = Path.Combine(workspaceRoot, "localservices", "AzureMcp.LocalService.CosmosDB", "bin", "Debug", "net9.0");

        // Set up Identity service
        var identityConfig = new GrpcServiceConfig
        {
            ServiceName = "Identity",
            ExtensionPath = identityServicePath,
            ExecutableNames = new[] { "AzureMcp.LocalService.Identity.exe", "AzureMcp.LocalService.Identity" },
            HealthEndpoint = "/ishealthy",
            StartupTimeoutSeconds = 30
        };
        _identityServiceHost = new GrpcServiceHost(_loggerFactory.CreateLogger<GrpcServiceHost>(), identityConfig);
        _identityServiceClient = new IdentityServiceClient(_loggerFactory, _identityServiceHost);

        // Set up ARM service
        var armConfig = new GrpcServiceConfig
        {
            ServiceName = "Arm",
            ExtensionPath = armServicePath,
            ExecutableNames = new[] { "AzureMcp.LocalService.Arm.exe", "AzureMcp.LocalService.Arm" },
            HealthEndpoint = "/ishealthy",
            StartupTimeoutSeconds = 30
        };
        _armServiceHost = new GrpcServiceHost(_loggerFactory.CreateLogger<GrpcServiceHost>(), armConfig);
        _armServiceClient = new ArmServiceClient(_loggerFactory, _identityServiceClient, _armServiceHost);

        // Set up CosmosDB service
        var cosmosDBConfig = new GrpcServiceConfig
        {
            ServiceName = "CosmosDB",
            ExtensionPath = cosmosDBServicePath,
            ExecutableNames = new[] { "AzureMcp.LocalService.CosmosDB.exe", "AzureMcp.LocalService.CosmosDB" },
            HealthEndpoint = "/ishealthy",
            StartupTimeoutSeconds = 30
        };
        _cosmosDBServiceHost = new GrpcServiceHost(_loggerFactory.CreateLogger<GrpcServiceHost>(), cosmosDBConfig);
        _cosmosDBServiceClient = new CosmosDBServiceClient(_loggerFactory, _identityServiceClient, _armServiceClient, _cosmosDBServiceHost);
    }

    [Fact]
    public async Task CanAttemptToEnsureServiceStartedThroughGrpcCall()
    {
        // Arrange
        SetupServices();

        // Act
        var endpoint = await _cosmosDBServiceClient!.EnsureServiceStartedAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(endpoint);
        Assert.StartsWith("http://localhost:", endpoint);
    }

    [Fact]
    public async Task CanAttemptToListDatabasesThroughGrpcCall()
    {
        SetupServices();

        try
        {
            var accounts = await _armServiceClient!.GetCosmosAccountsAsync(
                DefaultSubscriptionId,
                cancellationToken: TestContext.Current.CancellationToken);

            if (!accounts.Any(a => a == TestCosmosAccountName))
            {
                Assert.Fail($"Cosmos DB account '{TestCosmosAccountName}' not found in subscription {DefaultSubscriptionId}. Available accounts: {string.Join(", ", accounts)}");
            }

            var account = TestCosmosAccountName;
            var databases = await _cosmosDBServiceClient!.ListDatabasesAsync(
                account,
                DefaultSubscriptionId,
                "Credential",
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(databases);
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }
    }

    [Fact]
    public async Task CanAttemptToListContainersThroughGrpcCall()
    {
        SetupServices();

        try
        {
            var accounts = await _armServiceClient!.GetCosmosAccountsAsync(
                DefaultSubscriptionId,
                cancellationToken: TestContext.Current.CancellationToken);

            if (!accounts.Any(a => a == TestCosmosAccountName))
            {
                Assert.Fail($"Cosmos DB account '{TestCosmosAccountName}' not found in subscription {DefaultSubscriptionId}. Available accounts: {string.Join(", ", accounts)}");
            }

            var account = TestCosmosAccountName;
            var databases = await _cosmosDBServiceClient!.ListDatabasesAsync(
                TestCosmosAccountName,
                DefaultSubscriptionId,
                "Credential",
                cancellationToken: TestContext.Current.CancellationToken);

            if (!databases.Any())
            {
                _logger.LogWarning("No databases found in Cosmos DB account {AccountName}. Skipping container test.", account);
                return;
            }

            var database = databases.First();
            var containers = await _cosmosDBServiceClient!.ListContainersAsync(
                TestCosmosAccountName,
                database,
                DefaultSubscriptionId,
                "Credential",
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(containers);
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }
    }

    [Fact]
    public async Task CanAttemptToQueryItemsThroughGrpcCall()
    {
        SetupServices();

        try
        {
            var accounts = await _armServiceClient!.GetCosmosAccountsAsync(
                DefaultSubscriptionId,
                cancellationToken: TestContext.Current.CancellationToken);

            if (!accounts.Any(a => a == TestCosmosAccountName))
            {
                Assert.Fail($"Cosmos DB account '{TestCosmosAccountName}' not found in subscription {DefaultSubscriptionId}. Available accounts: {string.Join(", ", accounts)}");
            }

            var account = TestCosmosAccountName;
            var databases = await _cosmosDBServiceClient!.ListDatabasesAsync(
                TestCosmosAccountName,
                DefaultSubscriptionId,
                "Credential",
                cancellationToken: TestContext.Current.CancellationToken);

            if (!databases.Any())
            {
                _logger.LogWarning("No databases found in Cosmos DB account {AccountName}. Skipping query items test.", account);
                return;
            }

            var database = databases.First();
            var containers = await _cosmosDBServiceClient!.ListContainersAsync(
                TestCosmosAccountName,
                database,
                DefaultSubscriptionId,
                "Credential",
                cancellationToken: TestContext.Current.CancellationToken);

            if (!containers.Any())
            {
                _logger.LogWarning("No containers found in database {DatabaseName} of Cosmos DB account {AccountName}. Skipping query items test.", database, account);
                return;
            }

            var container = containers.First();
            var query = "SELECT TOP 5 * FROM c";
            var items = await _cosmosDBServiceClient!.QueryItemsAsync(
                TestCosmosAccountName,
                database,
                container,
                query,
                DefaultSubscriptionId,
                "Credential",
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(items);
        }
        catch (Exception ex)
        {
            FailOnException(ex);
        }
    }

    public void Dispose()
    {
        _cosmosDBServiceClient?.Dispose();
        _armServiceClient?.Dispose();
        _identityServiceClient?.Dispose();
        _cosmosDBServiceHost?.Dispose();
        _armServiceHost?.Dispose();
        _identityServiceHost?.Dispose();
        _loggerFactory?.Dispose();
    }
    
    private static void FailOnException(Exception ex)
    {
        Assert.Fail($"Expected gRPC call to succeed or return a proper error response, but got exception: {ex.GetType().Name}: {ex.Message}");
    }
}
