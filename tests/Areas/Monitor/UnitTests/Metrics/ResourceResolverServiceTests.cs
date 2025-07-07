// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ClientModel.Primitives;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Core;
using AzureMcp.Areas.Monitor.Services;
using AzureMcp.LocalServiceClient.Arm;
using AzureMcp.LocalServiceClient.Identity;
using AzureMcp.Options;
using AzureMcp.Services.Azure.Tenant;
using NSubstitute;
using Xunit;

namespace AzureMcp.Tests.Areas.Monitor.UnitTests.Metrics;

public class ResourceResolverServiceTests
{
    private readonly IArmServiceClient _armServiceClient;
    private readonly ITenantService _tenantService;
    private readonly IIdentityServiceClient _credentialService;
    private readonly ResourceResolverService _service;

    public ResourceResolverServiceTests()
    {
        _armServiceClient = Substitute.For<IArmServiceClient>();
        _tenantService = Substitute.For<ITenantService>();
        _credentialService = Substitute.For<IIdentityServiceClient>();
        _service = new ResourceResolverService(_armServiceClient, _tenantService, _credentialService);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Act & Assert - Constructor should not throw
        var service = new ResourceResolverService(_armServiceClient, _tenantService, _credentialService);
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullSubscriptionService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ResourceResolverService(null!, _tenantService, _credentialService));
    }

    #endregion

    #region ResolveResourceIdAsync Tests

    [Fact]
    public async Task ResolveResourceIdAsync_WithFullResourceId_ReturnsDirectly()
    {
        // Arrange
        var fullResourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Storage/storageAccounts/test";
        var subscription = "87654321-4321-4321-4321-210987654321"; // Different subscription to ensure it's not used

        // Act
        var result = await _service.ResolveResourceIdAsync(subscription, null, null, fullResourceId);

        // Assert
        Assert.Equal(fullResourceId, result.ToString());
        // Verify that ARM service client was not called since we're passing a full resource ID
        await _armServiceClient.DidNotReceive().ResolveResourceIdAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveResourceIdAsync_WithResourceNameRequiringResolution_CallsArmService()
    {
        // Arrange
        var subscription = "12345678-1234-1234-1234-123456789012";
        var resourceGroup = "test-rg";
        var resourceType = "Microsoft.Storage/storageAccounts";
        var resourceName = "test";
        var expectedResourceId = $"/subscriptions/{subscription}/resourceGroups/{resourceGroup}/providers/{resourceType}/{resourceName}";

        _armServiceClient.ResolveResourceIdAsync(subscription, resourceGroup, resourceType, resourceName, null, Arg.Any<CancellationToken>())
            .Returns(expectedResourceId);

        // Act
        var result = await _service.ResolveResourceIdAsync(subscription, resourceGroup, resourceType, resourceName);

        // Assert
        Assert.Equal(expectedResourceId, result.ToString());
        await _armServiceClient.Received(1).ResolveResourceIdAsync(subscription, resourceGroup, resourceType, resourceName, null, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", "")]
    public async Task ResolveResourceIdAsync_WithNullOrEmptySubscription_ThrowsArgumentException(string? subscription, string? resourceName)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ResolveResourceIdAsync(subscription!, null, null, resourceName!));
    }

    [Theory]
    [InlineData(null, null)]
    public async Task ResolveResourceIdAsync_WithNullOrEmptySubscription_ThrowsArgumentNullException(string? subscription, string? resourceName)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.ResolveResourceIdAsync(subscription!, null, null, resourceName!));
    }

    [Fact]
    public async Task ResolveResourceIdAsync_WithValidInputs_CallsArmServiceAndReturnsResourceIdentifier()
    {
        // Arrange
        var subscription = "sub1";
        var resourceGroup = "rg1";
        var resourceType = "Microsoft.Storage/storageAccounts";
        var resourceName = "testresource";
        var tenant = "tenant1";
        var expectedResourceId = $"/subscriptions/{subscription}/resourceGroups/{resourceGroup}/providers/{resourceType}/{resourceName}";

        _armServiceClient.ResolveResourceIdAsync(subscription, resourceGroup, resourceType, resourceName, tenant, Arg.Any<CancellationToken>())
            .Returns(expectedResourceId);

        // Act
        var result = await _service.ResolveResourceIdAsync(subscription, resourceGroup, resourceType, resourceName, tenant);

        // Assert
        Assert.Equal(expectedResourceId, result.ToString());
        await _armServiceClient.Received(1).ResolveResourceIdAsync(subscription, resourceGroup, resourceType, resourceName, tenant, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveResourceIdAsync_WithMinimalInputs_CallsArmServiceWithNulls()
    {
        // Arrange
        var subscription = "sub1";
        var resourceName = "testresource";
        var expectedResourceId = $"/subscriptions/{subscription}/resourceGroups/discovered-rg/providers/Microsoft.Storage/storageAccounts/{resourceName}";

        _armServiceClient.ResolveResourceIdAsync(subscription, null, null, resourceName, null, Arg.Any<CancellationToken>())
            .Returns(expectedResourceId);

        // Act
        var result = await _service.ResolveResourceIdAsync(subscription, null, null, resourceName);

        // Assert
        Assert.Equal(expectedResourceId, result.ToString());
        await _armServiceClient.Received(1).ResolveResourceIdAsync(subscription, null, null, resourceName, null, Arg.Any<CancellationToken>());
    }

    #endregion
}
