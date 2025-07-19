// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Areas.Sql.Models;
using AzureMcp.LocalServiceClient.Arm;
using AzureMcp.Options;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Areas.Sql.Services;

public class SqlService(IArmServiceClient armServiceClient, ILogger<SqlService> logger) : ISqlService
{
    private readonly IArmServiceClient _armServiceClient = armServiceClient ?? throw new ArgumentNullException(nameof(armServiceClient));
    private readonly ILogger<SqlService> _logger = logger;

    public async Task<SqlDatabase?> GetDatabaseAsync(
        string serverName,
        string databaseName,
        string resourceGroup,
        string subscription,
        RetryPolicyOptions? retryPolicy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _armServiceClient.GetSqlDatabaseAsync(
                subscriptionId: subscription,
                resourceGroupName: resourceGroup,
                serverName: serverName,
                databaseName: databaseName,
                tenantId: null,
                cancellationToken: cancellationToken);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting SQL database. Server: {Server}, Database: {Database}, ResourceGroup: {ResourceGroup}, Subscription: {Subscription}",
                serverName, databaseName, resourceGroup, subscription);
            throw;
        }
    }

    public async Task<List<SqlServerEntraAdministrator>> GetEntraAdministratorsAsync(
        string serverName,
        string resourceGroup,
        string subscription,
        RetryPolicyOptions? retryPolicy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _armServiceClient.GetSqlEntraAdministratorsAsync(
                subscriptionId: subscription,
                resourceGroupName: resourceGroup,
                serverName: serverName,
                tenantId: null,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting SQL server Entra ID administrators. Server: {Server}, ResourceGroup: {ResourceGroup}, Subscription: {Subscription}",
                serverName, resourceGroup, subscription);
            throw;
        }
    }

    public async Task<List<SqlElasticPool>> GetElasticPoolsAsync(
        string serverName,
        string resourceGroup,
        string subscription,
        RetryPolicyOptions? retryPolicy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _armServiceClient.GetSqlElasticPoolsAsync(
                subscriptionId: subscription,
                resourceGroupName: resourceGroup,
                serverName: serverName,
                tenantId: null,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting SQL elastic pools. Server: {Server}, ResourceGroup: {ResourceGroup}, Subscription: {Subscription}",
                serverName, resourceGroup, subscription);
            throw;
        }
    }

    public async Task<List<SqlServerFirewallRule>> ListFirewallRulesAsync(
        string serverName,
        string resourceGroup,
        string subscription,
        RetryPolicyOptions? retryPolicy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _armServiceClient.ListSqlFirewallRulesAsync(
                subscriptionId: subscription,
                resourceGroupName: resourceGroup,
                serverName: serverName,
                tenantId: null,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting SQL server firewall rules. Server: {Server}, ResourceGroup: {ResourceGroup}, Subscription: {Subscription}",
                serverName, resourceGroup, subscription);
            throw;
        }
    }
}
