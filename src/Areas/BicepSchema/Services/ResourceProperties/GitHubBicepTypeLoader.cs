// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Bicep.Types;
using Azure.Bicep.Types.Concrete;
using Azure.Bicep.Types.Index;
using Azure.Bicep.Types.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Areas.BicepSchema.Services.ResourceProperties;

public class GitHubBicepTypeLoader(HttpClient httpClient, IMemoryCache cache, ILogger<GitHubBicepTypeLoader> logger) : ITypeLoader
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IMemoryCache _cache = cache; // should this be disk based considering the size of the specs, or no caching is fine?
    private readonly ILogger<GitHubBicepTypeLoader> _logger = logger;
    private static readonly TimeSpan DefaultCacheExpiration = TimeSpan.FromHours(24);
    private const string BaseUrl = "https://raw.githubusercontent.com/Azure/bicep-types-az/refs/heads/main/generated";
    private const string TypeIndexUrl = BaseUrl + "/index.json";
    private const string TypeIndexCacheKey = "bicep_az_type_index";
    private const string ResourceTypeCacheKeyPrefix = "bicep_az_resource_type_";
    private const string FunctionTypeCacheKeyPrefix = "bicep_az_function_type_";
    private const string TypeCacheKeyPrefix = "bicep_az_type_";
    private static string GetTypeUrl(string typePath) => $"{BaseUrl}/{typePath}";

    public TypeIndex LoadTypeIndex()
    {
        if (_cache.TryGetValue(TypeIndexCacheKey, out TypeIndex? cachedIndex) && cachedIndex != null)
        {
            return cachedIndex;
        }

        _logger.LogInformation("Fetching type index from {Url}", TypeIndexUrl);

        using HttpResponseMessage response = _httpClient.GetAsync(TypeIndexUrl).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to fetch type index from {TypeIndexUrl}. Status code: {response.StatusCode}");
        }

        using Stream contentStream = response.Content.ReadAsStream();
        TypeIndex typeIndex = TypeSerializer.DeserializeIndex(contentStream);
        _cache.Set(TypeIndexCacheKey, typeIndex, DefaultCacheExpiration);
        return typeIndex;
    }

    public ResourceType LoadResourceType(CrossFileTypeReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        string cacheKey = $"{ResourceTypeCacheKeyPrefix}{reference}";
        if (_cache.TryGetValue(cacheKey, out ResourceType? cachedType) && cachedType != null)
        {
            return cachedType;
        }
        TypeBase type = LoadTypeFromRemote(reference);
        if (type is ResourceType resourceType)
        {
            _cache.Set(cacheKey, resourceType, DefaultCacheExpiration);
            return resourceType;
        }
        throw new InvalidOperationException($"Type found at reference {reference} is not a ResourceType");
    }

    public ResourceFunctionType LoadResourceFunctionType(CrossFileTypeReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        string cacheKey = $"{FunctionTypeCacheKeyPrefix}{reference}";
        if (_cache.TryGetValue(cacheKey, out ResourceFunctionType? cachedType) && cachedType != null)
        {
            return cachedType;
        }
        TypeBase type = LoadTypeFromRemote(reference);
        if (type is ResourceFunctionType functionType)
        {
            _cache.Set(cacheKey, functionType, DefaultCacheExpiration);
            return functionType;
        }
        throw new InvalidOperationException($"Type found at reference {reference} is not a ResourceFunctionType");
    }

    public TypeBase LoadType(CrossFileTypeReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        string cacheKey = $"{TypeCacheKeyPrefix}{reference}";
        if (_cache.TryGetValue(cacheKey, out TypeBase? cachedType) && cachedType != null)
        {
            return cachedType;
        }
        TypeBase type = LoadTypeFromRemote(reference);
        _cache.Set(cacheKey, type, DefaultCacheExpiration);
        return type;
    }

    private TypeBase LoadTypeFromRemote(CrossFileTypeReference reference)
    {
        string url = GetTypeUrl(reference.RelativePath);
        _logger.LogInformation("Fetching type from {Url}", url);
        using HttpResponseMessage response = _httpClient.GetAsync(url).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to fetch type. Status code: {StatusCode}", response.StatusCode);
            throw new InvalidOperationException($"Failed to fetch type from {url}. Status code: {response.StatusCode}");
        }
        using Stream contentStream = response.Content.ReadAsStream();
        try
        {
            TypeBase[] types = TypeSerializer.Deserialize(contentStream);
            if (types.Length <= reference.Index)
            {
                throw new ArgumentException($"Unable to locate type at index {reference.Index} in \"{reference.RelativePath}\" resource");
            }
            return types[reference.Index];
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize type from {Url}", url);
            throw new InvalidOperationException($"Failed to deserialize type from {url}", ex);
        }
    }
}
