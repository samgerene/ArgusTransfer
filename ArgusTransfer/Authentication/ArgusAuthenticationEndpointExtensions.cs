// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusAuthenticationEndpointExtensions.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Authentication
{
    using ArgusTransfer.Routing;

    /// <summary>
    /// Extension methods that mark endpoints as requiring authentication or allowing anonymous callers
    /// </summary>
    public static class ArgusAuthenticationEndpointExtensions
    {
        /// <summary>
        /// Requires an authenticated caller for the endpoint, even when
        /// <see cref="ArgusAuthenticationOptions.RequireAuthentication"/> is <c>false</c>
        /// </summary>
        /// <param name="builder">
        /// The <see cref="IArgusEndpointConventionBuilder"/> of the endpoint
        /// </param>
        /// <returns>
        /// The <see cref="IArgusEndpointConventionBuilder"/> for method chaining
        /// </returns>
        public static IArgusEndpointConventionBuilder RequireAuthentication(this IArgusEndpointConventionBuilder builder)
        {
            return builder.WithMetadata(ArgusAuthenticationOptions.AuthorizeMetadataKey, "true");
        }

        /// <summary>
        /// Allows anonymous callers for the endpoint, even when <see cref="ArgusAuthenticationOptions.RequireAuthentication"/>
        /// is <c>true</c>. The authentication handler still runs, so <see cref="ArgusContext.User"/> is set when the caller
        /// sends valid credentials.
        /// </summary>
        /// <param name="builder">
        /// The <see cref="IArgusEndpointConventionBuilder"/> of the endpoint
        /// </param>
        /// <returns>
        /// The <see cref="IArgusEndpointConventionBuilder"/> for method chaining
        /// </returns>
        public static IArgusEndpointConventionBuilder AllowAnonymous(this IArgusEndpointConventionBuilder builder)
        {
            return builder.WithMetadata(ArgusAuthenticationOptions.AuthorizeMetadataKey, "false");
        }
    }
}
