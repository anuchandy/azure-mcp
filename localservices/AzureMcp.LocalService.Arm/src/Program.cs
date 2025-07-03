// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

builder.Services.AddGrpc();
builder.Services.AddLogging();

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

// TODO: Add MapGrpcService calls here when gRPC services are implemented

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