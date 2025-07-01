// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;

namespace AzureMcp.Ext.Credential;

/// <summary>
/// A token credential that wraps another credential and adds a timeout to token acquisition.
/// </summary>
/// <param name="innerCredential">The credential to wrap.</param>
/// <param name="timeout">The timeout for token acquisition.</param>
internal class TimeoutTokenCredential(TokenCredential innerCredential, TimeSpan timeout) : TokenCredential
{
    private readonly TokenCredential _innerCredential = innerCredential;
    private readonly TimeSpan _timeout = timeout;

    /// <summary>
    /// Synchronously acquires an access token with timeout protection.
    /// </summary>
    /// <param name="requestContext">The details of the authentication request.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to control the request lifetime.</param>
    /// <returns>An <see cref="AccessToken"/> which can be used to authenticate service client calls.</returns>
    /// <exception cref="TimeoutException">Thrown when authentication times out.</exception>
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        try
        {
            return _innerCredential.GetToken(requestContext, cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Authentication timed out after {_timeout.TotalSeconds} seconds.");
        }
    }

    /// <summary>
    /// Asynchronously acquires an access token with timeout protection.
    /// </summary>
    /// <param name="requestContext">The details of the authentication request.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to control the request lifetime.</param>
    /// <returns>A <see cref="ValueTask{AccessToken}"/> which will resolve to an <see cref="AccessToken"/>.</returns>
    /// <exception cref="TimeoutException">Thrown when authentication times out.</exception>
    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        try
        {
            return await _innerCredential.GetTokenAsync(requestContext, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Authentication timed out after {_timeout.TotalSeconds} seconds.");
        }
    }
}
