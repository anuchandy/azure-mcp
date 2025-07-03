// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Options;
using AzureMcp.LocalService.Arm.Services;
using AzureMcp.LocalService.Arm.Clients;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<Configuration>(config =>
{
    // Environment variable: AzureMcp__LocalService__Arm__IdentityServiceEndpoint
    config.IdentityServiceEndpoint = builder.Configuration["AzureMcp:LocalService:Arm:IdentityServiceEndpoint"] ?? string.Empty;
});

builder.Services.AddSingleton<IdentityClient>(serviceProvider =>
{
    var config = serviceProvider.GetRequiredService<IOptions<Configuration>>().Value;
    config.Validate();
    var logger = serviceProvider.GetRequiredService<ILogger<IdentityClient>>();
    return new IdentityClient(config.IdentityServiceEndpoint, logger);
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

builder.Services.AddGrpc();
builder.Services.AddLogging();
builder.Services.AddMemoryCache();

if (builder.Environment.IsDevelopment())
{
    // gRPC reflection for dev.
    builder.Services.AddGrpcReflection();
}

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();
app.MapGrpcService<ArmGrpcService>();

if (app.Environment.IsDevelopment())
{
    // gRPC reflection for dev.
    app.MapGrpcReflectionService();
}

app.MapGet("/", () => "AzureMcp ARM gRPC Service is running");
app.MapGet("/ishealthy", () => new { status = "healthy", timestamp = DateTime.UtcNow });

var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:5001";
app.Logger.LogInformation("AzureMcp ARM gRPC Service configured for URLs: {Urls}", urls);

app.Run();

/// <summary>
/// Configuration type for Arm LocalService.
/// </summary>
public class Configuration
{
    /// <summary>
    /// The gRPC endpoint URL for the Identity LocalService.
    /// </summary>
    public string IdentityServiceEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(IdentityServiceEndpoint))
        {
            throw new InvalidOperationException("IdentityServiceEndpoint must be configured");
        }

        if (!Uri.TryCreate(IdentityServiceEndpoint, UriKind.Absolute, out var uri) || 
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new InvalidOperationException($"IdentityServiceEndpoint must be a valid HTTP/HTTPS URL: {IdentityServiceEndpoint}");
        }
    }
}