// ------------------------------------------------------------------------------------------------
// <copyright file="IArgusAuthenticationHandler.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Authentication
{
    using System.Threading.Tasks;

    using ArgusTransfer.Routing;

    /// <summary>
    /// Authenticates incoming requests, typically from the <c>Authorization</c> header
    /// (<see cref="Protocol.ArgusRequest.AuthorizationScheme"/> and <see cref="Protocol.ArgusRequest.AuthorizationParameter"/>).
    /// Registered as a singleton by <c>AddArgusAuthentication&lt;THandler&gt;()</c>, so implementations must be thread-safe.
    /// </summary>
    public interface IArgusAuthenticationHandler
    {
        /// <summary>
        /// Authenticates the request of the specified context
        /// </summary>
        /// <param name="context">
        /// The <see cref="ArgusContext"/> of the request; its <see cref="ArgusContext.EndpointMetadata"/> and
        /// <see cref="ArgusContext.RequestAborted"/> are available
        /// </param>
        /// <returns>
        /// The <see cref="ArgusAuthenticationResult"/>: <see cref="ArgusAuthenticationResult.Success"/> with the caller's
        /// identity, <see cref="ArgusAuthenticationResult.NoResult"/> without credentials, <see cref="ArgusAuthenticationResult.Fail"/>
        /// for invalid credentials, or <see cref="ArgusAuthenticationResult.Forbidden"/> for a caller that may not access the endpoint
        /// </returns>
        Task<ArgusAuthenticationResult> AuthenticateAsync(ArgusContext context);
    }
}
