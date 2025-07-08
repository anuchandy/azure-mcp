// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using AzureMcp.Areas.Server.Commands.Discovery;
using AzureMcp.Areas.Server.Commands.Runtime;
using AzureMcp.Areas.Server.Commands.ToolLoading;
using AzureMcp.Areas.Server.Options;
using AzureMcp.LocalServiceClient.Arm;
using AzureMcp.LocalServiceClient.CosmosDB;
using AzureMcp.LocalServiceClient.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Azure MCP server services.
/// </summary>
public static class AzureMcpServiceCollectionExtensions
{
    private const string DefaultServerName = "Azure MCP Server";

    /// <summary>
    /// Adds the Azure MCP server services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="serviceStartOptions">The options for configuring the server.</param>
    /// <returns>The service collection with MCP server services added.</returns>
    public static IServiceCollection AddAzureMcpServer(this IServiceCollection services, ServiceStartOptions serviceStartOptions)
    {
        // Register the LocalServiceClient services
        AddLocalServiceClients(services);

        // Register options for service start
        services.AddSingleton(serviceStartOptions);
        services.AddSingleton(Options.Options.Create(serviceStartOptions));

        // Register tool loader strategies
        services.AddSingleton<CommandFactoryToolLoader>();
        services.AddSingleton(sp =>
        {
            return new RegistryToolLoader(
                sp.GetRequiredService<RegistryDiscoveryStrategy>(),
                sp.GetRequiredService<IOptions<ServiceStartOptions>>(),
                sp.GetRequiredService<ILogger<RegistryToolLoader>>()
            );
        });

        services.AddSingleton<SingleProxyToolLoader>();
        services.AddSingleton<CompositeToolLoader>();
        services.AddSingleton<ServerToolLoader>();

        // Register server discovery strategies
        services.AddSingleton<CommandGroupDiscoveryStrategy>();
        services.AddSingleton<CompositeDiscoveryStrategy>();
        services.AddSingleton<RegistryDiscoveryStrategy>();

        // Register server providers
        services.AddSingleton<CommandGroupServerProvider>();
        services.AddSingleton<RegistryServerProvider>();

        // Register MCP runtimes
        services.AddSingleton<IMcpRuntime, McpRuntime>();

        // Register MCP discovery strategies based on proxy mode
        if (serviceStartOptions.Mode == ModeTypes.SingleToolProxy || serviceStartOptions.Mode == ModeTypes.NamespaceProxy)
        {
            services.AddSingleton<IMcpDiscoveryStrategy>(sp =>
            {
                var discoveryStrategies = new List<IMcpDiscoveryStrategy>
                {
                    sp.GetRequiredService<RegistryDiscoveryStrategy>(),
                    sp.GetRequiredService<CommandGroupDiscoveryStrategy>(),
                };

                return new CompositeDiscoveryStrategy(discoveryStrategies);
            });
        }

        // Configure tool loading based on mode
        if (serviceStartOptions.Mode == ModeTypes.SingleToolProxy)
        {
            services.AddSingleton<IToolLoader, SingleProxyToolLoader>();
        }
        else if (serviceStartOptions.Mode == ModeTypes.NamespaceProxy)
        {
            services.AddSingleton<IToolLoader, ServerToolLoader>();
        }
        else
        {
            services.AddSingleton<IMcpDiscoveryStrategy, RegistryDiscoveryStrategy>();
            services.AddSingleton<IToolLoader>(sp =>
            {
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var toolLoaders = new List<IToolLoader>
                {
                    sp.GetRequiredService<RegistryToolLoader>(),
                    sp.GetRequiredService<CommandFactoryToolLoader>(),
                };

                return new CompositeToolLoader(toolLoaders, loggerFactory.CreateLogger<CompositeToolLoader>());
            });
        }

        var mcpServerOptions = services
            .AddOptions<McpServerOptions>()
            .Configure<IMcpRuntime>((mcpServerOptions, mcpRuntime) =>
            {
                var mcpServerOptionsBuilder = services.AddOptions<McpServerOptions>();
                var entryAssembly = Assembly.GetEntryAssembly();
                var assemblyName = entryAssembly?.GetName();
                var serverName = entryAssembly?.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? DefaultServerName;

                mcpServerOptions.ProtocolVersion = "2024-11-05";
                mcpServerOptions.ServerInfo = new Implementation
                {
                    Name = serverName,
                    Version = assemblyName?.Version?.ToString() ?? "1.0.0-beta"
                };

                mcpServerOptions.Capabilities = new ServerCapabilities
                {
                    Tools = new ToolsCapability()
                    {
                        CallToolHandler = mcpRuntime.CallToolHandler,
                        ListToolsHandler = mcpRuntime.ListToolsHandler,
                    }
                };
            });

        var mcpServerBuilder = services.AddMcpServer();
        mcpServerBuilder.WithStdioServerTransport();

        return services;
    }

    private static void AddLocalServiceClients(IServiceCollection services)
    {
        services.AddSingleton<IIdentityServiceClient>(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            return new IdentityServiceClient(loggerFactory);
        });

        services.AddSingleton<IArmServiceClient>(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var identityServiceClient = provider.GetRequiredService<IIdentityServiceClient>();
            return new ArmServiceClient(loggerFactory, identityServiceClient);
        });

        services.AddSingleton<ICosmosDBServiceClient>(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var identityServiceClient = provider.GetRequiredService<IIdentityServiceClient>();
            var armServiceClient = provider.GetRequiredService<IArmServiceClient>();
            return new CosmosDBServiceClient(loggerFactory, identityServiceClient, armServiceClient);
        });
    }
}
