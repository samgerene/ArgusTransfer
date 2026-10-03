// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusContext.cs">
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
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using ArgusTransfer.Protocol;

    /// <summary>
    /// Encapsulates all information about an individual Argus request as it flows
    /// through the middleware pipeline and into the endpoint handler
    /// </summary>
    public class ArgusContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusContext"/> class
        /// </summary>
        /// <param name="request">
        /// The incoming <see cref="ArgusRequest"/>
        /// </param>
        /// <param name="requestAborted">
        /// A <see cref="CancellationToken"/> that is triggered when the request is aborted
        /// </param>
        public ArgusContext(ArgusRequest request, CancellationToken requestAborted)
        {
            this.Request = request;
            this.CorrelationToken = request.CorrelationToken;
            this.RequestAborted = requestAborted;
        }

        /// <summary>
        /// Gets the correlation token that identifies this request-response exchange.
        /// Initialized from the incoming request and automatically stamped on the response
        /// by the router after the pipeline completes
        /// </summary>
        public Guid CorrelationToken { get; }

        /// <summary>
        /// Gets the incoming <see cref="ArgusRequest"/>
        /// </summary>
        public ArgusRequest Request { get; }

        /// <summary>
        /// Gets or sets the <see cref="ArgusResponse"/> produced by the handler or middleware pipeline
        /// </summary>
        public ArgusResponse Response { get; set; }

        /// <summary>
        /// Gets the route parameter values extracted from the request route
        /// </summary>
        public IReadOnlyDictionary<string, string> RouteValues { get; internal set; }

        /// <summary>
        /// Gets the query parameter values parsed from the request URL
        /// </summary>
        public IReadOnlyDictionary<string, string> QueryValues { get; internal set; }

        /// <summary>
        /// Gets the metadata associated with the matched endpoint
        /// </summary>
        public IReadOnlyDictionary<string, string> EndpointMetadata { get; internal set; }

        /// <summary>
        /// Gets the <see cref="CancellationToken"/> that is triggered when the request is aborted
        /// </summary>
        public CancellationToken RequestAborted { get; }

        /// <summary>
        /// Gets a key-value collection that can be used to share data between
        /// middleware components and the handler within the scope of a single request
        /// </summary>
        public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the <see cref="IServiceProvider"/> for resolving request-scoped services
        /// </summary>
        public IServiceProvider RequestServices { get; set; }

        /// <summary>
        /// Sets <see cref="Response"/> to an error response carrying an <see cref="ArgusProblemDetails"/> body
        /// whose <see cref="ArgusProblemDetails.Instance"/> is the <see cref="CorrelationToken"/> of this request
        /// </summary>
        /// <param name="statusCode">
        /// The <see cref="ArgusStatusCode"/> of the problem
        /// </param>
        /// <param name="detail">
        /// A human-readable explanation specific to this occurrence of the problem
        /// </param>
        /// <param name="extensions">
        /// Optional additional context to include as top-level members
        /// </param>
        /// <param name="title">
        /// An optional title; defaults to the reason phrase of <paramref name="statusCode"/>
        /// </param>
        /// <param name="type">
        /// An optional error category identifier; defaults to <see cref="ArgusProblemDetails.DefaultType"/>
        /// </param>
        /// <returns>
        /// The <see cref="ArgusResponse"/> assigned to <see cref="Response"/>
        /// </returns>
        public ArgusResponse Problem(
            ArgusStatusCode statusCode,
            string detail = null,
            IDictionary<string, object> extensions = null,
            string title = null,
            string type = null)
        {
            var problem = ArgusProblemDetails.Create(statusCode, detail, extensions, title, type, this.CorrelationToken.ToString());

            this.Response = problem.ToResponse();

            return this.Response;
        }
    }
}
