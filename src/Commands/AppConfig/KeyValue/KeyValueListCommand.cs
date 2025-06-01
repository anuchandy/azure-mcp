// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Models.AppConfig;
using AzureMcp.Models.Option;
using AzureMcp.Options.AppConfig.KeyValue;
using AzureMcp.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Commands.AppConfig.KeyValue;

public sealed class KeyValueListCommand(ILogger<KeyValueListCommand> logger) : BaseAppConfigCommand<KeyValueListOptions>()
{
    private const string _commandTitle = "List App Configuration Key-Value Settings";
    private readonly ILogger<KeyValueListCommand> _logger = logger;

    // KeyValueList has different key and label descriptions, which is why we are defining here instead of using BaseKeyValueCommand
    private readonly Option<string> _keyOption = OptionDefinitions.AppConfig.KeyValueList.Key;
    private readonly Option<string> _labelOption = OptionDefinitions.AppConfig.KeyValueList.Label;

    public override string Name => "list";

    public override string Description =>
        """
        List all key-values in an App Configuration store. This command retrieves and displays all key-value pairs
        from the specified store. Each key-value includes its key, value, label, content type, ETag, last modified
        time, and lock status.
        """;

    public override string Title => _commandTitle;

    private static void LogDebug(string message)
    {
        var logPath = "/tmp/azmcp-server-debug.log";
        var logLine = $"{DateTime.UtcNow:O} [KeyValueListCommand] {message}\n";
        System.IO.File.AppendAllText(logPath, logLine);
    }

    protected override void RegisterOptions(Command command)
    {
        LogDebug("RegisterOptions called");
        base.RegisterOptions(command);
        command.AddOption(_keyOption);
        command.AddOption(_labelOption);
    }

    protected override KeyValueListOptions BindOptions(ParseResult parseResult)
    {
        LogDebug("BindOptions called");
        var options = base.BindOptions(parseResult);
        options.Key = parseResult.GetValueForOption(_keyOption);
        options.Label = parseResult.GetValueForOption(_labelOption);
        return options;
    }

    [McpServerTool(Destructive = false, ReadOnly = true, Title = _commandTitle)]
    public override async Task<CommandResponse> ExecuteAsync(CommandContext context, ParseResult parseResult)
    {
        LogDebug("ExecuteAsync called");
        var options = BindOptions(parseResult);
        try
        {
            if (!Validate(parseResult.CommandResult, context.Response).IsValid)
            {
                LogDebug("Validation failed");
                return context.Response;
            }

            var appConfigService = context.GetService<IAppConfigService>();
            LogDebug("Calling appConfigService.ListKeyValues");
            var settings = await appConfigService.ListKeyValues(
                options.Account!,
                options.Subscription!,
                options.Key,
                options.Label,
                options.Tenant,
                options.RetryPolicy);
            LogDebug($"ListKeyValues returned {settings?.Count ?? 0} settings");
            context.Response.Results = settings?.Count > 0 ?
                ResponseResult.Create(
                    new KeyValueListCommandResult(settings),
                    AppConfigJsonContext.Default.KeyValueListCommandResult) :
                null;
        }
        catch (Exception ex)
        {
            LogDebug($"Exception: {ex}");
            _logger.LogError("An exception occurred processing command. Exception: {Exception}", ex);
            HandleException(context.Response, ex);
        }
        return context.Response;
    }

    internal record KeyValueListCommandResult(List<KeyValueSetting> Settings);
}
