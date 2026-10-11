// -------------------------------------------------------------------------------------------------
//   <copyright file="Program.cs">
//
//     Copyright (c) 2025-2026 Sam Gerené
//
//     Licensed under the Apache License, Version 2.0 (the "License");
//     you may not use this file except in compliance with the License.
//     You may obtain a copy of the License at
//
//         http://www.apache.org/licenses/LICENSE-2.0
//
//     Unless required by applicable law or agreed to in writing, software
//     distributed under the License is distributed on an "AS IS" BASIS,
//     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//     See the License for the specific language governing permissions and
//     limitations under the License.
//
//   </copyright>
//   ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Sample
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    using ArgusTransfer.Client;
    using ArgusTransfer.Extensions;
    using ArgusTransfer.Routing;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// Entry point for the ArgusTransfer sample application
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point that configures and runs the sample host
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        [ExcludeFromCodeCoverage]
        public static void Main(string[] args)
        {
            Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    ConfigureServices(services, "sample");
                    services.AddHostedService<SampleClientService>();
                })
                .Build()
                .Run();
        }

        /// <summary>
        /// Registers the server and the client of the sample: the modules, a JSON body serializer, the exception
        /// handler, authentication, the pipe host and an <see cref="IArgusClient"/> with a retry policy. Both sides
        /// have compression enabled.
        /// </summary>
        /// <param name="services">
        /// The <see cref="IServiceCollection"/> to register services with
        /// </param>
        /// <param name="pipeName">
        /// The name of the named pipe the host listens on and the client connects to
        /// </param>
        /// <returns>
        /// The <see cref="IServiceCollection"/> for method chaining
        /// </returns>
        public static IServiceCollection ConfigureServices(IServiceCollection services, string pipeName)
        {
            // Server: the routes of the modules in this assembly
            services.AddArgusModules(typeof(Program).Assembly);

            // Lets callers ask for "Accept: application/json"
            services.AddArgusBodySerializer<JsonArgusBodySerializer>();

            // Turns an unhandled exception of a handler into a 500 problem details response
            services.AddArgusExceptionHandler();

            // Requires "Authorization: ApiKey ..." on every endpoint that does not call AllowAnonymous()
            services.AddArgusAuthentication<SampleApiKeyAuthenticationHandler>();

            services.AddArgusPipeHost(options =>
            {
                options.PipeName = pipeName;

                // Compresses a response of at least Compression.MinimumBodySize bytes when the client accepts it
                options.Compression.Enabled = true;
            });

            // Client
            services.AddArgusClient(pipeName, client =>
            {
                // Retries transient pipe failures, such as a connection that the host drops
                client.RetryPolicy = new ArgusRetryPolicy
                {
                    MaxRetries = 3,
                    InitialDelay = TimeSpan.FromMilliseconds(200),
                    Strategy = RetryBackoffStrategy.Exponential
                };

                // Accepts compressed responses and compresses request bodies of at least Compression.MinimumBodySize bytes
                client.Compression.Enabled = true;
            });

            return services;
        }
    }
}
