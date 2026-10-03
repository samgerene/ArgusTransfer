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
    using System.Runtime.Versioning;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Middleware;
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
        /// The <see cref="ArgusCompressionOptions"/> in effect, taken from <see cref="ArgusPipeHostOptions.Compression"/>
        /// </summary>
        private ArgusCompressionOptions compression;

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
            this.UseCompressionEncodings();
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
            this.UseCompressionEncodings();
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
                var serverStream = this.CreatePipeServer();

                try
                {
                    await serverStream.WaitForConnectionAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    await serverStream.DisposeAsync();
                    throw;
                }

                var requestToken = this.drainCancellationTokenSource.Token;
                var task = Task.Run(async () =>
                {
                    var semaphoreAcquired = false;

                    try
                    {
                        var request = await this.requestSerializer.ReadAsync(serverStream, requestToken, this.options.MaxRequestBodySize);

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

                            await this.responseSerializer.WriteAsync(serverStream, rejectResponse, requestToken);
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

                                await this.responseSerializer.WriteAsync(serverStream, notAcceptable, requestToken);
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
                        catch (OperationCanceledException ex) when (!requestToken.IsCancellationRequested)
                        {
                            this.logger.LogWarning(ex,
                                "Request {Verb} {Route} timed out after {Timeout}.",
                                request.Verb, request.Route, this.options.RequestTimeout);

                            var timeoutResponse = new ArgusResponse
                            {
                                StatusCode = ArgusStatusCode.ServiceUnavailable,
                                CorrelationToken = request.CorrelationToken,
                                Body = "Request processing timed out"
                            };

                            await this.responseSerializer.WriteAsync(serverStream, timeoutResponse, requestToken);
                            return;
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            context.Response = this.CreateUnhandledExceptionResponse(request, ex);
                        }

                        context.Response ??= this.CreateMissingResponse(request);

                        ArgusCompression.ApplyToResponse(request, context.Response, this.compression);

                        if (!string.IsNullOrEmpty(request.Accept))
                        {
                            await this.responseSerializer.WriteAsync(serverStream, context.Response, request.Accept, requestToken);
                        }
                        else
                        {
                            await this.responseSerializer.WriteAsync(serverStream, context.Response, requestToken);
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        logger.LogWarning(ex, "Request rejected: {Message}", ex.Message);

                        try
                        {
                            var badRequest = new ArgusResponse
                            {
                                StatusCode = ArgusStatusCode.BadRequest,
                                Body = ex.Message
                            };

                            await this.responseSerializer.WriteAsync(serverStream, badRequest, requestToken);
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
                _ = task.ContinueWith(_ => this.activeRequests.TryRemove(taskId, out _), TaskContinuationOptions.ExecuteSynchronously);
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
                    if (this.drainCancellationTokenSource != null)
                    {
                        await this.drainCancellationTokenSource.CancelAsync();
                    }
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
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return this.CreateUnhandledExceptionResponse(argusRequest, ex);
            }

            return context.Response ?? this.CreateMissingResponse(argusRequest);
        }

        /// <summary>
        /// Takes the compression options from <see cref="ArgusPipeHostOptions.Compression"/> (or the defaults when it is
        /// <c>null</c>) and points both serializers at their encodings, so compressed requests are decoded
        /// </summary>
        private void UseCompressionEncodings()
        {
            this.compression = this.options.Compression ?? new ArgusCompressionOptions();
            this.requestSerializer.ContentEncodings = this.compression.Encodings;
            this.responseSerializer.ContentEncodings = this.compression.Encodings;
        }

        /// <summary>
        /// Logs an exception that escaped the routing pipeline and creates a generic
        /// <see cref="ArgusStatusCode.InternalServerError"/> problem details response for it, so the client
        /// receives a response instead of a closed connection and no exception details leak to the client.
        /// Register <see cref="ArgusExceptionHandlerMiddleware"/> to customize this behavior
        /// </summary>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> that was being processed
        /// </param>
        /// <param name="exception">
        /// The unhandled <see cref="Exception"/>
        /// </param>
        /// <returns>
        /// The 500 <see cref="ArgusResponse"/>
        /// </returns>
        private ArgusResponse CreateUnhandledExceptionResponse(ArgusRequest request, Exception exception)
        {
            this.logger.LogError(
                exception,
                "Unhandled exception while processing {Verb} {Route} [{CorrelationToken}]. Returning 500.",
                request.Verb,
                request.Route,
                request.CorrelationToken);

            return CreateInternalServerErrorResponse(request);
        }

        /// <summary>
        /// Logs that the routing pipeline completed without setting <see cref="ArgusContext.Response"/> -- a handler or
        /// middleware bug -- and creates a generic <see cref="ArgusStatusCode.InternalServerError"/> problem details
        /// response, so the client receives a response instead of a closed connection (which a retrying client would resend)
        /// </summary>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> that was being processed
        /// </param>
        /// <returns>
        /// The 500 <see cref="ArgusResponse"/>
        /// </returns>
        private ArgusResponse CreateMissingResponse(ArgusRequest request)
        {
            this.logger.LogError(
                "Processing {Verb} {Route} [{CorrelationToken}] completed without a response; the handler or a middleware did not set ArgusContext.Response. Returning 500.",
                request.Verb,
                request.Route,
                request.CorrelationToken);

            return CreateInternalServerErrorResponse(request);
        }

        /// <summary>
        /// Creates the generic <see cref="ArgusStatusCode.InternalServerError"/> problem details response for a request,
        /// without a body for <see cref="ArgusVerb.HEAD"/> requests
        /// </summary>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> that was being processed
        /// </param>
        /// <returns>
        /// The 500 <see cref="ArgusResponse"/>
        /// </returns>
        private static ArgusResponse CreateInternalServerErrorResponse(ArgusRequest request)
        {
            var response = ArgusExceptionHandlerMiddleware.CreateErrorResponse(request.CorrelationToken, null, includeExceptionDetails: false);

            if (request.Verb == ArgusVerb.HEAD)
            {
                response.Body = null;
            }

            return response;
        }

        /// <summary>
        /// Creates a new <see cref="NamedPipeServerStream"/> for an incoming connection,
        /// applying the configured (or default) <see cref="PipeSecurity"/> on Windows so a
        /// user-session client can connect to a pipe owned by a service running as LocalSystem
        /// </summary>
        /// <returns>
        /// The created <see cref="NamedPipeServerStream"/>
        /// </returns>
        private NamedPipeServerStream CreatePipeServer()
        {
            if (OperatingSystem.IsWindows())
            {
                var security = this.options.PipeSecurity ?? CreateDefaultPipeSecurity();

                return NamedPipeServerStreamAcl.Create(
                    this.options.PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    inBufferSize: 0,
                    outBufferSize: 0,
                    pipeSecurity: security);
            }

            return new NamedPipeServerStream(
                this.options.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
        }

        /// <summary>
        /// Builds the default <see cref="PipeSecurity"/> applied when no
        /// <see cref="ArgusPipeHostOptions.PipeSecurity"/> has been supplied. Grants the
        /// Authenticated Users group <see cref="PipeAccessRights.ReadWrite"/> and
        /// <see cref="PipeAccessRights.Synchronize"/>, which lets clients running as a regular
        /// user connect to a pipe owned by a service running as LocalSystem. The service-running
        /// account itself is granted <see cref="PipeAccessRights.FullControl"/> so subsequent
        /// instances of the pipe can be created (Authenticated Users alone do not get
        /// <see cref="PipeAccessRights.CreateNewInstance"/>).
        /// </summary>
        /// <returns>
        /// The default <see cref="PipeSecurity"/>
        /// </returns>
        [SupportedOSPlatform("windows")]
        private static PipeSecurity CreateDefaultPipeSecurity()
        {
            var security = new PipeSecurity();

            using var current = WindowsIdentity.GetCurrent();

            if (current.User != null)
            {
                security.AddAccessRule(new PipeAccessRule(
                    current.User,
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));
            }

            security.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize,
                AccessControlType.Allow));

            return security;
        }
    }
}
