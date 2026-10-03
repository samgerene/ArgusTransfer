// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusTransportExtensions.cs">
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

    using ArgusTransfer.Client;
    using ArgusTransfer.Routing;
    using ArgusTransfer.Serialization;
    using ArgusTransfer.Server;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Extension methods for registering the Argus named-pipe host with the DI container
    /// </summary>
    public static class ArgusTransportExtensions
    {
        /// <summary>
        /// Registers <see cref="IArgusClient"/> with a transient <see cref="ArgusClient"/> implementation
        /// </summary>
        /// <param name="services">
        /// The <see cref="IServiceCollection"/> to register services with
        /// </param>
        /// <param name="pipeName">
        /// The name of the named pipe to connect to. Defaults to "argus"
        /// </param>
        /// <param name="defaultTimeout">An optional default timeout for all requests made by the client</param>
        /// <returns>
        /// The <see cref="IServiceCollection"/> for method chaining
        /// </returns>
        public static IServiceCollection AddArgusClient(this IServiceCollection services, string pipeName = "argus", TimeSpan? defaultTimeout = null)
        {
            return services.AddArgusClient(pipeName, defaultTimeout, null);
        }

        /// <summary>
        /// Registers <see cref="IArgusClient"/> with a transient <see cref="ArgusClient"/> implementation
        /// that retries transient named pipe failures according to an <see cref="ArgusRetryPolicy"/>.
        /// Retry attempts are logged through <see cref="ILogger{ArgusClient}"/> when logging is registered.
        /// </summary>
        /// <param name="services">
        /// The <see cref="IServiceCollection"/> to register services with
        /// </param>
        /// <param name="pipeName">
        /// The name of the named pipe to connect to
        /// </param>
        /// <param name="defaultTimeout">An optional default timeout for all requests made by the client</param>
        /// <param name="configureRetry">
        /// An optional <see cref="Action{ArgusRetryPolicy}"/> to configure the retry policy. When <c>null</c>, retries are disabled;
        /// otherwise the action receives an <see cref="ArgusRetryPolicy"/> initialized with its defaults
        /// </param>
        /// <returns>
        /// The <see cref="IServiceCollection"/> for method chaining
        /// </returns>
        public static IServiceCollection AddArgusClient(this IServiceCollection services, string pipeName, TimeSpan? defaultTimeout, Action<ArgusRetryPolicy> configureRetry)
        {
            services.AddTransient<IArgusClient>(sp =>
            {
                var client = CreateClient(sp, pipeName);

                if (defaultTimeout.HasValue)
                {
                    client.DefaultTimeout = defaultTimeout.Value;
                }

                if (configureRetry != null)
                {
                    var retryPolicy = new ArgusRetryPolicy();
                    configureRetry(retryPolicy);
                    client.RetryPolicy = retryPolicy;
                }

                return client;
            });

            return services;
        }

        /// <summary>
        /// Registers <see cref="IArgusClient"/> with a transient <see cref="ArgusClient"/> implementation and lets the caller
        /// configure each client instance, for example its <see cref="ArgusClient.DefaultTimeout"/>,
        /// <see cref="ArgusClient.RetryPolicy"/> or <see cref="ArgusClient.Compression"/>. The client receives an
        /// <see cref="ILogger{ArgusClient}"/> when logging is registered.
        /// </summary>
        /// <param name="services">
        /// The <see cref="IServiceCollection"/> to register services with
        /// </param>
        /// <param name="pipeName">
        /// The name of the named pipe to connect to
        /// </param>
        /// <param name="configure">
        /// An <see cref="Action{ArgusClient}"/> invoked for every created client; may be <c>null</c>
        /// </param>
        /// <returns>
        /// The <see cref="IServiceCollection"/> for method chaining
        /// </returns>
        public static IServiceCollection AddArgusClient(this IServiceCollection services, string pipeName, Action<ArgusClient> configure)
        {
            services.AddTransient<IArgusClient>(sp =>
            {
                var client = CreateClient(sp, pipeName);
                configure?.Invoke(client);

                return client;
            });

            return services;
        }

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

            services.AddArgusPlainTextProtocol();

            services.AddHostedService<ArgusPipeHostBackgroundService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ArgusPipeHostBackgroundService>>();
                var router = sp.GetRequiredService<ArgusRouter>();
                var options = sp.GetRequiredService<IOptions<ArgusPipeHostOptions>>();
                var registry = sp.GetService<IArgusBodySerializerRegistry>();

                return registry != null
                    ? new ArgusPipeHostBackgroundService(logger, router, options, registry)
                    : new ArgusPipeHostBackgroundService(logger, router, options, sp.GetRequiredService<IArgusBodySerializer>());
            });

            return services;
        }

        /// <summary>
        /// Creates an <see cref="ArgusClient"/> that uses the registered <see cref="IArgusBodySerializerRegistry"/> and
        /// <see cref="ILogger{ArgusClient}"/> when available
        /// </summary>
        /// <param name="serviceProvider">
        /// The <see cref="IServiceProvider"/> to resolve services from
        /// </param>
        /// <param name="pipeName">
        /// The name of the named pipe to connect to
        /// </param>
        /// <returns>
        /// The created <see cref="ArgusClient"/>
        /// </returns>
        private static ArgusClient CreateClient(IServiceProvider serviceProvider, string pipeName)
        {
            var registry = serviceProvider.GetService<IArgusBodySerializerRegistry>();
            var client = registry != null ? new ArgusClient(pipeName, registry) : new ArgusClient(pipeName);

            client.Logger = serviceProvider.GetService<ILogger<ArgusClient>>();

            return client;
        }
    }
}
