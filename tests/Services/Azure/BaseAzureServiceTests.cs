// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using AzureMcp.LocalServiceClient.Identity;
using AzureMcp.Services.Azure;
using AzureMcp.Services.Azure.Tenant;
using NSubstitute;
using Xunit;

namespace AzureMcp.Tests.Services.Azure;

[Trait("Area", "Core")]
public class BaseAzureServiceTests
{
    private const string TenantId = "test-tenant-id";
    private const string TenantName = "test-tenant-name";

    private readonly ITenantService _tenantService = Substitute.For<ITenantService>();
    private readonly IIdentityServiceClient _credentialService = Substitute.For<IIdentityServiceClient>();
    private readonly TestAzureService _azureService;

    public BaseAzureServiceTests()
    {
        _azureService = new TestAzureService(_credentialService, _tenantService);
        _tenantService.GetTenantId(TenantName).Returns(TenantId);
    }

    [Fact]
    public async Task GetCredential_CreatesAndUsesCachedCredential()
    {
        // Arrange
        var tenantName2 = "Other-Tenant-Name";
        var tenantId2 = "Other-Tenant-Id";

        _tenantService.GetTenantId(tenantName2).Returns(tenantId2);

        // Act - Get credential for first tenant twice
        var credential1 = await _azureService.GetCredential(TenantName);
        var credential2 = await _azureService.GetCredential(TenantName);

        // Assert - Should return the same cached credential
        Assert.Equal(credential1, credential2);

        // Act - Get credential for different tenant
        var otherCredential = await _azureService.GetCredential(tenantName2);

        // Assert - Should be different credential
        Assert.NotEqual(credential1, otherCredential);

        // Verify the credential service was called appropriately
        await _credentialService.Received(1).GetCredentialAsync(TenantId, Arg.Any<CancellationToken>());
        await _credentialService.Received(1).GetCredentialAsync(tenantId2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveTenantIdAsync_ReturnsValueNoService()
    {
        var testAzureService = new TestAzureService(_credentialService, null);

        string? actual = await testAzureService.ResolveTenantId(TenantName);
        Assert.Equal(TenantName, actual);

        string? actual2 = await testAzureService.ResolveTenantId(null);
        Assert.Null(actual2);
    }

    private sealed class TestAzureService(IIdentityServiceClient credentialService, ITenantService? tenantService = null) : BaseAzureService(credentialService, tenantService)
    {
        public new Task<TokenCredential> GetCredential(string? tenant = null) =>
            base.GetCredential(tenant);

        public Task<string?> ResolveTenantId(string? tenant) => ResolveTenantIdAsync(tenant);
    }
}
