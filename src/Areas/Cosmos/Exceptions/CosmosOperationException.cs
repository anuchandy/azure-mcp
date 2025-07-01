// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;

namespace AzureMcp.Areas.Cosmos.Exceptions;

/// <summary>
/// Represents an exception that occurred during a Cosmos DB Data plane operation.
/// This wraps the external Microsoft.Azure.Cosmos.CosmosException to prevent leaking Microsoft.Azure.Cosmos package.
/// </summary>
public class CosmosOperationException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public double RequestCharge { get; }
    public string? ActivityId { get; }
    public TimeSpan? RetryAfter { get; }

    public CosmosOperationException(
        string message, 
        HttpStatusCode statusCode, 
        double requestCharge = 0,
        string? activityId = null,
        TimeSpan? retryAfter = null,
        Exception? innerException = null) 
        : base(message, innerException)
    {
        StatusCode = statusCode;
        RequestCharge = requestCharge;
        ActivityId = activityId;
        RetryAfter = retryAfter;
    }
}
