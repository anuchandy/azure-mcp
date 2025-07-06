// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureMcp.LocalServiceClient.Arm;

/// <summary>
/// Exception thrown when a local service call fails.
/// </summary>
public sealed class LocalServiceCallException : Exception
{
    /// <summary>
    /// Gets the name of the method that failed (without Async suffix).
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// Gets the error message from the service response.
    /// </summary>
    public string ServiceErrorMessage { get; }

    /// <summary>
    /// Initializes a new instance of the LocalServiceCallException.
    /// </summary>
    /// <param name="methodName">The name of the method that failed (without Async suffix).</param>
    /// <param name="serviceErrorMessage">The error message from the service response.</param>
    public LocalServiceCallException(string methodName, string serviceErrorMessage)
        : base($"Local service call '{methodName}' failed: {serviceErrorMessage}")
    {
        MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
        ServiceErrorMessage = serviceErrorMessage ?? throw new ArgumentNullException(nameof(serviceErrorMessage));
    }

    /// <summary>
    /// Initializes a new instance of the LocalServiceCallException with an inner exception.
    /// </summary>
    /// <param name="methodName">The name of the method that failed (without Async suffix).</param>
    /// <param name="serviceErrorMessage">The error message from the service response.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public LocalServiceCallException(string methodName, string serviceErrorMessage, Exception innerException)
        : base($"Local service call '{methodName}' failed: {serviceErrorMessage}", innerException)
    {
        MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
        ServiceErrorMessage = serviceErrorMessage ?? throw new ArgumentNullException(nameof(serviceErrorMessage));
    }
}
