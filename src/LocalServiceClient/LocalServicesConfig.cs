// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureMcp.LocalServiceClient;

[JsonSerializable(typeof(LocalServiceConfig))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class LocalServiceConfigJsonContext : JsonSerializerContext
{
}

internal static class LocalServicesConfig
{
    private static readonly Lazy<LocalServiceConfig> _config = new(LoadConfig);

    public static LocalServiceConfig Config => _config.Value;

    private static LocalServiceConfig LoadConfig()
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "localservices.json");

            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<LocalServiceConfig>(json, LocalServiceConfigJsonContext.Default.LocalServiceConfig);
                if (config != null)
                {
                    return config;
                }
            }
        }
        catch (Exception)
        {
        }

        return new LocalServiceConfig();
    }

    public static string GetServicePath(string localServiceName)
    {
        if (Config.ServicePaths.TryGetValue(localServiceName, out var path))
        {
            return path;
        }

        // Fallback to default path if not configured
        return Path.Combine("localservices", localServiceName);
    }
}

internal class LocalServiceConfig
{
    public Dictionary<string, string> ServicePaths { get; set; } = new();
}
