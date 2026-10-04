// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusModuleExtensions.cs">
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
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    using ArgusTransfer.Middleware;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    /// <summary>
    /// Extension methods for registering Argus modules and the router with the DI container
    /// </summary>
    public static class ArgusModuleExtensions
    {
        /// <summary>
        /// Scans the calling assembly for <see cref="IArgusModule"/> implementations,
        /// registers them as transient services, and registers <see cref="ArgusRouter"/>
        /// as a singleton via a factory that resolves all modules and calls <see cref="IArgusModule.AddRoutes"/>.
        /// When an <see cref="ArgusExceptionHandlerMiddleware"/> is registered (see <c>AddArgusExceptionHandler()</c>),
        /// it is added to the router as the outermost global middleware before the modules add their routes, followed by
        /// the <see cref="ArgusAuthenticationMiddleware"/> when registered (see <c>AddArgusAuthentication&lt;THandler&gt;()</c>).
        /// The method is never inlined, so the calling assembly is always the assembly that calls this method. To register
        /// modules that live in other assemblies, use <see cref="AddArgusModules(IServiceCollection, Assembly[])"/>
        /// </summary>
        /// <param name="services">
        /// The <see cref="IServiceCollection"/> to register services with
        /// </param>
        /// <returns>
        /// The <see cref="IServiceCollection"/> for method chaining
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> is <c>null</c>
        /// </exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IServiceCollection AddArgusModules(this IServiceCollection services)
        {
            return services.AddArgusModules(Assembly.GetCallingAssembly());
        }

        /// <summary>
        /// Scans the specified assemblies for <see cref="IArgusModule"/> implementations,
        /// registers them as transient services, and registers <see cref="ArgusRouter"/>
        /// as a singleton via a factory that resolves all modules and calls <see cref="IArgusModule.AddRoutes"/>.
        /// The global middleware is added as described for <see cref="AddArgusModules(IServiceCollection)"/>.
        /// Calling this method more than once, or combining it with <see cref="AddArgusModules(IServiceCollection)"/>,
        /// registers each module type and the router only once
        /// </summary>
        /// <param name="services">
        /// The <see cref="IServiceCollection"/> to register services with
        /// </param>
        /// <param name="assemblies">
        /// The assemblies to scan for <see cref="IArgusModule"/> implementations
        /// </param>
        /// <returns>
        /// The <see cref="IServiceCollection"/> for method chaining
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or <paramref name="assemblies"/> is <c>null</c>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="assemblies"/> is empty or contains a <c>null</c> element
        /// </exception>
        public static IServiceCollection AddArgusModules(this IServiceCollection services, params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(assemblies);

            if (assemblies.Length == 0)
            {
                throw new ArgumentException("At least one assembly must be specified.", nameof(assemblies));
            }

            if (assemblies.Any(a => a == null))
            {
                throw new ArgumentException("The assemblies must not contain null.", nameof(assemblies));
            }

            var moduleTypes = assemblies
                .Distinct()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IArgusModule).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);

            foreach (var moduleType in moduleTypes)
            {
                services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IArgusModule), moduleType));
            }

            services.TryAddSingleton<ArgusRouter>(CreateRouter);

            return services;
        }

        /// <summary>
        /// Creates the <see cref="ArgusRouter"/>: adds the registered global middleware and lets every registered
        /// <see cref="IArgusModule"/> add its routes
        /// </summary>
        /// <param name="serviceProvider">
        /// The <see cref="IServiceProvider"/> to resolve the middleware and modules from
        /// </param>
        /// <returns>
        /// The configured <see cref="ArgusRouter"/>
        /// </returns>
        private static ArgusRouter CreateRouter(IServiceProvider serviceProvider)
        {
            var router = new ArgusRouter();

            var exceptionHandler = serviceProvider.GetService<ArgusExceptionHandlerMiddleware>();

            if (exceptionHandler != null)
            {
                router.UseMiddleware(exceptionHandler);
            }

            var authentication = serviceProvider.GetService<ArgusAuthenticationMiddleware>();

            if (authentication != null)
            {
                router.UseMiddleware(authentication);
            }

            var modules = serviceProvider.GetServices<IArgusModule>();

            foreach (var module in modules)
            {
                module.AddRoutes(router);
            }

            return router;
        }
    }
}
