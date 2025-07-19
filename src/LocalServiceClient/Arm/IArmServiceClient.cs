// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Areas.AppConfig.Models;
using AzureMcp.Areas.Redis.Models.CacheForRedis;
using AzureMcp.Commands.Kusto;
using AzureMcp.Models.ResourceGroup;

namespace AzureMcp.LocalServiceClient.Arm;

/// <summary>
/// Service for managing Azure Resource Manager operations via gRPC.
/// </summary>
public interface IArmServiceClient : IServiceClient
{
    /// <summary>
    /// Gets the status of the Identity service connectivity.
    /// </summary>
    /// <param name="tenantId">Optional tenant ID to test with</param>
    /// <param name="scopes">Optional scopes to test with</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response indicating the Identity service status</returns>
    Task<IdentityServiceStatusResult> GetIdentityServiceStatusAsync(
        string? tenantId = null, 
        string[]? scopes = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all accessible tenants.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of tenant data</returns>
    Task<List<TenantData>> GetTenantsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all accessible subscriptions for the specified tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant ID to list subscriptions for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of subscription data</returns>
    Task<List<SubscriptionData>> ListSubscriptionsAsync(
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific subscription by ID or name.
    /// </summary>
    /// <param name="subscription">The subscription ID or name to retrieve</param>
    /// <param name="tenantId">Optional tenant ID to search within specific tenant</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The subscription data if found</returns>
    Task<SubscriptionData> GetSubscriptionAsync(
        string subscription,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets storage accounts for a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID to get storage accounts for</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of storage account names</returns>
    Task<List<string>> GetStorageAccountsAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets storage account keys for a specific storage account.
    /// </summary>
    /// <param name="accountName">Storage account name</param>
    /// <param name="subscriptionId">Subscription ID where the storage account exists</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The first storage account key value</returns>
    Task<string> GetStorageAccountKeysAsync(
        string accountName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets connection string for a specific storage account.
    /// </summary>
    /// <param name="accountName">Storage account name</param>
    /// <param name="subscriptionId">Subscription ID where the storage account exists</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The storage account connection string</returns>
    Task<string> GetStorageAccountConnectionStringAsync(
        string accountName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets Cosmos DB accounts for a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID to get Cosmos DB accounts for</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of Cosmos DB account names</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<List<string>> GetCosmosAccountsAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific Cosmos DB account details.
    /// </summary>
    /// <param name="accountName">Cosmos DB account name</param>
    /// <param name="subscriptionId">Subscription ID where the Cosmos DB account exists</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The Cosmos DB account details</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<CosmosAccountData> GetCosmosAccountAsync(
        string accountName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets App Configuration accounts for a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID to get App Configuration accounts for</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of App Configuration accounts</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<List<AppConfigurationAccount>> GetAppConfigAccountsAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the endpoint for a specific App Configuration account.
    /// </summary>
    /// <param name="accountName">App Configuration account name</param>
    /// <param name="subscriptionId">Subscription ID where the App Configuration account exists</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The App Configuration account endpoint</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<string> GetAppConfigAccountEndpointAsync(
        string accountName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets Kusto clusters for a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID to get Kusto clusters for</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of Kusto cluster names</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<List<string>> GetKustoClustersAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific Kusto cluster details.
    /// </summary>
    /// <param name="clusterName">Kusto cluster name</param>
    /// <param name="subscriptionId">Subscription ID where the Kusto cluster exists</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The Kusto cluster details</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<KustoClusterResourceProxy> GetKustoClusterAsync(
        string clusterName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all resource groups for a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID to get resource groups for</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of resource groups</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<List<ResourceGroupInfo>> GetResourceGroupsAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific resource group.
    /// </summary>
    /// <param name="resourceGroupName">Resource group name</param>
    /// <param name="subscriptionId">Subscription ID where the resource group exists</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The resource group details</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<ResourceGroupInfo> GetResourceGroupAsync(
        string resourceGroupName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists role assignments for a scope.
    /// </summary>
    /// <param name="scope">The scope that the role assignments apply to</param>
    /// <param name="tenantId">Optional tenant ID for cross-tenant operations</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of role assignments</returns>
    Task<List<AzureMcp.Areas.Authorization.Models.RoleAssignment>> ListRoleAssignmentsAsync(
        string scope,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Redis caches for a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID to get Redis caches for</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of Redis caches</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<List<Cache>> ListRedisCachesAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Redis access policy assignments for a cache.
    /// </summary>
    /// <param name="cacheName">Redis cache name</param>
    /// <param name="resourceGroupName">Resource group name containing the cache</param>
    /// <param name="subscriptionId">Subscription ID where the cache exists</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of access policy assignments</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<List<AccessPolicyAssignment>> ListRedisAccessPolicyAssignmentsAsync(
        string cacheName,
        string resourceGroupName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Redis clusters for a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID to get Redis clusters for</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of Redis clusters</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<List<AzureMcp.Areas.Redis.Models.ManagedRedis.Cluster>> ListRedisClustersAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Redis databases for a cluster.
    /// </summary>
    /// <param name="clusterName">Redis cluster name</param>
    /// <param name="resourceGroupName">Resource group name containing the cluster</param>
    /// <param name="subscriptionId">Subscription ID where the cluster exists</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of Redis databases</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<List<AzureMcp.Areas.Redis.Models.ManagedRedis.Database>> ListRedisDatabasesAsync(
        string clusterName,
        string resourceGroupName,
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists PostgreSQL flexible servers in a resource group.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the servers exist</param>
    /// <param name="resourceGroupName">Resource group name containing the servers</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of PostgreSQL server names</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<List<string>> ListPostgreSqlServersAsync(
        string subscriptionId,
        string resourceGroupName,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets PostgreSQL server configuration details.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the server exists</param>
    /// <param name="resourceGroupName">Resource group name containing the server</param>
    /// <param name="serverName">PostgreSQL server name</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The PostgreSQL server configuration as a formatted string</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<string> GetPostgreSqlServerConfigAsync(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific PostgreSQL server configuration parameter value.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the server exists</param>
    /// <param name="resourceGroupName">Resource group name containing the server</param>
    /// <param name="serverName">PostgreSQL server name</param>
    /// <param name="parameterName">Configuration parameter name</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The parameter value</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<string> GetPostgreSqlServerParameterAsync(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string parameterName,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a specific PostgreSQL server configuration parameter value.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the server exists</param>
    /// <param name="resourceGroupName">Resource group name containing the server</param>
    /// <param name="serverName">PostgreSQL server name</param>
    /// <param name="parameterName">Configuration parameter name</param>
    /// <param name="parameterValue">Configuration parameter value to set</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A success message indicating the parameter was updated</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<string> SetPostgreSqlServerParameterAsync(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string parameterName,
        string parameterValue,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a SQL Server database details.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the server exists</param>
    /// <param name="resourceGroupName">Resource group name containing the server</param>
    /// <param name="serverName">SQL server name</param>
    /// <param name="databaseName">Database name</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The SQL database details</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<AzureMcp.Areas.Sql.Models.SqlDatabase> GetSqlDatabaseAsync(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string databaseName,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets SQL Server Entra ID administrators for the specified server.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the SQL server exists</param>
    /// <param name="resourceGroupName">Resource group name containing the SQL server</param>
    /// <param name="serverName">SQL server name</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of SQL Server Entra ID administrators</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<List<AzureMcp.Areas.Sql.Models.SqlServerEntraAdministrator>> GetSqlEntraAdministratorsAsync(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets SQL Server elastic pools for the specified server.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the SQL server exists</param>
    /// <param name="resourceGroupName">Resource group name containing the SQL server</param>
    /// <param name="serverName">SQL server name</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of SQL Server elastic pools</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<List<AzureMcp.Areas.Sql.Models.SqlElasticPool>> GetSqlElasticPoolsAsync(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists SQL Server firewall rules for the specified server.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the SQL server exists</param>
    /// <param name="resourceGroupName">Resource group name containing the SQL server</param>
    /// <param name="serverName">SQL server name</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of SQL Server firewall rules</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<List<AzureMcp.Areas.Sql.Models.SqlServerFirewallRule>> ListSqlFirewallRulesAsync(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets load testing resources from a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the load testing resources exist</param>
    /// <param name="resourceGroup">Optional resource group name to filter resources</param>
    /// <param name="testResourceName">Optional specific test resource name to retrieve</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of load testing resources</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<List<AzureMcp.Areas.LoadTesting.Models.LoadTestingResource.TestResource>> GetLoadTestResourcesAsync(
        string subscriptionId,
        string? resourceGroup = null,
        string? testResourceName = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a load testing resource.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the load testing resource will be created</param>
    /// <param name="resourceGroup">Resource group name where the resource will be created</param>
    /// <param name="testResourceName">Optional specific test resource name. If not provided, will be auto-generated</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created or updated load testing resource</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<AzureMcp.Areas.LoadTesting.Models.LoadTestingResource.TestResource> CreateOrUpdateLoadTestingResourceAsync(
        string subscriptionId,
        string resourceGroup,
        string? testResourceName = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Azure Search services in a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the Search services exist</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of Search service names</returns>
    Task<List<string>> ListSearchServicesAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Grafana workspaces in a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID where the Grafana workspaces exist</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of Grafana workspaces</returns>
    Task<List<AzureMcp.Areas.Grafana.Models.Workspace.Workspace>> ListGrafanaWorkspacesAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists monitored resources for a Datadog monitor.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID containing the Datadog resource</param>
    /// <param name="resourceGroupName">Resource group name containing the Datadog resource</param>
    /// <param name="datadogResourceName">Name of the Datadog resource</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of monitored resource names</returns>
    Task<List<string>> ListMonitoredDatadogResourcesAsync(
        string subscriptionId,
        string resourceGroupName,
        string datadogResourceName,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Monitor workspaces for a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID or name</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of Monitor workspaces</returns>
    Task<List<MonitorWorkspaceInfo>> ListMonitorWorkspacesAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Monitor tables for a workspace.
    /// </summary>
    /// <param name="subscriptionId">The subscription ID or name</param>
    /// <param name="resourceGroupName">The resource group name</param>
    /// <param name="workspaceName">The workspace name or ID</param>
    /// <param name="tableType">Optional table type filter (defaults to "CustomLog")</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of Monitor table names</returns>
    Task<List<string>> ListMonitorTablesAsync(
        string subscriptionId,
        string resourceGroupName,
        string workspaceName,
        string? tableType = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Monitor table types for a workspace.
    /// </summary>
    /// <param name="subscriptionId">The subscription ID or name</param>
    /// <param name="resourceGroupName">The resource group name</param>
    /// <param name="workspaceName">The workspace name or ID</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of distinct Monitor table types</returns>
    Task<List<string>> ListMonitorTableTypesAsync(
        string subscriptionId,
        string resourceGroupName,
        string workspaceName,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deploys a model to Azure Cognitive Services.
    /// </summary>
    /// <param name="deploymentName">The deployment name</param>
    /// <param name="modelName">The model name</param>
    /// <param name="modelFormat">The model format (e.g., OpenAI)</param>
    /// <param name="azureAiServicesName">The Azure AI Services account name</param>
    /// <param name="resourceGroup">The resource group name</param>
    /// <param name="subscriptionId">The subscription ID</param>
    /// <param name="modelVersion">Optional model version</param>
    /// <param name="modelSource">Optional model source</param>
    /// <param name="skuName">Optional SKU name</param>
    /// <param name="skuCapacity">Optional SKU capacity</param>
    /// <param name="scaleType">Optional scale type</param>
    /// <param name="scaleCapacity">Optional scale capacity</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deployment result as JSON string</returns>
    Task<string> DeployModelAsync(
        string deploymentName,
        string modelName,
        string modelFormat,
        string azureAiServicesName,
        string resourceGroup,
        string subscriptionId,
        string? modelVersion = null,
        string? modelSource = null,
        string? skuName = null,
        int? skuCapacity = null,
        string? scaleType = null,
        int? scaleCapacity = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a resource identifier from provided parameters.
    /// </summary>
    /// <param name="subscription">The subscription ID</param>
    /// <param name="resourceGroup">The resource group name (optional)</param>
    /// <param name="resourceType">The resource type (optional, e.g., 'Microsoft.Storage/storageAccounts')</param>
    /// <param name="resourceName">The resource name or full resource ID</param>
    /// <param name="tenant">Optional tenant ID for multi-tenant scenarios</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The resolved Azure resource ID</returns>
    Task<string> ResolveResourceIdAsync(
        string subscription,
        string? resourceGroup,
        string? resourceType,
        string resourceName,
        string? tenant = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all AKS (Azure Kubernetes Service) clusters in a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID to list clusters for</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of AKS clusters</returns>
    /// <exception cref="LocalServiceCallException">Thrown when the operation fails</exception>
    Task<List<AzureMcp.Areas.Aks.Models.Cluster>> ListAksClustersAsync(
        string subscriptionId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of checking Identity service status.
/// </summary>
public class IdentityServiceStatusResult
{
    /// <summary>
    /// Whether the Identity service check was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if check failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Additional details about the check.
    /// </summary>
    public string? Details { get; init; }
}

/// <summary>
/// Subscription information.
/// </summary>
public class SubscriptionData
{
    /// <summary>
    /// The subscription ID (GUID).
    /// </summary>
    public required string SubscriptionId { get; init; }

    /// <summary>
    /// The display name of the subscription.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The tenant ID this subscription belongs to.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// The subscription state (e.g., "Enabled", "Disabled").
    /// </summary>
    public required string State { get; init; }
}



/// <summary>
/// Cosmos DB account information.
/// </summary>
public class CosmosAccountData
{
    /// <summary>
    /// The account name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The account ID (resource ID).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The account location.
    /// </summary>
    public required string Location { get; init; }

    /// <summary>
    /// The account type (e.g., "DocumentDB").
    /// </summary>
    public required string AccountType { get; init; }

    /// <summary>
    /// The resource group name.
    /// </summary>
    public required string ResourceGroup { get; init; }

    /// <summary>
    /// The provisioning state.
    /// </summary>
    public required string ProvisioningState { get; init; }

    /// <summary>
    /// The document endpoint URL.
    /// </summary>
    public required string DocumentEndpoint { get; init; }

    /// <summary>
    /// The primary master key for the account.
    /// </summary>
    public required string PrimaryMasterKey { get; init; }
}

/// <summary>
/// Information about a Monitor workspace.
/// </summary>
public class MonitorWorkspaceInfo
{
    /// <summary>
    /// Workspace name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Workspace customer ID (GUID).
    /// </summary>
    public string CustomerId { get; init; } = string.Empty;

    /// <summary>
    /// Workspace ARM resource ID.
    /// </summary>
    public string ArmId { get; init; } = string.Empty;
}

/// <summary>
/// Tenant information.
/// </summary>
public class TenantData
{
    /// <summary>
    /// The fully qualified ID of the tenant (e.g., /tenants/8d65815f-a5b6-402f-9298-045155da7d74).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The tenant ID (GUID).
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Category of the tenant (as string value).
    /// </summary>
    public required string TenantCategory { get; init; }

    /// <summary>
    /// Country/region name of the address for the tenant.
    /// </summary>
    public required string Country { get; init; }

    /// <summary>
    /// Country/region abbreviation for the tenant.
    /// </summary>
    public required string CountryCode { get; init; }

    /// <summary>
    /// The display name of the tenant.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The list of domains for the tenant.
    /// </summary>
    public required List<string> Domains { get; init; }

    /// <summary>
    /// The default domain for the tenant.
    /// </summary>
    public required string DefaultDomain { get; init; }

    /// <summary>
    /// The tenant type (only available for 'Home' tenant category).
    /// </summary>
    public required string TenantType { get; init; }

    /// <summary>
    /// The tenant's branding logo URL (only available for 'Home' tenant category).
    /// </summary>
    public required string TenantBrandingLogoUri { get; init; }
}

