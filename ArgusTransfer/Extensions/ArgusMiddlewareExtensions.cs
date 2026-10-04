// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusMiddlewareExtensions.cs">
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

namespace ArgusTransfer.Extensions
{
    using System;

    using ArgusTransfer.Authentication;
    using ArgusTransfer.Middleware;
    using ArgusTransfer.Routing;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Extension methods for registering Argus middleware with the DI container
    /// </summary>
    public static class ArgusMiddlewareExtensions
    {
        /// <summary>
        /// Registers <see cref="ArgusExceptionHandlerMiddleware"/> as a singleton and optionally configures
        /// <see cref="ArgusExceptionHandlerOptions"/>. The <see cref="ArgusRouter"/> created by
        /// <see cref="ArgusModuleExtensions.AddArgusModules(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/> registers it as the outermost global middleware,
        /// before any middleware added by modules, regardless of the order in which both methods are called.
        /// </summary>
        /// <param name="services">
        /// The <see cref="IServiceCollection"/> to register services with
        /// </param>
        /// <param name="configure">
        /// An optional <see cref="Action{ArgusExceptionHandlerOptions}"/> to configure the exception handler options
        /// </param>
        /// <returns>
        /// The <see cref="IServiceCollection"/> for method chaining
        /// </returns>
        public static IServiceCollection AddArgusExceptionHandler(this IServiceCollection services, Action<ArgusExceptionHandlerOptions> configure = null)
        {
            services.AddOptions();

            if (configure != null)
            {
                services.Configure(configure);
            }

            services.TryAddSingleton(sp => new ArgusExceptionHandlerMiddleware(
                sp.GetService<ILogger<ArgusExceptionHandlerMiddleware>>() ?? NullLogger<ArgusExceptionHandlerMiddleware>.Instance,
                sp.GetRequiredService<IOptions<ArgusExceptionHandlerOptions>>()));

            return services;
        }

        /// <summary>
        /// Registers <typeparamref name="THandler"/> as the singleton <see cref="IArgusAuthenticationHandler"/> and
        /// <see cref="ArgusAuthenticationMiddleware"/> as a singleton, and optionally configures
        /// <see cref="ArgusAuthenticationOptions"/>. The <see cref="ArgusRouter"/> created by
        /// <see cref="ArgusModuleExtensions.AddArgusModules(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/> registers the middleware as a global middleware directly after
        /// the exception handler (when registered) and before any middleware added by modules, regardless of the order in which
        /// the methods are called. When called more than once, the first registered handler is used.
        /// </summary>
        /// <typeparam name="THandler">
        /// The <see cref="IArgusAuthenticationHandler"/> implementation; it must be thread-safe
        /// </typeparam>
        /// <param name="services">
        /// The <see cref="IServiceCollection"/> to register services with
        /// </param>
        /// <param name="configure">
        /// An optional <see cref="Action{ArgusAuthenticationOptions}"/> to configure the authentication options
        /// </param>
        /// <returns>
        /// The <see cref="IServiceCollection"/> for method chaining
        /// </returns>
        public static IServiceCollection AddArgusAuthentication<THandler>(this IServiceCollection services, Action<ArgusAuthenticationOptions> configure = null)
            where THandler : class, IArgusAuthenticationHandler
        {
            services.AddOptions();

            if (configure != null)
            {
                services.Configure(configure);
            }

            services.TryAddSingleton<IArgusAuthenticationHandler, THandler>();
            services.TryAddSingleton(sp => new ArgusAuthenticationMiddleware(
                sp.GetRequiredService<IArgusAuthenticationHandler>(),
                sp.GetService<ILogger<ArgusAuthenticationMiddleware>>() ?? NullLogger<ArgusAuthenticationMiddleware>.Instance,
                sp.GetRequiredService<IOptions<ArgusAuthenticationOptions>>()));

            return services;
        }
    }
}
