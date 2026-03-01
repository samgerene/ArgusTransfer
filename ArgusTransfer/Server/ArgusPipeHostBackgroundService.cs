// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusPipeHostBackgroundService.cs" company="Sam Gerené">
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

namespace ArgusTransfer.Server
{
    using System;
    using System.IO;
    using System.IO.Pipes;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Routing;
    using ArgusTransfer.Serialization;

    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Background service that listens on a named pipe for IPC requests
    /// and routes them to the appropriate handler via the <see cref="ArgusRouter"/>
    /// </summary>
    public class ArgusPipeHostBackgroundService : BackgroundService
    {
        /// <summary>
        /// The <see cref="ILogger{ArgusPipeHostBackgroundService}"/> used for logging
        /// </summary>
        private readonly ILogger<ArgusPipeHostBackgroundService> logger;

        /// <summary>
        /// The <see cref="ArgusRouter"/> used to dispatch requests to registered handlers
        /// </summary>
        private readonly ArgusRouter router;

        /// <summary>
        /// The <see cref="ArgusPipeHostOptions"/> containing the pipe configuration
        /// </summary>
        private readonly ArgusPipeHostOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusPipeHostBackgroundService"/> class
        /// </summary>
        /// <param name="logger">
        /// The <see cref="ILogger{ArgusPipeHostBackgroundService}"/> used for logging
        /// </param>
        /// <param name="router">
        /// The <see cref="ArgusRouter"/> used to dispatch requests to registered handlers
        /// </param>
        /// <param name="options">
        /// The <see cref="IOptions{ArgusPipeHostOptions}"/> containing the pipe configuration
        /// </param>
        public ArgusPipeHostBackgroundService(ILogger<ArgusPipeHostBackgroundService> logger, ArgusRouter router, IOptions<ArgusPipeHostOptions> options)
        {
            this.logger = logger;
            this.router = router;
            this.options = options.Value;
        }

        /// <summary>
        /// Listens on the named pipe for incoming requests, deserializes them,
        /// routes to the appropriate handler, and writes back the response
        /// </summary>
        /// <param name="stoppingToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// An awaitable <see cref="Task"/>
        /// </returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var serverStream = new NamedPipeServerStream(this.options.PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await serverStream.WaitForConnectionAsync(stoppingToken);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var reader = new StreamReader(serverStream);
                        await using var writer = new StreamWriter(serverStream) { AutoFlush = true };

                        var request = await ArgusRequestReader.ReadAsync(reader, stoppingToken);
                        var response = await this.HandleRequestAsync(request);
                        ArgusResponseWriter.Write(writer, response);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Pipe request handling failed.");
                    }
                    finally
                    {
                        await serverStream.DisposeAsync();
                    }
                }, stoppingToken);
            }
        }

        /// <summary>
        /// Routes an <see cref="ArgusRequest"/> to the appropriate handler
        /// via the <see cref="ArgusRouter"/>
        /// </summary>
        /// <param name="argusRequest">
        /// The <see cref="ArgusRequest"/> to handle
        /// </param>
        /// <returns>
        /// An <see cref="ArgusResponse"/> containing the result of the operation
        /// </returns>
        public async Task<ArgusResponse> HandleRequestAsync(ArgusRequest argusRequest)
        {
            return await this.router.RouteAsync(argusRequest);
        }
    }
}
