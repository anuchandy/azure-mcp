// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Data.AppConfiguration;
using AzureMcp.Areas.AppConfig.Models;
using AzureMcp.LocalServiceClient.Arm;
using AzureMcp.LocalServiceClient.Identity;
using AzureMcp.Options;
using AzureMcp.Services.Azure;

namespace AzureMcp.Areas.AppConfig.Services;

public class AppConfigService(IArmServiceClient armService, IIdentityServiceClient credentialService) 
    : BaseAzureService(credentialService), IAppConfigService
{
    private readonly IArmServiceClient _armService = armService ?? throw new ArgumentNullException(nameof(armService));

    public async Task<List<AppConfigurationAccount>> GetAppConfigAccounts(string subscriptionId, string? tenant = null, RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscriptionId);

        return await _armService.GetAppConfigAccountsAsync(subscriptionId, tenant);
    }

    public async Task<List<KeyValueSetting>> ListKeyValues(
        string accountName,
        string subscriptionId,
        string? key = null,
        string? label = null,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(accountName, subscriptionId);

        var client = await GetConfigurationClient(accountName, subscriptionId, tenant, retryPolicy);
        var settings = new List<KeyValueSetting>();

        var selector = new SettingSelector
        {
            KeyFilter = string.IsNullOrEmpty(key) ? null : key,
            LabelFilter = string.IsNullOrEmpty(label) ? null : label
        };

        await foreach (var setting in client.GetConfigurationSettingsAsync(selector))
        {
            settings.Add(new KeyValueSetting
            {
                Key = setting.Key,
                Value = setting.Value,
                Label = setting.Label ?? string.Empty,
                ContentType = setting.ContentType ?? string.Empty,
                ETag = new ETag { Value = setting.ETag.ToString() },
                LastModified = setting.LastModified,
                Locked = setting.IsReadOnly
            });
        }

        return settings;
    }

    public async Task<KeyValueSetting> GetKeyValue(string accountName, string key, string subscriptionId, string? tenant = null, RetryPolicyOptions? retryPolicy = null, string? label = null)
    {
        ValidateRequiredParameters(accountName, key, subscriptionId);
        var client = await GetConfigurationClient(accountName, subscriptionId, tenant, retryPolicy);
        var response = await client.GetConfigurationSettingAsync(key, label, cancellationToken: default);
        var setting = response.Value;

        return new KeyValueSetting
        {
            Key = setting.Key,
            Value = setting.Value,
            Label = setting.Label ?? string.Empty,
            ContentType = setting.ContentType ?? string.Empty,
            ETag = new ETag { Value = setting.ETag.ToString() },
            LastModified = setting.LastModified,
            Locked = setting.IsReadOnly
        };
    }

    public async Task LockKeyValue(string accountName, string key, string subscriptionId, string? tenant = null, RetryPolicyOptions? retryPolicy = null, string? label = null)
    {
        await SetKeyValueReadOnlyState(accountName, key, subscriptionId, tenant, retryPolicy, label, true);
    }

    public async Task UnlockKeyValue(string accountName, string key, string subscriptionId, string? tenant = null, RetryPolicyOptions? retryPolicy = null, string? label = null)
    {
        await SetKeyValueReadOnlyState(accountName, key, subscriptionId, tenant, retryPolicy, label, false);
    }

    public async Task SetKeyValue(string accountName, string key, string value, string subscriptionId, string? tenant = null, RetryPolicyOptions? retryPolicy = null, string? label = null)
    {
        ValidateRequiredParameters(accountName, key, value, subscriptionId);
        var client = await GetConfigurationClient(accountName, subscriptionId, tenant, retryPolicy);
        await client.SetConfigurationSettingAsync(key, value, label, cancellationToken: default);
    }

    public async Task DeleteKeyValue(string accountName, string key, string subscriptionId, string? tenant = null, RetryPolicyOptions? retryPolicy = null, string? label = null)
    {
        ValidateRequiredParameters(accountName, key, subscriptionId);
        var client = await GetConfigurationClient(accountName, subscriptionId, tenant, retryPolicy);
        await client.DeleteConfigurationSettingAsync(key, label, cancellationToken: default);
    }

    private async Task SetKeyValueReadOnlyState(string accountName, string key, string subscriptionId, string? tenant, RetryPolicyOptions? retryPolicy, string? label, bool isReadOnly)
    {
        ValidateRequiredParameters(accountName, key, subscriptionId);
        var client = await GetConfigurationClient(accountName, subscriptionId, tenant, retryPolicy);
        await client.SetReadOnlyAsync(key, label, isReadOnly, cancellationToken: default);
    }

    private async Task<ConfigurationClient> GetConfigurationClient(string accountName, string subscriptionId, string? tenant, RetryPolicyOptions? retryPolicy)
    {
        var endpoint = await _armService.GetAppConfigAccountEndpointAsync(accountName, subscriptionId, tenant);
        var credential = await GetCredential(tenant);

        return new ConfigurationClient(new Uri(endpoint), credential);
    }

}
