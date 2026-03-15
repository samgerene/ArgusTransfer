// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRouter.cs" >
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
    /// <see cref="ArgusRequest"/> messages through a middleware pipeline
    /// to matching route handlers
    /// </summary>
    public class ArgusRouter : IArgusRouteBuilder
    {
        /// <summary>
        /// The list of registered route endpoints
        /// </summary>
        private readonly List<ArgusRouteEndpoint> endpoints = new List<ArgusRouteEndpoint>();

        /// <summary>
        /// The list of global middleware instances, executed in registration order
        /// </summary>
        private readonly List<IArgusMiddleware> globalMiddlewares = new List<IArgusMiddleware>();

        /// <summary>
        /// Registers a global middleware that runs for all requests in registration order.
        /// Global middleware executes before per-endpoint middleware
        /// </summary>
        /// <param name="middleware">
        /// The <see cref="IArgusMiddleware"/> instance to register
        /// </param>
        public void UseMiddleware(IArgusMiddleware middleware)
        {
            this.globalMiddlewares.Add(middleware);
        }

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
        /// Routes an <see cref="ArgusContext"/> to the matching handler, executing
        /// global and per-endpoint middleware in the pipeline
        /// </summary>
        /// <param name="context">
        /// The <see cref="ArgusContext"/> for the current request
        /// </param>
        /// <returns>
        /// An awaitable <see cref="Task"/>. When complete, <see cref="ArgusContext.Response"/>
        /// contains the result. If no route matches, a <see cref="ArgusStatusCode.NotFound"/>
        /// response is set. If the route matches but the verb does not, a
        /// <see cref="ArgusStatusCode.BadRequest"/> response is set
        /// </returns>
        public async Task RouteAsync(ArgusContext context)
        {
            var routeMatched = false;

            foreach (var endpoint in this.endpoints)
            {
                if (ArgusRouteTemplateParser.TryMatch(endpoint.RouteTemplate, context.Request.Route, out var routeValues))
                {
                    routeMatched = true;

                    if (endpoint.Verb == context.Request.Verb)
                    {
                        context.RouteValues = routeValues;
                        context.EndpointMetadata = endpoint.Metadata;

                        var pipeline = this.BuildPipeline(endpoint);
                        await pipeline(context);

                        this.StampCorrelationToken(context);

                        return;
                    }
                }
            }

            context.Response = new ArgusResponse
            {
                StatusCode = routeMatched ? ArgusStatusCode.BadRequest : ArgusStatusCode.NotFound
            };

            this.StampCorrelationToken(context);
        }

        /// <summary>
        /// Stamps the <see cref="ArgusContext.CorrelationToken"/> onto the response
        /// so that handlers and middleware never need to set it manually
        /// </summary>
        /// <param name="context">
        /// The <see cref="ArgusContext"/> whose response will be stamped
        /// </param>
        private void StampCorrelationToken(ArgusContext context)
        {
            if (context.Response != null)
            {
                context.Response.CorrelationToken = context.CorrelationToken;
            }
        }

        /// <summary>
        /// Builds a composed pipeline delegate by wrapping the endpoint handler
        /// with per-endpoint middleware (innermost) and global middleware (outermost)
        /// </summary>
        /// <param name="endpoint">
        /// The <see cref="ArgusRouteEndpoint"/> whose handler and middleware form the pipeline
        /// </param>
        /// <returns>
        /// An <see cref="ArgusRequestDelegate"/> representing the complete pipeline
        /// </returns>
        private ArgusRequestDelegate BuildPipeline(ArgusRouteEndpoint endpoint)
        {
            ArgusRequestDelegate pipeline = ctx => endpoint.Handler(ctx);

            for (var i = endpoint.Middlewares.Count - 1; i >= 0; i--)
            {
                var middleware = endpoint.Middlewares[i];
                var next = pipeline;
                pipeline = ctx => middleware.InvokeAsync(ctx, next);
            }

            for (var i = this.globalMiddlewares.Count - 1; i >= 0; i--)
            {
                var middleware = this.globalMiddlewares[i];
                var next = pipeline;
                pipeline = ctx => middleware.InvokeAsync(ctx, next);
            }

            return pipeline;
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
