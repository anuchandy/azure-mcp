// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using AzureMcp.Models.Option;
using AzureMcp.Options.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace AzureMcp.Commands.Server;

[HiddenCommand]
public sealed class ServiceStartCommand : BaseCommand
{
    private const string _commandTitle = "Start MCP Server";
    private readonly Option<string> _transportOption = OptionDefinitions.Service.Transport;
    private readonly Option<int> _portOption = OptionDefinitions.Service.Port;

    public override string Name => "start";
    public override string Description => "Starts Azure MCP Server.";
    public override string Title => _commandTitle;

    protected override void RegisterOptions(Command command)
    {
        LogDebug("[ServiceStartCommand] RegisterOptions called");
        base.RegisterOptions(command);
        command.AddOption(_transportOption);
        command.AddOption(_portOption);
    }

    private static void LogDebug(string message)
    {
        var logPath = "/tmp/azmcp-server-debug.log";
        var logLine = $"{DateTime.UtcNow:O} {message}\n";
        System.IO.File.AppendAllText(logPath, logLine);
    }

    public override async Task<CommandResponse> ExecuteAsync(CommandContext context, ParseResult parseResult)
    {
#if DEBUG
        LogDebug("[ServiceStartCommand] ExecuteAsync called in debug mode");
        if (!System.Diagnostics.Debugger.IsAttached)
        {
            LogDebug("[ServiceStartCommand] Waiting for debugger to attach. PID: " + Environment.ProcessId);
            while (!System.Diagnostics.Debugger.IsAttached)
            {
                Thread.Sleep(100);
            }
            LogDebug("[ServiceStartCommand] Debugger Attached");
        }
#else
        LogDebug("[ServiceStartCommand] ExecuteAsync called in non-debug mode");
#endif

        var port = parseResult.GetValueForOption(_portOption) == default
            ? OptionDefinitions.Service.Port.GetDefaultValue()
            : parseResult.GetValueForOption(_portOption);

        var serverOptions = new ServiceStartOptions
        {
            Transport = parseResult.GetValueForOption(_transportOption) ?? TransportTypes.StdIo,
            Port = port
        };

        using var host = CreateHost(serverOptions);
        await host.StartAsync(CancellationToken.None);
        await host.WaitForShutdownAsync(CancellationToken.None);

        return context.Response;
    }

    private IHost CreateHost(ServiceStartOptions serverOptions)
    {
        LogDebug("[ServiceStartCommand] CreateHost called");

        if (serverOptions.Transport == TransportTypes.Sse)
        {
            var builder = WebApplication.CreateBuilder([]);
            Program.ConfigureServices(builder.Services);
            ConfigureMcpServer(builder.Services, serverOptions.Transport);

            builder.WebHost
                .ConfigureKestrel(server => server.ListenAnyIP(serverOptions.Port))
                .ConfigureLogging(logging =>
                {
                    logging.AddEventSourceLogger();
                });

            var application = builder.Build();

            application.MapMcp();

            return application;
        }
        else
        {
            return Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddEventSourceLogger();
                })
                .ConfigureServices(services =>
                {
                    Program.ConfigureServices(services);
                    ConfigureMcpServer(services, serverOptions.Transport);
                })
                .Build();
        }
    }

    private static void ConfigureMcpServer(IServiceCollection services, string transport)
    {
        LogDebug("[ServiceStartCommand] ConfigureMcpServer called");

        services.AddSingleton<ToolOperations>();
        services.AddSingleton<AzureEventSourceLogForwarder>();
        services.AddHostedService<OtelService>();

        services.AddOptions<McpServerOptions>()
            .Configure<ToolOperations>((mcpServerOptions, toolOperations) =>
            {
                var entryAssembly = Assembly.GetEntryAssembly();
                var assemblyName = entryAssembly?.GetName();
                var serverName = entryAssembly?.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "Azure MCP Server";

                mcpServerOptions.ServerInfo = new Implementation
                {
                    Name = serverName,
                    Version = assemblyName?.Version?.ToString() ?? "1.0.0-beta"
                };

                mcpServerOptions.Capabilities = new ServerCapabilities
                {
                    Tools = toolOperations.ToolsCapability
                };

                mcpServerOptions.ProtocolVersion = "2024-11-05";

            });

        var mcpServerBuilder = services.AddMcpServer();

        if (transport != TransportTypes.Sse)
        {
            mcpServerBuilder.WithStdioServerTransport();
        }
        else
        {
            mcpServerBuilder.WithHttpTransport();
        }
    }

    private sealed class StdioMcpServerHostedService(IMcpServer session) : BackgroundService
    {
        /// <inheritdoc />
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            LogDebug("[ServiceStartCommand.StdioMcpServerHostedService] ExecuteAsync called");
            return session.RunAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Resolves (and starts) the OpenTelemetry services.
    /// </summary>
    private sealed class OtelService : BackgroundService
    {
        private readonly MeterProvider? _meterProvider;
        private readonly TracerProvider? _tracerProvider;
        private readonly LoggerProvider? _loggerProvider;
        private readonly AzureEventSourceLogForwarder _logForwarder;

        public OtelService(IServiceProvider provider)
        {
            LogDebug("[ServiceStartCommand.OtelService] .ctor called");
            _meterProvider = provider.GetService<MeterProvider>();
            _tracerProvider = provider.GetService<TracerProvider>();
            _loggerProvider = provider.GetService<LoggerProvider>();
            _logForwarder = provider.GetRequiredService<AzureEventSourceLogForwarder>();
            _logForwarder.Start();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            LogDebug("[ServiceStartCommand.OtelService] ExecuteAsync called");
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            LogDebug("[ServiceStartCommand.OtelService] Dispose called");
            _meterProvider?.Dispose();
            _tracerProvider?.Dispose();
            _loggerProvider?.Dispose();
            _logForwarder.Dispose();
        }
    }
}
