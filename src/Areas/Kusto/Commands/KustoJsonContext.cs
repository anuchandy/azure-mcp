// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AzureMcp.Areas.Kusto.Commands;

namespace AzureMcp.Commands.Kusto;

[JsonSerializable(typeof(ClusterListCommand.ClusterListCommandResult))]
[JsonSerializable(typeof(ClusterGetCommand.ClusterGetCommandResult))]
[JsonSerializable(typeof(DatabaseListCommand.DatabaseListCommandResult))]
[JsonSerializable(typeof(TableListCommand.TableListCommandResult))]
[JsonSerializable(typeof(TableSchemaCommand.TableSchemaCommandResult))]
[JsonSerializable(typeof(QueryCommand.QueryCommandResult))]
[JsonSerializable(typeof(SampleCommand.SampleCommandResult))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonNode))]
[JsonSerializable(typeof(List<KustoClusterResourceProxy>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class KustoJsonContext : JsonSerializerContext
{
}

public sealed record KustoClusterResourceProxy()
{
    public required string ClusterUri { get; set; }
    public required string ClusterName { get; set; }
    public required string Location { get; set; }
    public required string ResourceGroupName { get; set; }
    public required string SubscriptionId { get; set; }
    public required string Sku { get; set; }
    public required string Zones { get; set; }
    public required string Identity { get; set; }
    public required string ETag { get; set; }
    public required string State { get; set; }
    public required string ProvisioningState { get; set; }
    public required string DataIngestionUri { get; set; }
    public required string StateReason { get; set; }
    public required bool IsStreamingIngestEnabled { get; set; }
    public required string EngineType { get; set; }
    public required bool IsAutoStopEnabled { get; set; }
}
