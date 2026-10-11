// ------------------------------------------------------------------------------------------------
// <copyright file="IArgusRouteBuilder.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

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
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="routeTemplate"/> or <paramref name="handler"/> is <c>null</c>
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when <paramref name="routeTemplate"/> contains a parameter without a name or references an unknown route constraint
        /// </exception>
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
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="routeTemplate"/> or <paramref name="handler"/> is <c>null</c>
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when <paramref name="routeTemplate"/> contains a parameter without a name or references an unknown route constraint
        /// </exception>
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
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="routeTemplate"/> or <paramref name="handler"/> is <c>null</c>
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when <paramref name="routeTemplate"/> contains a parameter without a name or references an unknown route constraint
        /// </exception>
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
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="routeTemplate"/> or <paramref name="handler"/> is <c>null</c>
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when <paramref name="routeTemplate"/> contains a parameter without a name or references an unknown route constraint
        /// </exception>
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
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="routeTemplate"/> or <paramref name="handler"/> is <c>null</c>
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when <paramref name="routeTemplate"/> contains a parameter without a name or references an unknown route constraint
        /// </exception>
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
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="routeTemplate"/> or <paramref name="handler"/> is <c>null</c>
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when <paramref name="routeTemplate"/> contains a parameter without a name or references an unknown route constraint
        /// </exception>
        IArgusEndpointConventionBuilder MapDelete(string routeTemplate, ArgusHandlerDelegate handler);
    }
}
