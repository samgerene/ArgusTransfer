// ------------------------------------------------------------------------------------------------
// <copyright file="IArgusEndpointConventionBuilder.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Routing
{
    /// <summary>
    /// Provides a fluent interface for attaching metadata and middleware to a registered route endpoint
    /// </summary>
    public interface IArgusEndpointConventionBuilder
    {
        /// <summary>
        /// Attaches a key-value metadata pair to the endpoint
        /// </summary>
        /// <param name="key">
        /// The metadata key
        /// </param>
        /// <param name="value">
        /// The metadata value
        /// </param>
        /// <returns>
        /// This <see cref="IArgusEndpointConventionBuilder"/> for method chaining
        /// </returns>
        IArgusEndpointConventionBuilder WithMetadata(string key, string value);

        /// <summary>
        /// Attaches a middleware instance to the endpoint. Per-endpoint middleware
        /// executes after global middleware, in registration order (first registered = outermost)
        /// </summary>
        /// <param name="middleware">
        /// The <see cref="IArgusMiddleware"/> instance to attach
        /// </param>
        /// <returns>
        /// This <see cref="IArgusEndpointConventionBuilder"/> for method chaining
        /// </returns>
        IArgusEndpointConventionBuilder WithMiddleware(IArgusMiddleware middleware);
    }
}
