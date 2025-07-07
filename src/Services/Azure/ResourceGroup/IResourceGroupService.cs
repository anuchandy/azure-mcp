// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Models.ResourceGroup;
using AzureMcp.Options;

namespace AzureMcp.Services.Azure.ResourceGroup;

public interface IResourceGroupService
{
    Task<List<ResourceGroupInfo>> GetResourceGroups(string subscriptionId, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
    Task<ResourceGroupInfo?> GetResourceGroup(string subscriptionId, string resourceGroupName, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
}
