// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AzureMcp.Tests.Client.Helpers;
using ModelContextProtocol.Client;
using Xunit;

namespace AzureMcp.Tests.Client;

public abstract class CommandTestsBase : IDisposable
{
    protected const string TenantNameReason = "Service principals cannot use TenantName for lookup";

    protected IMcpClient Client { get; }
    protected LiveTestSettings Settings { get; }
    protected StringBuilder FailureOutput { get; } = new();
    protected ITestOutputHelper Output { get; }

    public CommandTestsBase(LiveTestFixture liveTestFixture, ITestOutputHelper output)
    {
        Client = liveTestFixture.Client;
        Settings = liveTestFixture.Settings;
        Output = output;
    }

    protected async Task<JsonElement?> CallToolAsync(string command, Dictionary<string, object?> parameters)
    {
        // Output will be streamed, so if we're not in debug mode, hold the debug output for logging in the failure case
        Action<string> writeOutput = Settings.DebugOutput
            ? s => Output.WriteLine(s)
            : s => FailureOutput.AppendLine(s);

        writeOutput($"request: {JsonSerializer.Serialize(new { command, parameters })}");

        var result = await Client.CallToolAsync(command, parameters);

        // Fail if the server process indicated an error
        if (result.IsError)
        {
            Output.WriteLine($"response: {JsonSerializer.Serialize(result)}");
            throw new Exception($"Server process failed: {result.Content.FirstOrDefault()?.Text}");
        }

        var content = result.Content.FirstOrDefault(c => c.MimeType == "application/json")?.Text;
        if (string.IsNullOrWhiteSpace(content))
        {
            Output.WriteLine($"response: {JsonSerializer.Serialize(result)}");
            throw new Exception("No JSON content found in the response.");
        }

        var root = JsonSerializer.Deserialize<JsonElement>(content!);
        if (root.ValueKind != JsonValueKind.Object)
        {
            Output.WriteLine($"response: {JsonSerializer.Serialize(result)}");
            throw new Exception("Invalid JSON response.");
        }

        // If the response has a status property and it's >= 400, treat as error
        if (root.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.Number && statusProp.GetInt32() >= 400)
        {
            var message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : null;
            throw new Exception($"Server returned error status {statusProp.GetInt32()}: {message}");
        }

        // Remove the `args` property and log the content
        var trimmed = root.Deserialize<JsonObject>()!;
        trimmed.Remove("args");
        writeOutput($"response content: {trimmed.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}");

        return root.TryGetProperty("results", out var property) ? property : null;
    }

    public void Dispose()
    {
        if (!Settings.DebugOutput && TestContext.Current.TestState?.Result == TestResult.Failed && FailureOutput.Length > 0)
        {
            Output.WriteLine(FailureOutput.ToString());
        }
    }
}
