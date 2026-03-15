// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusSerializationExtensions.cs">
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
    using ArgusTransfer.Serialization;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    /// <summary>
    /// Extension methods for registering Argus serialization services with the DI container
    /// </summary>
    public static class ArgusSerializationExtensions
    {
        /// <summary>
        /// Registers the <see cref="IArgusBodySerializer"/> as a singleton using <see cref="JsonArgusBodySerializer"/>.
        /// Uses <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService, TImplementation}(IServiceCollection)"/>
        /// so that a previously registered alternative body serializer takes precedence.
        /// </summary>
        /// <param name="services">
        /// The <see cref="IServiceCollection"/> to register services with
        /// </param>
        /// <returns>
        /// The <see cref="IServiceCollection"/> for method chaining
        /// </returns>
        public static IServiceCollection AddArgusTextProtocol(this IServiceCollection services)
        {
            services.TryAddSingleton<IArgusBodySerializer, JsonArgusBodySerializer>();

            return services;
        }
    }
}
