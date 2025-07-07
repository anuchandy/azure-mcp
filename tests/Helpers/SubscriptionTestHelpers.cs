// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.LocalServiceClient.Arm;

namespace AzureMcp.Tests.Helpers;

/// <summary>
/// Helper methods for creating test subscription data using the ARM service client model.
/// </summary>
public static class SubscriptionTestHelpers
{
    public static SubscriptionData CreateSubscriptionData(string subscriptionId, string displayName)
    {
        return new SubscriptionData
        {
            SubscriptionId = subscriptionId,
            DisplayName = displayName,
            TenantId = Guid.NewGuid().ToString(),
            State = "Enabled"
        };
    }

    /// <summary>
    /// Creates a subscription with minimal test data - useful when you only need ID and name
    /// </summary>
    public static SubscriptionData CreateMinimalSubscriptionData(string id, string name) =>
        CreateSubscriptionData(id, name);

    /// <summary>
    /// Creates a list of test subscriptions with sequential IDs
    /// </summary>
    public static List<SubscriptionData> CreateTestSubscriptions(int count)
    {
        var subs = new List<SubscriptionData>();
        for (int i = 1; i <= count; i++)
        {
            subs.Add(CreateMinimalSubscriptionData(
                $"sub-{i}",
                $"Test Subscription {i}"));
        }
        return subs;
    }
}
