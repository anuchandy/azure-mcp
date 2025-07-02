// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Ext.Credential.Services;

var builder = WebApplication.CreateBuilder(args);

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
app.MapGrpcService<CredentialGrpcService>();

if (app.Environment.IsDevelopment())
{
    // gRPC reflection for dev.
    app.MapGrpcReflectionService();
}

app.MapGet("/", () => "Use a gRPC client to use AzureMcp.Ext.Credential.Grpc service");

var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:5000";
app.Logger.LogInformation("AzureMcp Credential gRPC Service configured for URLs: {Urls}", urls);

app.Run();