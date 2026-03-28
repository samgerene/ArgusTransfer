// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusPipeHostBackgroundService.cs">
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

namespace ArgusTransfer.Server
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.IO.Pipes;
    using System.Linq;
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
        /// The <see cref="ArgusRequestSerializer"/> used to deserialize incoming requests
        /// </summary>
        private readonly ArgusRequestSerializer requestSerializer;

        /// <summary>
        /// The <see cref="ArgusResponseSerializer"/> used to serialize outgoing responses
        /// </summary>
        private readonly ArgusResponseSerializer responseSerializer;

        /// <summary>
        /// The <see cref="IArgusBodySerializerRegistry"/> used to resolve serializers by content type
        /// </summary>
        private readonly IArgusBodySerializerRegistry bodySerializerRegistry;

        /// <summary>
        /// Tracks in-flight request handler tasks for graceful shutdown draining
        /// </summary>
        private readonly ConcurrentDictionary<int, Task> activeRequests = new();

        /// <summary>
        /// Cancellation source for in-flight requests during shutdown drain
        /// </summary>
        private CancellationTokenSource drainCancellationTokenSource;

        /// <summary>
        /// Semaphore used to limit the number of concurrently processed requests
        /// </summary>
        private SemaphoreSlim concurrencySemaphore;

        /// <summary>
        /// The current number of in-flight requests being processed
        /// </summary>
        private long currentRequestCount;

        /// <summary>
        /// The total number of requests rejected due to concurrency limits
        /// </summary>
        private long rejectedRequestCount;

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
        /// <param name="bodySerializer">
        /// The <see cref="IArgusBodySerializer"/> used to serialize and deserialize message bodies
        /// </param>
        public ArgusPipeHostBackgroundService(
            ILogger<ArgusPipeHostBackgroundService> logger,
            ArgusRouter router,
            IOptions<ArgusPipeHostOptions> options,
            IArgusBodySerializer bodySerializer)
        {
            this.logger = logger;
            this.router = router;
            this.options = options.Value;
            this.requestSerializer = new ArgusRequestSerializer(bodySerializer);
            this.responseSerializer = new ArgusResponseSerializer(bodySerializer);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusPipeHostBackgroundService"/> class
        /// using an <see cref="IArgusBodySerializerRegistry"/> for content-type-aware serialization
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
        /// <param name="bodySerializerRegistry">
        /// The <see cref="IArgusBodySerializerRegistry"/> used to resolve serializers by content type
        /// </param>
        public ArgusPipeHostBackgroundService(
            ILogger<ArgusPipeHostBackgroundService> logger,
            ArgusRouter router,
            IOptions<ArgusPipeHostOptions> options,
            IArgusBodySerializerRegistry bodySerializerRegistry)
        {
            this.logger = logger;
            this.router = router;
            this.options = options.Value;
            this.bodySerializerRegistry = bodySerializerRegistry;
            this.requestSerializer = new ArgusRequestSerializer(bodySerializerRegistry);
            this.responseSerializer = new ArgusResponseSerializer(bodySerializerRegistry);
        }

        /// <summary>
        /// Gets the current number of in-flight requests being processed
        /// </summary>
        public long CurrentRequestCount => Interlocked.Read(ref this.currentRequestCount);

        /// <summary>
        /// Gets the total number of requests rejected due to concurrency limits
        /// </summary>
        public long RejectedRequestCount => Interlocked.Read(ref this.rejectedRequestCount);

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
            this.drainCancellationTokenSource = new CancellationTokenSource();
            this.concurrencySemaphore = new SemaphoreSlim(this.options.MaxConcurrentRequests, this.options.MaxConcurrentRequests);

            while (!stoppingToken.IsCancellationRequested)
            {
                var serverStream = new NamedPipeServerStream(this.options.PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await serverStream.WaitForConnectionAsync(stoppingToken);

                var requestToken = this.drainCancellationTokenSource.Token;
                var task = Task.Run(async () =>
                {
                    var semaphoreAcquired = false;

                    try
                    {
                        using var reader = new StreamReader(serverStream);
                        await using var writer = new StreamWriter(serverStream);
                        writer.AutoFlush = true;

                        var request = await this.requestSerializer.ReadAsync(reader, requestToken, this.options.MaxRequestBodySize);

                        if (!await this.concurrencySemaphore.WaitAsync(0))
                        {
                            Interlocked.Increment(ref this.rejectedRequestCount);

                            this.logger.LogWarning(
                                "Concurrency limit of {MaxConcurrentRequests} reached. Rejecting request {Verb} {Route} with 503.",
                                this.options.MaxConcurrentRequests, request.Verb, request.Route);

                            var rejectResponse = new ArgusResponse
                            {
                                StatusCode = ArgusStatusCode.ServiceUnavailable,
                                CorrelationToken = request.CorrelationToken,
                                Body = "Server concurrency limit reached"
                            };

                            this.responseSerializer.Write(writer, rejectResponse);
                            return;
                        }

                        semaphoreAcquired = true;
                        Interlocked.Increment(ref this.currentRequestCount);

                        if (this.bodySerializerRegistry != null)
                        {
                            var acceptType = request.Accept;

                            if (!string.IsNullOrEmpty(acceptType)
                                && !this.bodySerializerRegistry.TryGetSerializer(acceptType, out _))
                            {
                                var notAcceptable = new ArgusResponse
                                {
                                    StatusCode = ArgusStatusCode.NotAcceptable,
                                    CorrelationToken = request.CorrelationToken
                                };

                                this.responseSerializer.Write(writer, notAcceptable);
                                return;
                            }
                        }

                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(requestToken);

                        if (this.options.RequestTimeout != Timeout.InfiniteTimeSpan)
                        {
                            timeoutCts.CancelAfter(this.options.RequestTimeout);
                        }

                        var context = new ArgusContext(request, timeoutCts.Token);

                        try
                        {
                            await this.router.RouteAsync(context);
                        }
                        catch (OperationCanceledException) when (!requestToken.IsCancellationRequested)
                        {
                            this.logger.LogWarning(
                                "Request {Verb} {Route} timed out after {Timeout}.",
                                request.Verb, request.Route, this.options.RequestTimeout);

                            var timeoutResponse = new ArgusResponse
                            {
                                StatusCode = ArgusStatusCode.ServiceUnavailable,
                                CorrelationToken = request.CorrelationToken,
                                Body = "Request processing timed out"
                            };

                            this.responseSerializer.Write(writer, timeoutResponse);
                            return;
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }

                        if (context.Response.IsStreamed)
                        {
                            await this.responseSerializer.WriteAsync(writer, context.Response, requestToken);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(request.Accept))
                            {
                                this.responseSerializer.Write(writer, context.Response, request.Accept);
                            }
                            else
                            {
                                this.responseSerializer.Write(writer, context.Response);
                            }
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        logger.LogWarning(ex, "Request rejected: {Message}", ex.Message);

                        try
                        {
                            await using var errorWriter = new StreamWriter(serverStream);
                            errorWriter.AutoFlush = true;

                            var badRequest = new ArgusResponse
                            {
                                StatusCode = ArgusStatusCode.BadRequest,
                                Body = ex.Message
                            };

                            this.responseSerializer.Write(errorWriter, badRequest);
                        }
                        catch
                        {
                            // Best effort — pipe may already be broken
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Pipe request handling failed.");
                    }
                    finally
                    {
                        if (semaphoreAcquired)
                        {
                            Interlocked.Decrement(ref this.currentRequestCount);
                            this.concurrencySemaphore.Release();
                        }

                        await serverStream.DisposeAsync();
                    }
                }, CancellationToken.None);

                var taskId = task.Id;
                this.activeRequests[taskId] = task;
                task.ContinueWith(_ => this.activeRequests.TryRemove(taskId, out _), TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        /// <summary>
        /// Stops the service, draining in-flight requests within the configured
        /// <see cref="ArgusPipeHostOptions.ShutdownDrainTimeout"/> before cancelling them
        /// </summary>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal forced shutdown
        /// </param>
        /// <returns>
        /// An awaitable <see cref="Task"/>
        /// </returns>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);

            var pending = this.activeRequests.Values.ToArray();

            if (pending.Length > 0)
            {
                this.logger.LogInformation("Draining {Count} in-flight request(s)...", pending.Length);

                var drainTask = Task.WhenAll(pending);
                var completed = await Task.WhenAny(drainTask, Task.Delay(this.options.ShutdownDrainTimeout, cancellationToken));

                if (completed != drainTask)
                {
                    this.logger.LogWarning("Shutdown drain timeout expired. Cancelling {Count} remaining request(s).", this.activeRequests.Count);
                    this.drainCancellationTokenSource?.Cancel();
                }
            }

            this.drainCancellationTokenSource?.Dispose();
            this.concurrencySemaphore?.Dispose();
        }

        /// <summary>
        /// Routes an <see cref="ArgusRequest"/> through the middleware pipeline
        /// and to the appropriate handler via the <see cref="ArgusRouter"/>
        /// </summary>
        /// <param name="argusRequest">
        /// The <see cref="ArgusRequest"/> to handle
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> that can be used to cancel the operation
        /// </param>
        /// <returns>
        /// An <see cref="ArgusResponse"/> containing the result of the operation
        /// </returns>
        public async Task<ArgusResponse> HandleRequestAsync(ArgusRequest argusRequest, CancellationToken cancellationToken = default)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (this.options.RequestTimeout != Timeout.InfiniteTimeSpan)
            {
                timeoutCts.CancelAfter(this.options.RequestTimeout);
            }

            var context = new ArgusContext(argusRequest, timeoutCts.Token);

            try
            {
                await this.router.RouteAsync(context);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new ArgusResponse
                {
                    StatusCode = ArgusStatusCode.ServiceUnavailable,
                    CorrelationToken = argusRequest.CorrelationToken,
                    Body = "Request processing timed out"
                };
            }

            return context.Response;
        }
    }
}
