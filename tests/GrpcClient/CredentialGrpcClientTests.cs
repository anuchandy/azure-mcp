// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Azure.Core;
using AzureMcp.GrpcClient;

namespace AzureMcp.Tests.GrpcClient;

public class CredentialGrpcClientTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<CredentialGrpcClientTests> _logger;
    private GrpcServiceHost? _serviceHost;
    private CredentialGrpcClient? _credentialClient;

    public CredentialGrpcClientTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = _loggerFactory.CreateLogger<CredentialGrpcClientTests>();
    }

    [Fact]
    public async Task ServiceHostHealthCheckWorks()
    {
        // Arrange
        var testAssemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var testDirectory = Path.GetDirectoryName(testAssemblyLocation)!;

        var config = new GrpcServiceConfig
        {
            ServiceName = "Credential",
            ExtensionPath = testDirectory,
            ExecutableNames = new[]
            {
                "AzureMcp.Ext.Credential.exe",
                "AzureMcp.Ext.Credential"
            },
            StartupTimeoutSeconds = 10
        };

        var logger = _loggerFactory.CreateLogger<GrpcServiceHost>();
        _serviceHost = new GrpcServiceHost(logger, config);

        // Act
        var endpointUrl = await _serviceHost.StartServiceAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(endpointUrl);
        Assert.StartsWith("http://localhost:", endpointUrl);
        Assert.True(_serviceHost.IsRunning);

        // Check health endpoint
        Assert.NotNull(_serviceHost.EndpointUrl);
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestVersion = new Version(2, 0);
        httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        var healthResponse = await httpClient.GetAsync($"{endpointUrl}/ishealthy", TestContext.Current.CancellationToken);
        Assert.True(healthResponse.IsSuccessStatusCode);
        var healthContent = await healthResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("healthy", healthContent);

        // Check if we can get a credential
        var credentialClient = new CredentialGrpcClient(_loggerFactory, _serviceHost);
        var credential = await credentialClient.GetCredentialAsync(tenantId: null, TestContext.Current.CancellationToken);
        Assert.NotNull(credential);
        Assert.IsAssignableFrom<TokenCredential>(credential);
        Assert.True(_serviceHost.IsRunning);
    }

    [Fact]
    public async Task CanAttemptToGetTokenThroughGrpcCredential()
    {
        // Arrange
        var testAssemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var testDirectory = Path.GetDirectoryName(testAssemblyLocation)!;
        
        var config = new GrpcServiceConfig
        {
            ServiceName = "Credential",
            ExtensionPath = testDirectory,
            ExecutableNames = new[]
            {
                "AzureMcp.Ext.Credential.exe",
                "AzureMcp.Ext.Credential"
            },
            StartupTimeoutSeconds = 10
        };

        var logger = _loggerFactory.CreateLogger<GrpcServiceHost>();
        _serviceHost = new GrpcServiceHost(logger, config);
        _credentialClient = new CredentialGrpcClient(_loggerFactory, _serviceHost);

        // Act
        var credential = await _credentialClient.GetCredentialAsync(tenantId: null, TestContext.Current.CancellationToken);
        
        var tokenRequestContext = new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/.default" });

        try
        {
            var tokenResult = await credential.GetTokenAsync(tokenRequestContext, TestContext.Current.CancellationToken);
            Assert.NotNull(tokenResult.Token);
            Assert.True(tokenResult.ExpiresOn > DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Likely authentication failure: {Message}", ex.Message);
            Assert.Fail($"Expected authentication to succeed, but got exception: {ex.GetType().Name}: {ex.Message}");
        }
        Assert.NotNull(credential);
        Assert.IsAssignableFrom<TokenCredential>(credential);
        Assert.True(_serviceHost.IsRunning);
    }

    public void Dispose()
    {
        _credentialClient?.Dispose();
        _serviceHost?.Dispose();
        _loggerFactory?.Dispose();
    }
}
