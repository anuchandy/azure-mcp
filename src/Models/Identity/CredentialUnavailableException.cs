// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureMcp.Models.Identity;

public sealed class CredentialUnavailableException : Exception
{
    public string? ErrorCode { get; }

    public string? ErrorContext { get; }

    public bool IsRetryable { get; }

    public CredentialUnavailableException(string message) : base(message)
    {
    }

    public CredentialUnavailableException(string message, string? errorCode, string? errorContext, bool isRetryable) 
        : base(message)
    {
        ErrorCode = errorCode;
        ErrorContext = errorContext;
        IsRetryable = isRetryable;
    }

    public CredentialUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
