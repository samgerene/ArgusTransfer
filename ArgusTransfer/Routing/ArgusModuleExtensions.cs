// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusModuleExtensions.cs" company="Sam Gerené">
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
    using System;
    using System.Linq;
    using System.Reflection;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Extension methods for registering Argus modules and the router with the DI container
    /// </summary>
    public static class ArgusModuleExtensions
    {
        /// <summary>
        /// Scans the calling assembly for <see cref="IArgusModule"/> implementations,
        /// registers them as transient services, and registers <see cref="ArgusRouter"/>
        /// as a singleton via a factory that resolves all modules and calls <see cref="IArgusModule.AddRoutes"/>
        /// </summary>
        /// <param name="services">
        /// The <see cref="IServiceCollection"/> to register services with
        /// </param>
        /// <returns>
        /// The <see cref="IServiceCollection"/> for method chaining
        /// </returns>
        public static IServiceCollection AddArgusModules(this IServiceCollection services)
        {
            var moduleTypes = Assembly.GetCallingAssembly()
                .GetTypes()
                .Where(t => typeof(IArgusModule).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                .ToList();

            foreach (var moduleType in moduleTypes)
            {
                services.AddTransient(typeof(IArgusModule), moduleType);
            }

            services.AddSingleton<ArgusRouter>(sp =>
            {
                var router = new ArgusRouter();
                var modules = sp.GetServices<IArgusModule>();

                foreach (var module in modules)
                {
                    module.AddRoutes(router);
                }

                return router;
            });

            return services;
        }
    }
}
