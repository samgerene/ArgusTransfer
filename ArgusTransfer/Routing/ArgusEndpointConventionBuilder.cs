// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusEndpointConventionBuilder.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Routing
{
    /// <summary>
    /// Writes metadata to an <see cref="ArgusRouteEndpoint"/>
    /// </summary>
    internal class ArgusEndpointConventionBuilder : IArgusEndpointConventionBuilder
    {
        /// <summary>
        /// The <see cref="ArgusRouteEndpoint"/> being configured
        /// </summary>
        private readonly ArgusRouteEndpoint endpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusEndpointConventionBuilder"/> class
        /// </summary>
        /// <param name="endpoint">
        /// The <see cref="ArgusRouteEndpoint"/> to attach metadata to
        /// </param>
        public ArgusEndpointConventionBuilder(ArgusRouteEndpoint endpoint)
        {
            this.endpoint = endpoint;
        }

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
        public IArgusEndpointConventionBuilder WithMetadata(string key, string value)
        {
            this.endpoint.Metadata[key] = value;
            return this;
        }

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
        public IArgusEndpointConventionBuilder WithMiddleware(IArgusMiddleware middleware)
        {
            this.endpoint.AddMiddleware(middleware);
            return this;
        }
    }
}
