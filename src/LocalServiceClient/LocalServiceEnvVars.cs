// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureMcp.LocalServiceClient;

/// <summary>
/// Constants for local service environment variable names.
/// </summary>
public static class LocalServiceEnvVars
{
    /// <summary>
    /// Environment variable names for ARM local service.
    /// </summary>
    public static class Arm
    {
        public const string IdentityServiceEndpoint = "AzureMcp__LocalService__Arm__IdentityServiceEndpoint";
    }

    /// <summary>
    /// Environment variable names for CosmosDB local service.
    /// </summary>
    public static class CosmosDB
    {
        public const string IdentityServiceEndpoint = "AzureMcp__LocalService__CosmosDB__IdentityServiceEndpoint";
        public const string ArmServiceEndpoint = "AzureMcp__LocalService__CosmosDB__ArmServiceEndpoint";
    }

    /// <summary>
    /// Environment variable names for Identity local service.
    /// </summary>
    public static class Identity
    {
        // Identity service doesn't depend on other services currently
        // Add constants here if needed in the future
    }
}
