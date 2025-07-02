// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureMcp.GrpcClient.Credential;

public sealed class AuthenticationFailedException : Exception
{
    public string? ErrorCode { get; }
    public string? ErrorContext { get; }
    public bool IsRetryable { get; }

    public AuthenticationFailedException(string message) : base(message)
    {
    }

    public AuthenticationFailedException(string message, string? errorCode, string? errorContext, bool isRetryable) 
        : base(message)
    {
        ErrorCode = errorCode;
        ErrorContext = errorContext;
        IsRetryable = isRetryable;
    }

    public AuthenticationFailedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
