// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRouter.cs" company="Sam Gerené">
//
//     Copyright (c) 2026 Sam Gerené
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
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;

    /// <summary>
    /// Implements <see cref="IArgusRouteBuilder"/> and dispatches incoming
    /// <see cref="ArgusRequest"/> messages to matching route handlers
    /// </summary>
    public class ArgusRouter : IArgusRouteBuilder
    {
        /// <summary>
        /// The list of registered route endpoints
        /// </summary>
        private readonly List<ArgusRouteEndpoint> endpoints = new List<ArgusRouteEndpoint>();

        /// <summary>
        /// Registers a handler for GET requests matching the specified route template
        /// </summary>
        /// <param name="routeTemplate">
        /// The route template to match
        /// </param>
        /// <param name="handler">
        /// The <see cref="ArgusHandlerDelegate"/> to invoke when matched
        /// </param>
        /// <returns>
        /// An <see cref="IArgusEndpointConventionBuilder"/> for further configuration
        /// </returns>
        public IArgusEndpointConventionBuilder MapGet(string routeTemplate, ArgusHandlerDelegate handler)
        {
            return this.Map(ArgusVerb.GET, routeTemplate, handler);
        }

        /// <summary>
        /// Registers a handler for POST requests matching the specified route template
        /// </summary>
        /// <param name="routeTemplate">
        /// The route template to match
        /// </param>
        /// <param name="handler">
        /// The <see cref="ArgusHandlerDelegate"/> to invoke when matched
        /// </param>
        /// <returns>
        /// An <see cref="IArgusEndpointConventionBuilder"/> for further configuration
        /// </returns>
        public IArgusEndpointConventionBuilder MapPost(string routeTemplate, ArgusHandlerDelegate handler)
        {
            return this.Map(ArgusVerb.POST, routeTemplate, handler);
        }

        /// <summary>
        /// Registers a handler for PUT requests matching the specified route template
        /// </summary>
        /// <param name="routeTemplate">
        /// The route template to match
        /// </param>
        /// <param name="handler">
        /// The <see cref="ArgusHandlerDelegate"/> to invoke when matched
        /// </param>
        /// <returns>
        /// An <see cref="IArgusEndpointConventionBuilder"/> for further configuration
        /// </returns>
        public IArgusEndpointConventionBuilder MapPut(string routeTemplate, ArgusHandlerDelegate handler)
        {
            return this.Map(ArgusVerb.PUT, routeTemplate, handler);
        }

        /// <summary>
        /// Registers a handler for PATCH requests matching the specified route template
        /// </summary>
        /// <param name="routeTemplate">
        /// The route template to match
        /// </param>
        /// <param name="handler">
        /// The <see cref="ArgusHandlerDelegate"/> to invoke when matched
        /// </param>
        /// <returns>
        /// An <see cref="IArgusEndpointConventionBuilder"/> for further configuration
        /// </returns>
        public IArgusEndpointConventionBuilder MapPatch(string routeTemplate, ArgusHandlerDelegate handler)
        {
            return this.Map(ArgusVerb.PATCH, routeTemplate, handler);
        }

        /// <summary>
        /// Registers a handler for HEAD requests matching the specified route template
        /// </summary>
        /// <param name="routeTemplate">
        /// The route template to match
        /// </param>
        /// <param name="handler">
        /// The <see cref="ArgusHandlerDelegate"/> to invoke when matched
        /// </param>
        /// <returns>
        /// An <see cref="IArgusEndpointConventionBuilder"/> for further configuration
        /// </returns>
        public IArgusEndpointConventionBuilder MapHead(string routeTemplate, ArgusHandlerDelegate handler)
        {
            return this.Map(ArgusVerb.HEAD, routeTemplate, handler);
        }

        /// <summary>
        /// Registers a handler for DELETE requests matching the specified route template
        /// </summary>
        /// <param name="routeTemplate">
        /// The route template to match
        /// </param>
        /// <param name="handler">
        /// The <see cref="ArgusHandlerDelegate"/> to invoke when matched
        /// </param>
        /// <returns>
        /// An <see cref="IArgusEndpointConventionBuilder"/> for further configuration
        /// </returns>
        public IArgusEndpointConventionBuilder MapDelete(string routeTemplate, ArgusHandlerDelegate handler)
        {
            return this.Map(ArgusVerb.DELETE, routeTemplate, handler);
        }

        /// <summary>
        /// Routes an <see cref="ArgusRequest"/> to the matching handler based on
        /// verb and route, returning the handler's <see cref="ArgusResponse"/>
        /// </summary>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> to route
        /// </param>
        /// <returns>
        /// An <see cref="ArgusResponse"/> from the matched handler,
        /// a <see cref="ArgusStatusCode.BadRequest"/> response if the route matches but the verb does not,
        /// or a <see cref="ArgusStatusCode.NotFound"/> response if no route matches
        /// </returns>
        public async Task<ArgusResponse> RouteAsync(ArgusRequest request)
        {
            var routeMatched = false;

            foreach (var endpoint in this.endpoints)
            {
                if (ArgusRouteTemplateParser.TryMatch(endpoint.RouteTemplate, request.Route, out var routeValues))
                {
                    routeMatched = true;

                    if (endpoint.Verb == request.Verb)
                    {
                        return await endpoint.Handler(request, routeValues);
                    }
                }
            }

            return new ArgusResponse
            {
                CorrelationToken = request.CorrelationToken,
                StatusCode = routeMatched ? ArgusStatusCode.BadRequest : ArgusStatusCode.NotFound
            };
        }

        /// <summary>
        /// Registers a route endpoint with the specified verb, template, and handler
        /// </summary>
        /// <param name="verb">
        /// The <see cref="ArgusVerb"/> to match
        /// </param>
        /// <param name="routeTemplate">
        /// The route template to match
        /// </param>
        /// <param name="handler">
        /// The <see cref="ArgusHandlerDelegate"/> to invoke when matched
        /// </param>
        /// <returns>
        /// An <see cref="ArgusEndpointConventionBuilder"/> for further configuration
        /// </returns>
        private ArgusEndpointConventionBuilder Map(ArgusVerb verb, string routeTemplate, ArgusHandlerDelegate handler)
        {
            var endpoint = new ArgusRouteEndpoint
            {
                Verb = verb,
                RouteTemplate = routeTemplate,
                Handler = handler
            };

            this.endpoints.Add(endpoint);

            return new ArgusEndpointConventionBuilder(endpoint);
        }
    }
}
