// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace AzureMcp.LocalServiceClient;

/// <summary>
/// Host to start and manage a gRPC service.
/// </summary>
public sealed class GrpcServiceHost : IDisposable
{
    private readonly ILogger<GrpcServiceHost> _logger;
    private readonly GrpcServiceConfig _config;
    private Process? _serviceProcess;
    private string? _serviceEndpoint;
    private bool _disposed;

    public GrpcServiceHost(ILogger<GrpcServiceHost> logger, GrpcServiceConfig config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public string? EndpointUrl => _serviceEndpoint;

    public bool IsRunning => _serviceProcess is { HasExited: false };

    public string ServiceName => _config.ServiceName;

    public async Task<string> StartServiceAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return _serviceEndpoint!;
        }

        var port = GetAvailablePort();
        _serviceEndpoint = $"http://localhost:{port}";

        var executablePath = GetServiceExecutablePath();
        _logger.LogInformation("Starting {ServiceName} service at {EndpointUrl}", _config.ServiceName, _serviceEndpoint);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.Environment["ASPNETCORE_URLS"] = _serviceEndpoint;
        if (_config.EnvironmentVariables != null)
        {
            foreach (var kvp in _config.EnvironmentVariables)
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }
        }

        try
        {
            _serviceProcess = Process.Start(startInfo);
            if (_serviceProcess == null)
            {
                throw new InvalidOperationException($"Failed to start {_config.ServiceName} process");
            }
            await WaitForServiceReadyAsync(cancellationToken);
            _logger.LogInformation("{ServiceName} service started successfully on {EndpointUrl}", _config.ServiceName, _serviceEndpoint);
            return _serviceEndpoint;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start {ServiceName} service", _config.ServiceName);
            await StopServiceAsync();
            throw;
        }
    }

    public async Task StopServiceAsync()
    {
        if (_serviceProcess != null)
        {
            try
            {
                _logger.LogInformation("Stopping {ServiceName} service", _config.ServiceName);

                if (!_serviceProcess.HasExited)
                {
                    _serviceProcess.Kill();
                    await _serviceProcess.WaitForExitAsync();
                }
                _serviceProcess.Dispose();
                _serviceProcess = null;
                _serviceEndpoint = null;
                _logger.LogInformation("{ServiceName} service stopped", _config.ServiceName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping {ServiceName} service", _config.ServiceName);
            }
        }
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private string GetServiceExecutablePath()
    {
        // Get the directory of the current executable
        var currentDirectory = AppContext.BaseDirectory;
        
        // The service extension should be in the specified path
        var servicePath = Path.Combine(currentDirectory, _config.ExtensionPath);
        
        // Get executable names to search for
        var executableNames = _config.ExecutableNames ?? GetDefaultExecutableNames();

        foreach (var name in executableNames)
        {
            var fullPath = Path.Combine(servicePath, name);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        throw new FileNotFoundException($"{_config.ServiceName} executable not found in {servicePath}. Searched for: {string.Join(", ", executableNames)}");
    }

    private string[] GetDefaultExecutableNames()
    {
        var extensionName = Path.GetFileName(_config.ExtensionPath);
        return new[]
        {
            $"{extensionName}.exe",
            extensionName 
        };
    }

    private async Task WaitForServiceReadyAsync(CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(2);
        httpClient.DefaultRequestVersion = new Version(2, 0);
        httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;

        var maxAttempts = _config.StartupTimeoutSeconds;
        var delayBetweenAttempts = TimeSpan.FromSeconds(1);
        var healthUrl = $"{_serviceEndpoint}{_config.HealthEndpoint}";

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await httpClient.GetAsync(healthUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Service not ready yet, wait and try again.
            }

            if (attempt < maxAttempts - 1)
            {
                await Task.Delay(delayBetweenAttempts, cancellationToken);
            }
        }
        throw new TimeoutException($"{_config.ServiceName} service did not become ready within {maxAttempts} seconds");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopServiceAsync().GetAwaiter().GetResult();
            _disposed = true;
        }
    }
}
