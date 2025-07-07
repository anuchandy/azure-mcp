// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Options;
using AzureMcp.LocalService.CosmosDB.Services;
using AzureMcp.LocalService.CosmosDB.Clients;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<Configuration>(config =>
{
    // Environment variable: AzureMcp__LocalService__CosmosDB__ArmServiceEndpoint
    config.ArmServiceEndpoint = builder.Configuration["AzureMcp:LocalService:CosmosDB:ArmServiceEndpoint"] ?? string.Empty;
    // Environment variable: AzureMcp__LocalService__CosmosDB__IdentityServiceEndpoint
    config.IdentityServiceEndpoint = builder.Configuration["AzureMcp:LocalService:CosmosDB:IdentityServiceEndpoint"] ?? string.Empty;
});

builder.Services.AddSingleton<ArmServiceClient>(serviceProvider =>
{
    var config = serviceProvider.GetRequiredService<IOptions<Configuration>>().Value;
    Configuration.ValidateEndpoint(config.ArmServiceEndpoint, nameof(Configuration.ArmServiceEndpoint));
    var logger = serviceProvider.GetRequiredService<ILogger<ArmServiceClient>>();
    return new ArmServiceClient(config.ArmServiceEndpoint, logger);
});

builder.Services.AddSingleton<IdentityClient>(serviceProvider =>
{
    var config = serviceProvider.GetRequiredService<IOptions<Configuration>>().Value;
    Configuration.ValidateEndpoint(config.IdentityServiceEndpoint, nameof(Configuration.IdentityServiceEndpoint));
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
app.MapGrpcService<CosmosDBGrpcService>();

if (app.Environment.IsDevelopment())
{
    // gRPC reflection for dev.
    app.MapGrpcReflectionService();
}

app.MapGet("/", () => "AzureMcp CosmosDB gRPC Service is running");
app.MapGet("/ishealthy", () => new { status = "healthy", timestamp = DateTime.UtcNow });

var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:5003";
app.Logger.LogInformation("AzureMcp CosmosDB gRPC Service configured for URLs: {Urls}", urls);

app.Run();

/// <summary>
/// Configuration type for CosmosDB LocalService.
/// </summary>
public class Configuration
{
    /// <summary>
    /// The gRPC endpoint URL for the ARM LocalService.
    /// </summary>
    public string ArmServiceEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// The gRPC endpoint URL for the Identity LocalService.
    /// </summary>
    public string IdentityServiceEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Validates that an endpoint is configured and is a valid HTTP/HTTPS URL.
    /// </summary>
    /// <param name="endpoint">The endpoint URL to validate.</param>
    /// <param name="endpointName">The name of the endpoint for error messages.</param>
    /// <exception cref="InvalidOperationException">Thrown when the endpoint is invalid.</exception>
    public static void ValidateEndpoint(string endpoint, string endpointName)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException($"{endpointName} must be configured");
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || 
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new InvalidOperationException($"{endpointName} must be a valid HTTP/HTTPS URL: {endpoint}");
        }
    }
}