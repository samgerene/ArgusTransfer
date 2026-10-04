// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRouteEndpoint.cs" >
//
//     Copyright (c) 2025-2026 Sam Gerené
//
//     Licensed under the Apache License, Version 2.0 (the "License");
//     you may not use this file except in compliance with the License.
//     You may obtain a copy of the License at
//
//         http://www.apache.org/licenses/LICENSE-2.0
//
//     Unless required by applicable law or agreed to in writing, softwareUseCases
//     distributed under the License is distributed on an "AS IS" BASIS,
//     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//     See the License for the specific language governing permissions and
//     limitations under the License.
//
//   </copyright>
//   ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Routing
{
    using System.Collections.Generic;
    using System.Threading;

    using ArgusTransfer.Protocol;

    /// <summary>
    /// Represents a registered route endpoint with its verb, parsed route template,
    /// handler delegate, optional metadata, per-endpoint middleware, and its cached pipeline
    /// </summary>
    internal class ArgusRouteEndpoint
    {
        /// <summary>
        /// The middleware instances attached to this endpoint
        /// </summary>
        private readonly List<IArgusMiddleware> middlewares = new List<IArgusMiddleware>();

        /// <summary>
        /// The version of <see cref="middlewares"/>, incremented whenever a middleware is added
        /// </summary>
        private int middlewareVersion;

        /// <summary>
        /// Gets or sets the <see cref="ArgusVerb"/> this endpoint responds to
        /// </summary>
        public ArgusVerb Verb { get; set; }

        /// <summary>
        /// Gets or sets the parsed route template for this endpoint
        /// (e.g. "/healthendpoint/{identifier:Guid}")
        /// </summary>
        public ArgusRouteTemplate Template { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ArgusHandlerDelegate"/> that handles matching requests
        /// </summary>
        public ArgusHandlerDelegate Handler { get; set; } = null!;

        /// <summary>
        /// Gets the metadata dictionary attached to this endpoint
        /// </summary>
        public Dictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets the middleware instances attached to this endpoint,
        /// executed in registration order (first registered = outermost)
        /// </summary>
        public IReadOnlyList<IArgusMiddleware> Middlewares => this.middlewares;

        /// <summary>
        /// Gets the version of <see cref="Middlewares"/>, which changes whenever a middleware is added
        /// </summary>
        public int MiddlewareVersion => Volatile.Read(ref this.middlewareVersion);

        /// <summary>
        /// Gets or sets the composed pipeline cached by the router, or <c>null</c> when none has been built yet
        /// </summary>
        public CachedPipeline Pipeline { get; set; }

        /// <summary>
        /// Attaches a middleware to this endpoint and invalidates the cached pipeline
        /// </summary>
        /// <param name="middleware">
        /// The <see cref="IArgusMiddleware"/> to attach
        /// </param>
        public void AddMiddleware(IArgusMiddleware middleware)
        {
            this.middlewares.Add(middleware);
            Interlocked.Increment(ref this.middlewareVersion);
        }

        /// <summary>
        /// A composed pipeline together with the middleware versions it was built from; it is current only while
        /// both versions are unchanged
        /// </summary>
        /// <param name="GlobalMiddlewareVersion">
        /// The router's global middleware version the pipeline was built from
        /// </param>
        /// <param name="EndpointMiddlewareVersion">
        /// The endpoint's <see cref="MiddlewareVersion"/> the pipeline was built from
        /// </param>
        /// <param name="Delegate">
        /// The composed <see cref="ArgusRequestDelegate"/>
        /// </param>
        internal sealed record CachedPipeline(int GlobalMiddlewareVersion, int EndpointMiddlewareVersion, ArgusRequestDelegate Delegate);
    }
}
