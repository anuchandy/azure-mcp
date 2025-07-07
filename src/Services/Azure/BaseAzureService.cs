// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.Versioning;
using Azure.Core;
using AzureMcp.Services.Azure.Tenant;
using AzureMcp.LocalServiceClient.Identity;

namespace AzureMcp.Services.Azure;

public abstract class BaseAzureService(IIdentityServiceClient credentialService, ITenantService? tenantService = null)
{
    private static readonly UserAgentPolicy s_sharedUserAgentPolicy;
    internal static readonly string s_defaultUserAgent;

    private TokenCredential? _credential;
    private string? _lastTenantId;
    private readonly ITenantService? _tenantService = tenantService;
    private readonly IIdentityServiceClient _credentialService = credentialService;

    static BaseAzureService()
    {
        var assembly = typeof(BaseAzureService).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        var framework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        var platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

        s_defaultUserAgent = $"azmcp/{version} ({framework}; {platform})";
        s_sharedUserAgentPolicy = new UserAgentPolicy(s_defaultUserAgent);
    }

    protected string UserAgent { get; } = s_defaultUserAgent;

    protected async Task<string?> ResolveTenantIdAsync(string? tenant)
    {
        if (tenant == null || _tenantService == null)
            return tenant;
        return await _tenantService.GetTenantId(tenant);
    }

    protected async Task<TokenCredential> GetCredential(string? tenant = null)
    {
        var tenantId = string.IsNullOrEmpty(tenant) ? null : await ResolveTenantIdAsync(tenant);

        // Return cached credential if it exists and tenant ID hasn't changed
        if (_credential != null && _lastTenantId == tenantId)
        {
            return _credential;
        }

        try
        {
            _credential = await _credentialService.GetCredentialAsync(tenantId);
            _lastTenantId = tenantId;
            return _credential;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get credential: {ex.Message}", ex);
        }
    }

    protected static T AddDefaultPolicies<T>(T clientOptions) where T : ClientOptions
    {
        clientOptions.AddPolicy(s_sharedUserAgentPolicy, HttpPipelinePosition.BeforeTransport);

        return clientOptions;
    }

    /// <summary>
    /// Validates that the provided parameters are not null or empty
    /// </summary>
    /// <param name="parameters">Array of parameters to validate</param>
    /// <exception cref="ArgumentException">Thrown when any parameter is null or empty</exception>
    protected static void ValidateRequiredParameters(params string?[] parameters)
    {
        foreach (var param in parameters)
        {
            ArgumentException.ThrowIfNullOrEmpty(param);
        }
    }
}