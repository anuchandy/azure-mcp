// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using AzureMcp.Areas.Cosmos.Exceptions;
using AzureMcp.LocalServiceClient.Identity;
using AzureMcp.LocalServiceClient.Arm;
using AzureMcp.LocalServiceClient.CosmosDB;
using AzureMcp.Options;
using AzureMcp.Services.Azure;
using AzureMcp.Services.Azure.Tenant;
using AzureMcp.Services.Caching;

namespace AzureMcp.Areas.Cosmos.Services;

public class CosmosService(IArmServiceClient armService, ITenantService tenantService, ICacheService cacheService, IIdentityServiceClient credentialService, ICosmosDBServiceClient cosmosDBService)
    : BaseAzureService(credentialService, tenantService), ICosmosService, IDisposable
{
    private readonly IArmServiceClient _armService = armService ?? throw new ArgumentNullException(nameof(armService));
    private readonly ICacheService _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    private readonly ICosmosDBServiceClient _cosmosDBService = cosmosDBService ?? throw new ArgumentNullException(nameof(cosmosDBService));
    private bool _disposed;

    public async Task<List<string>> GetCosmosAccounts(string subscriptionId, string? tenant = null, RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscriptionId);

        try
        {
            return await _armService.GetCosmosAccountsAsync(subscriptionId, tenant);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving Cosmos DB accounts: {ex.Message}", ex);
        }
    }

    public async Task<List<string>> ListDatabases(
        string accountName,
        string subscriptionId,
        AuthMethod authMethod = AuthMethod.Credential,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(accountName, subscriptionId);

        try
        {
            return await _cosmosDBService.ListDatabasesAsync(
                accountName,
                subscriptionId,
                ConvertAuthMethodToString(authMethod),
                tenant);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error listing databases: {ex.Message}", ex);
        }
    }

    public async Task<List<string>> ListContainers(
        string accountName,
        string databaseName,
        string subscriptionId,
        AuthMethod authMethod = AuthMethod.Credential,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(accountName, databaseName, subscriptionId);

        try
        {
            return await _cosmosDBService.ListContainersAsync(
                accountName,
                databaseName,
                subscriptionId,
                ConvertAuthMethodToString(authMethod),
                tenant);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error listing containers: {ex.Message}", ex);
        }
    }

    public async Task<List<JsonNode>> QueryItems(
        string accountName,
        string databaseName,
        string containerName,
        string? query,
        string subscriptionId,
        AuthMethod authMethod = AuthMethod.Credential,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(accountName, databaseName, containerName, subscriptionId);

        try
        {
            var baseQuery = string.IsNullOrEmpty(query) ? "SELECT * FROM c" : query;

            var jsonItems = await _cosmosDBService.QueryItemsAsync(
                accountName,
                databaseName,
                containerName,
                baseQuery,
                subscriptionId,
                ConvertAuthMethodToString(authMethod),
                tenant);

            var items = new List<JsonNode>();
            foreach (var json in jsonItems)
            {
                if (!string.IsNullOrEmpty(json))
                {
                    var jsonNode = JsonNode.Parse(json);
                    if (jsonNode != null)
                    {
                        items.Add(jsonNode);
                    }
                }
            }

            return items;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error querying items: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
    
    private static string ConvertAuthMethodToString(AuthMethod authMethod)
    {
        return authMethod switch
        {
            AuthMethod.Key => "Key",
            AuthMethod.Credential => "Credential",
            _ => "Credential"
        };
    }
}
