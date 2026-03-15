// -------------------------------------------------------------------------------------------------
//   <copyright file="IArgusRouteBuilder.cs">
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
    /// <summary>
    /// Defines methods for registering route handlers and global middleware
    /// </summary>
    public interface IArgusRouteBuilder
    {
        /// <summary>
        /// Registers a global middleware that runs for all requests in registration order.
        /// Global middleware executes before per-endpoint middleware
        /// </summary>
        /// <param name="middleware">
        /// The <see cref="IArgusMiddleware"/> instance to register
        /// </param>
        void UseMiddleware(IArgusMiddleware middleware);

        /// <summary>
        /// Registers a handler for GET requests matching the specified route template
        /// </summary>
        /// <param name="routeTemplate">
        /// The route template to match (e.g. "/healthendpoint/{identifier:Guid}")
        /// </param>
        /// <param name="handler">
        /// The <see cref="ArgusHandlerDelegate"/> to invoke when matched
        /// </param>
        /// <returns>
        /// An <see cref="IArgusEndpointConventionBuilder"/> for further configuration
        /// </returns>
        IArgusEndpointConventionBuilder MapGet(string routeTemplate, ArgusHandlerDelegate handler);

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
        IArgusEndpointConventionBuilder MapPost(string routeTemplate, ArgusHandlerDelegate handler);

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
        IArgusEndpointConventionBuilder MapPut(string routeTemplate, ArgusHandlerDelegate handler);

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
        IArgusEndpointConventionBuilder MapPatch(string routeTemplate, ArgusHandlerDelegate handler);

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
        IArgusEndpointConventionBuilder MapHead(string routeTemplate, ArgusHandlerDelegate handler);

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
        IArgusEndpointConventionBuilder MapDelete(string routeTemplate, ArgusHandlerDelegate handler);
    }
}
