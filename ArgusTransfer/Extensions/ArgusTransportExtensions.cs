// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusTransportExtensions.cs" company="Sam Gerené">
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

namespace ArgusTransfer.Extensions
{
    using System;

    using ArgusTransfer.Server;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Extension methods for registering the Argus named-pipe host with the DI container
    /// </summary>
    public static class ArgusTransportExtensions
    {
        /// <summary>
        /// Registers the <see cref="ArgusPipeHostBackgroundService"/> as a hosted service
        /// and optionally configures <see cref="ArgusPipeHostOptions"/>
        /// </summary>
        /// <param name="services">
        /// The <see cref="IServiceCollection"/> to register services with
        /// </param>
        /// <param name="configure">
        /// An optional <see cref="Action{ArgusPipeHostOptions}"/> to configure the pipe host options
        /// </param>
        /// <returns>
        /// The <see cref="IServiceCollection"/> for method chaining
        /// </returns>
        public static IServiceCollection AddArgusPipeHost(this IServiceCollection services, Action<ArgusPipeHostOptions> configure = null)
        {
            if (configure != null)
            {
                services.Configure(configure);
            }

            services.AddHostedService<ArgusPipeHostBackgroundService>();

            return services;
        }
    }
}
