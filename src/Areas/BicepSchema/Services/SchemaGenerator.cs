// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Azure.Bicep.Types;
using AzureMcp.Areas.BicepSchema.Services.ResourceProperties;
using AzureMcp.Areas.BicepSchema.Services.ResourceProperties.Entities;
using AzureMcp.Services.Azure.BicepSchema.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Areas.BicepSchema.Services;
public static class SchemaGenerator
{
    public static List<ComplexType> GetResponse(TypesDefinitionResult typesDefinitionResult)
    {
        var allComplexTypes = new List<ComplexType>();
        allComplexTypes.AddRange(typesDefinitionResult.ResourceTypeEntities);
        allComplexTypes.AddRange(typesDefinitionResult.ResourceFunctionTypeEntities);
        allComplexTypes.AddRange(typesDefinitionResult.OtherComplexTypeEntities);
        return allComplexTypes;
    }

    public static TypesDefinitionResult GetResourceTypeDefinitions(
        IServiceProvider serviceProvider,
        string resourceTypeName,
        string? apiVersion = null)
    {
        ResourceVisitor resourceVisitor = serviceProvider.GetRequiredService<ResourceVisitor>();

        if (string.IsNullOrEmpty(apiVersion))
        {
            apiVersion = ApiVersionSelector.SelectLatestStable(resourceVisitor.GetResourceApiVersions(resourceTypeName));
        }

        return resourceVisitor.LoadSingleResource(resourceTypeName, apiVersion);
    }

    public static void ConfigureServices(ServiceCollection services)
    {
        services.AddHttpClient<GitHubBicepTypeLoader>();
        services.AddSingleton<ITypeLoader>(sp => new GitHubBicepTypeLoader(
            sp.GetRequiredService<System.Net.Http.HttpClient>(),
            sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
            sp.GetRequiredService<ILogger<GitHubBicepTypeLoader>>()));
        services.AddSingleton<ResourceVisitor>();
        services.AddMemoryCache();
    }
}
