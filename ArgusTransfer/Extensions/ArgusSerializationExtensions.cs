// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusSerializationExtensions.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

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
        /// Registers the <see cref="IArgusBodySerializer"/> as a singleton using <see cref="PlainTextArgusBodySerializer"/>.
        /// Uses <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService, TImplementation}(IServiceCollection)"/>
        /// so that a previously registered alternative body serializer takes precedence.
        /// </summary>
        /// <param name="services">
        /// The <see cref="IServiceCollection"/> to register services with
        /// </param>
        /// <returns>
        /// The <see cref="IServiceCollection"/> for method chaining
        /// </returns>
        public static IServiceCollection AddArgusPlainTextProtocol(this IServiceCollection services)
        {
            services.TryAddSingleton<IArgusBodySerializer, PlainTextArgusBodySerializer>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IArgusBodySerializer, PlainTextArgusBodySerializer>());
            services.TryAddSingleton<IArgusBodySerializerRegistry, ArgusBodySerializerRegistry>();

            return services;
        }

        /// <summary>
        /// Registers an additional <see cref="IArgusBodySerializer"/> implementation with the DI container.
        /// Uses <see cref="ServiceCollectionDescriptorExtensions.TryAddEnumerable(IServiceCollection, ServiceDescriptor)"/>
        /// so that each concrete type is registered at most once.
        /// </summary>
        /// <typeparam name="T">
        /// The <see cref="IArgusBodySerializer"/> implementation type to register
        /// </typeparam>
        /// <param name="services">
        /// The <see cref="IServiceCollection"/> to register services with
        /// </param>
        /// <returns>
        /// The <see cref="IServiceCollection"/> for method chaining
        /// </returns>
        public static IServiceCollection AddArgusBodySerializer<T>(this IServiceCollection services)
            where T : class, IArgusBodySerializer
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IArgusBodySerializer, T>());

            return services;
        }
    }
}
