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
                _logger.LogInformation("Expected authentication failure: {ErrorMessage}", status.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call GetIdentityServiceStatusAsync");
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
            _logger.LogError(ex, "Failed to call ListSubscriptionsAsync");
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
            var subscriptionsResult = await _armServiceClient.ListSubscriptionsAsync(
                tenantId: null, 
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(subscriptionsResult);
            if (!subscriptionsResult.IsSuccess)
            {
                Assert.Fail($"Expected subscription list to succeed, but got error: {subscriptionsResult.ErrorMessage}");
            }
            Assert.True(subscriptionsResult.IsSuccess, "ListSubscriptionsAsync should succeed");
            Assert.NotNull(subscriptionsResult.Subscriptions);
            Assert.True(subscriptionsResult.Subscriptions.Count > 0, "Should have at least one subscription");

            var targetSubscription = subscriptionsResult.Subscriptions.FirstOrDefault(s => 
                s.DisplayName.Equals(DefaultSubscription, StringComparison.OrdinalIgnoreCase));
            
            if (targetSubscription == null)
            {
                Assert.Fail($"Could not find '{DefaultSubscription}' subscription. Available subscriptions: " + 
                    string.Join(", ", subscriptionsResult.Subscriptions.Select(s => s.DisplayName)));
            }

            var subscriptionId = targetSubscription.SubscriptionId;

            var result = await _armServiceClient.GetStorageAccountsAsync(
                subscriptionId: subscriptionId,
                tenantId: null, 
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            if (!result.IsSuccess)
            {
                Assert.Fail($"Expected storage accounts call to succeed, but got error: {result.ErrorMessage}");
            }
            Assert.True(result.IsSuccess, "GetStorageAccountsAsync should succeed");
            Assert.True(result.StorageAccounts.Count > 0, $"Should have at least one storage account in {DefaultSubscription} subscription");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call GetStorageAccountsAsync");
            Assert.Fail($"Expected gRPC call to succeed or return a proper error response, but got exception: {ex.GetType().Name}: {ex.Message}");
        }

        Assert.NotNull(_identityServiceHost);
        Assert.NotNull(_armServiceHost);
        Assert.True(_identityServiceHost.IsRunning);
        Assert.True(_armServiceHost.IsRunning);
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
