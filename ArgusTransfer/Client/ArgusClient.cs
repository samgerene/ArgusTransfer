// -------------------------------------------------------------------------------------------------
//  <copyright file="ArgusClient.cs">
//
//    Copyright (C) 2025-2026 Sam Gerené
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, softwareUseCases
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Pipes;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Serialization;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// A low-level client that sends an <see cref="ArgusRequest"/> and receives an <see cref="ArgusResponse"/>
    /// over a named pipe using the ARGUS/1.0 wire protocol
    /// </summary>
    public class ArgusClient : IArgusClient
    {
        /// <summary>
        /// The name of the named pipe to connect to
        /// </summary>
        private readonly string pipeName;

        /// <summary>
        /// The <see cref="ArgusRequestSerializer"/> used to serialize outgoing requests
        /// </summary>
        private readonly ArgusRequestSerializer requestSerializer;

        /// <summary>
        /// The <see cref="ArgusResponseSerializer"/> used to deserialize incoming responses
        /// </summary>
        private readonly ArgusResponseSerializer responseSerializer;

        /// <summary>
        /// Tracks whether this instance has been disposed
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Backing field for <see cref="Logger"/>
        /// </summary>
        private ILogger logger = NullLogger.Instance;

        /// <summary>
        /// Gets or sets the default timeout for requests. Defaults to 30 seconds.
        /// The timeout applies to the whole call, including retries and the delays between them.
        /// </summary>
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the <see cref="ArgusRetryPolicy"/> used to retry requests that fail because of
        /// transient named pipe errors. Defaults to <c>null</c>, which disables retries.
        /// </summary>
        public ArgusRetryPolicy RetryPolicy { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ILogger"/> used to log retry attempts. Defaults to <see cref="NullLogger.Instance"/>;
        /// assigning <c>null</c> restores the default.
        /// </summary>
        public ILogger Logger
        {
            get => this.logger;
            set => this.logger = value ?? NullLogger.Instance;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusClient"/> class
        /// </summary>
        /// <param name="pipeName">
        /// The name of the named pipe to connect to. Defaults to "argus"
        /// </param>
        /// <param name="bodySerializer">
        /// An optional <see cref="IArgusBodySerializer"/>. Defaults to <see cref="PlainTextArgusBodySerializer"/>
        /// </param>
        public ArgusClient(string pipeName = "argus", IArgusBodySerializer bodySerializer = null)
        {
            this.pipeName = pipeName;
            var body = bodySerializer ?? new PlainTextArgusBodySerializer();
            this.requestSerializer = new ArgusRequestSerializer(body);
            this.responseSerializer = new ArgusResponseSerializer(body);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusClient"/> class
        /// that uses the specified <see cref="IArgusBodySerializerRegistry"/> to resolve
        /// body serializers by content type
        /// </summary>
        /// <param name="pipeName">
        /// The name of the named pipe to connect to
        /// </param>
        /// <param name="bodySerializerRegistry">
        /// The <see cref="IArgusBodySerializerRegistry"/> used to resolve body serializers
        /// </param>
        public ArgusClient(string pipeName, IArgusBodySerializerRegistry bodySerializerRegistry)
        {
            this.pipeName = pipeName;
            this.requestSerializer = new ArgusRequestSerializer(bodySerializerRegistry);
            this.responseSerializer = new ArgusResponseSerializer(bodySerializerRegistry);
        }

        /// <summary>
        /// Sends an <see cref="ArgusRequest"/> over the named pipe and returns the <see cref="ArgusResponse"/>
        /// </summary>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> to send
        /// </param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The <see cref="ArgusResponse"/> received from the server
        /// </returns>
        /// <remarks>
        /// When <see cref="RetryPolicy"/> is set, attempts that fail with a transient error are retried
        /// as described on <see cref="ArgusRetryPolicy"/>, within the same timeout
        /// </remarks>
        /// <exception cref="TimeoutException">
        /// Thrown when the request, including any retries, does not complete within the timeout
        /// </exception>
        public async Task<ArgusResponse> SendAsync(ArgusRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);

            var effectiveTimeout = timeout ?? this.DefaultTimeout;
            using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var retryPolicy = this.RetryPolicy;
            var bodyStreamStart = request.IsStreamed && request.BodyStream.CanSeek ? request.BodyStream.Position : -1;
            var retryAttempt = 0;

            try
            {
                while (true)
                {
                    var requestSent = false;
                    TimeSpan delay;

                    var pipeClient = new NamedPipeClientStream(".", this.pipeName, PipeDirection.InOut);

                    try
                    {
                        await pipeClient.ConnectAsync(linkedCts.Token);

                        requestSent = true;
                        await this.requestSerializer.WriteAsync(pipeClient, request, linkedCts.Token);

                        var response = await this.responseSerializer.ReadAsync(pipeClient, linkedCts.Token);

                        return response;
                    }
                    catch (Exception ex) when (CanRetry(retryPolicy, ex, request, requestSent, bodyStreamStart, retryAttempt, linkedCts.Token))
                    {
                        retryAttempt++;
                        delay = retryPolicy.GetDelay(retryAttempt);

                        this.logger.LogWarning(
                            ex,
                            "Request {Verb} {Route} to pipe '{PipeName}' failed; retry {RetryAttempt} of {MaxRetries} in {DelayMs} ms",
                            request.Verb,
                            request.Route,
                            this.pipeName,
                            retryAttempt,
                            retryPolicy.MaxRetries,
                            delay.TotalMilliseconds);
                    }
                    finally
                    {
                        await pipeClient.DisposeAsync();
                    }

                    if (bodyStreamStart >= 0)
                    {
                        request.BodyStream.Position = bodyStreamStart;
                    }

                    await Task.Delay(delay, linkedCts.Token);
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"The request to pipe '{this.pipeName}' timed out after {effectiveTimeout.TotalSeconds:F1} seconds.");
            }
            catch (IOException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"The request to pipe '{this.pipeName}' timed out after {effectiveTimeout.TotalSeconds:F1} seconds.");
            }
        }

        /// <summary>
        /// Sends a GET request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        public Task<ArgusResponse> GetAsync(string route, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return this.SendAsync(new ArgusRequest { Verb = ArgusVerb.GET, Route = route }, timeout, cancellationToken);
        }

        /// <summary>
        /// Sends a GET request to the specified route with query parameters
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        public Task<ArgusResponse> GetAsync(string route, IReadOnlyDictionary<string, string> queryParameters, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var request = new ArgusRequest { Verb = ArgusVerb.GET, Route = route };
            CopyQueryParameters(queryParameters, request);
            return this.SendAsync(request, timeout, cancellationToken);
        }

        /// <summary>
        /// Sends a POST request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        public Task<ArgusResponse> PostAsync(string route, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return this.SendAsync(new ArgusRequest { Verb = ArgusVerb.POST, Route = route, Body = body }, timeout, cancellationToken);
        }

        /// <summary>
        /// Sends a POST request to the specified route with query parameters
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        public Task<ArgusResponse> PostAsync(string route, IReadOnlyDictionary<string, string> queryParameters, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var request = new ArgusRequest { Verb = ArgusVerb.POST, Route = route, Body = body };
            CopyQueryParameters(queryParameters, request);
            return this.SendAsync(request, timeout, cancellationToken);
        }

        /// <summary>
        /// Sends a POST request with a streaming body to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="bodyStream">The <see cref="Stream"/> containing the request body</param>
        /// <param name="contentType">The optional content type of the body. Defaults to application/octet-stream</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        public Task<ArgusResponse> PostAsync(string route, Stream bodyStream, string contentType = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return this.SendStreamAsync(ArgusVerb.POST, route, bodyStream, contentType, timeout, cancellationToken);
        }

        /// <summary>
        /// Sends a PUT request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        public Task<ArgusResponse> PutAsync(string route, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return this.SendAsync(new ArgusRequest { Verb = ArgusVerb.PUT, Route = route, Body = body }, timeout, cancellationToken);
        }

        /// <summary>
        /// Sends a PUT request to the specified route with query parameters
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        public Task<ArgusResponse> PutAsync(string route, IReadOnlyDictionary<string, string> queryParameters, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var request = new ArgusRequest { Verb = ArgusVerb.PUT, Route = route, Body = body };
            CopyQueryParameters(queryParameters, request);
            return this.SendAsync(request, timeout, cancellationToken);
        }

        /// <summary>
        /// Sends a PUT request with a streaming body to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="bodyStream">The <see cref="Stream"/> containing the request body</param>
        /// <param name="contentType">The optional content type of the body. Defaults to application/octet-stream</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        public Task<ArgusResponse> PutAsync(string route, Stream bodyStream, string contentType = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return this.SendStreamAsync(ArgusVerb.PUT, route, bodyStream, contentType, timeout, cancellationToken);
        }

        /// <summary>
        /// Sends a PATCH request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        public Task<ArgusResponse> PatchAsync(string route, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return this.SendAsync(new ArgusRequest { Verb = ArgusVerb.PATCH, Route = route, Body = body }, timeout, cancellationToken);
        }

        /// <summary>
        /// Sends a PATCH request to the specified route with query parameters
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        public Task<ArgusResponse> PatchAsync(string route, IReadOnlyDictionary<string, string> queryParameters, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var request = new ArgusRequest { Verb = ArgusVerb.PATCH, Route = route, Body = body };
            CopyQueryParameters(queryParameters, request);
            return this.SendAsync(request, timeout, cancellationToken);
        }

        /// <summary>
        /// Sends a PATCH request with a streaming body to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="bodyStream">The <see cref="Stream"/> containing the request body</param>
        /// <param name="contentType">The optional content type of the body. Defaults to application/octet-stream</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        public Task<ArgusResponse> PatchAsync(string route, Stream bodyStream, string contentType = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return this.SendStreamAsync(ArgusVerb.PATCH, route, bodyStream, contentType, timeout, cancellationToken);
        }

        /// <summary>
        /// Sends a DELETE request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        public Task<ArgusResponse> DeleteAsync(string route, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return this.SendAsync(new ArgusRequest { Verb = ArgusVerb.DELETE, Route = route }, timeout, cancellationToken);
        }

        /// <summary>
        /// Sends a DELETE request to the specified route with query parameters
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        public Task<ArgusResponse> DeleteAsync(string route, IReadOnlyDictionary<string, string> queryParameters, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var request = new ArgusRequest { Verb = ArgusVerb.DELETE, Route = route };
            CopyQueryParameters(queryParameters, request);
            return this.SendAsync(request, timeout, cancellationToken);
        }

        /// <summary>
        /// Sends a HEAD request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        public Task<ArgusResponse> HeadAsync(string route, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return this.SendAsync(new ArgusRequest { Verb = ArgusVerb.HEAD, Route = route }, timeout, cancellationToken);
        }

        /// <summary>
        /// Sends a HEAD request to the specified route with query parameters
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        public Task<ArgusResponse> HeadAsync(string route, IReadOnlyDictionary<string, string> queryParameters, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var request = new ArgusRequest { Verb = ArgusVerb.HEAD, Route = route };
            CopyQueryParameters(queryParameters, request);
            return this.SendAsync(request, timeout, cancellationToken);
        }

        /// <summary>
        /// Sends an <see cref="ArgusRequest"/> over the named pipe and validates that the
        /// <see cref="ArgusResponse"/> indicates a successful (2xx) status code
        /// </summary>
        /// <param name="request">The <see cref="ArgusRequest"/> to send</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The successful <see cref="ArgusResponse"/> received from the server</returns>
        /// <exception cref="ArgusRequestException">Thrown when the response status code is outside the 2xx range</exception>
        public async Task<ArgusResponse> SendEnsureSuccessAsync(ArgusRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var response = await this.SendAsync(request, timeout, cancellationToken);
            return response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Sends a GET request to the specified route and validates that the response indicates a successful (2xx) status code
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The successful <see cref="ArgusResponse"/> received from the server</returns>
        /// <exception cref="ArgusRequestException">Thrown when the response status code is outside the 2xx range</exception>
        public async Task<ArgusResponse> GetEnsureSuccessAsync(string route, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var response = await this.GetAsync(route, timeout, cancellationToken);
            return response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Sends a GET request to the specified route with query parameters and validates that the response indicates a successful (2xx) status code
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The successful <see cref="ArgusResponse"/> received from the server</returns>
        /// <exception cref="ArgusRequestException">Thrown when the response status code is outside the 2xx range</exception>
        public async Task<ArgusResponse> GetEnsureSuccessAsync(string route, IReadOnlyDictionary<string, string> queryParameters, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var response = await this.GetAsync(route, queryParameters, timeout, cancellationToken);
            return response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Sends a POST request to the specified route and validates that the response indicates a successful (2xx) status code
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The successful <see cref="ArgusResponse"/> received from the server</returns>
        /// <exception cref="ArgusRequestException">Thrown when the response status code is outside the 2xx range</exception>
        public async Task<ArgusResponse> PostEnsureSuccessAsync(string route, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var response = await this.PostAsync(route, body, timeout, cancellationToken);
            return response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Sends a POST request to the specified route with query parameters and validates that the response indicates a successful (2xx) status code
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The successful <see cref="ArgusResponse"/> received from the server</returns>
        /// <exception cref="ArgusRequestException">Thrown when the response status code is outside the 2xx range</exception>
        public async Task<ArgusResponse> PostEnsureSuccessAsync(string route, IReadOnlyDictionary<string, string> queryParameters, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var response = await this.PostAsync(route, queryParameters, body, timeout, cancellationToken);
            return response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Sends a POST request with a streaming body to the specified route and validates that the response indicates a successful (2xx) status code
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="bodyStream">The <see cref="Stream"/> containing the request body</param>
        /// <param name="contentType">The optional content type of the body. Defaults to application/octet-stream</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The successful <see cref="ArgusResponse"/> received from the server</returns>
        /// <exception cref="ArgusRequestException">Thrown when the response status code is outside the 2xx range</exception>
        public async Task<ArgusResponse> PostEnsureSuccessAsync(string route, Stream bodyStream, string contentType = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var response = await this.PostAsync(route, bodyStream, contentType, timeout, cancellationToken);
            return response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Sends a PUT request to the specified route and validates that the response indicates a successful (2xx) status code
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The successful <see cref="ArgusResponse"/> received from the server</returns>
        /// <exception cref="ArgusRequestException">Thrown when the response status code is outside the 2xx range</exception>
        public async Task<ArgusResponse> PutEnsureSuccessAsync(string route, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var response = await this.PutAsync(route, body, timeout, cancellationToken);
            return response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Sends a PUT request to the specified route with query parameters and validates that the response indicates a successful (2xx) status code
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The successful <see cref="ArgusResponse"/> received from the server</returns>
        /// <exception cref="ArgusRequestException">Thrown when the response status code is outside the 2xx range</exception>
        public async Task<ArgusResponse> PutEnsureSuccessAsync(string route, IReadOnlyDictionary<string, string> queryParameters, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var response = await this.PutAsync(route, queryParameters, body, timeout, cancellationToken);
            return response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Sends a PUT request with a streaming body to the specified route and validates that the response indicates a successful (2xx) status code
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="bodyStream">The <see cref="Stream"/> containing the request body</param>
        /// <param name="contentType">The optional content type of the body. Defaults to application/octet-stream</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The successful <see cref="ArgusResponse"/> received from the server</returns>
        /// <exception cref="ArgusRequestException">Thrown when the response status code is outside the 2xx range</exception>
        public async Task<ArgusResponse> PutEnsureSuccessAsync(string route, Stream bodyStream, string contentType = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var response = await this.PutAsync(route, bodyStream, contentType, timeout, cancellationToken);
            return response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Sends a PATCH request to the specified route and validates that the response indicates a successful (2xx) status code
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The successful <see cref="ArgusResponse"/> received from the server</returns>
        /// <exception cref="ArgusRequestException">Thrown when the response status code is outside the 2xx range</exception>
        public async Task<ArgusResponse> PatchEnsureSuccessAsync(string route, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var response = await this.PatchAsync(route, body, timeout, cancellationToken);
            return response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Sends a PATCH request to the specified route with query parameters and validates that the response indicates a successful (2xx) status code
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The successful <see cref="ArgusResponse"/> received from the server</returns>
        /// <exception cref="ArgusRequestException">Thrown when the response status code is outside the 2xx range</exception>
        public async Task<ArgusResponse> PatchEnsureSuccessAsync(string route, IReadOnlyDictionary<string, string> queryParameters, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var response = await this.PatchAsync(route, queryParameters, body, timeout, cancellationToken);
            return response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Sends a PATCH request with a streaming body to the specified route and validates that the response indicates a successful (2xx) status code
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="bodyStream">The <see cref="Stream"/> containing the request body</param>
        /// <param name="contentType">The optional content type of the body. Defaults to application/octet-stream</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The successful <see cref="ArgusResponse"/> received from the server</returns>
        /// <exception cref="ArgusRequestException">Thrown when the response status code is outside the 2xx range</exception>
        public async Task<ArgusResponse> PatchEnsureSuccessAsync(string route, Stream bodyStream, string contentType = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var response = await this.PatchAsync(route, bodyStream, contentType, timeout, cancellationToken);
            return response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Sends a DELETE request to the specified route and validates that the response indicates a successful (2xx) status code
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The successful <see cref="ArgusResponse"/> received from the server</returns>
        /// <exception cref="ArgusRequestException">Thrown when the response status code is outside the 2xx range</exception>
        public async Task<ArgusResponse> DeleteEnsureSuccessAsync(string route, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var response = await this.DeleteAsync(route, timeout, cancellationToken);
            return response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Sends a DELETE request to the specified route with query parameters and validates that the response indicates a successful (2xx) status code
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The successful <see cref="ArgusResponse"/> received from the server</returns>
        /// <exception cref="ArgusRequestException">Thrown when the response status code is outside the 2xx range</exception>
        public async Task<ArgusResponse> DeleteEnsureSuccessAsync(string route, IReadOnlyDictionary<string, string> queryParameters, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var response = await this.DeleteAsync(route, queryParameters, timeout, cancellationToken);
            return response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Sends a HEAD request to the specified route and validates that the response indicates a successful (2xx) status code
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The successful <see cref="ArgusResponse"/> received from the server</returns>
        /// <exception cref="ArgusRequestException">Thrown when the response status code is outside the 2xx range</exception>
        public async Task<ArgusResponse> HeadEnsureSuccessAsync(string route, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var response = await this.HeadAsync(route, timeout, cancellationToken);
            return response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Sends a HEAD request to the specified route with query parameters and validates that the response indicates a successful (2xx) status code
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The successful <see cref="ArgusResponse"/> received from the server</returns>
        /// <exception cref="ArgusRequestException">Thrown when the response status code is outside the 2xx range</exception>
        public async Task<ArgusResponse> HeadEnsureSuccessAsync(string route, IReadOnlyDictionary<string, string> queryParameters, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var response = await this.HeadAsync(route, queryParameters, timeout, cancellationToken);
            return response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Determines whether a failed attempt may be retried under the specified <see cref="ArgusRetryPolicy"/>
        /// </summary>
        /// <param name="retryPolicy">The retry policy, or <c>null</c> when retries are disabled</param>
        /// <param name="exception">The exception raised by the failed attempt</param>
        /// <param name="request">The request being sent</param>
        /// <param name="requestSent">Whether writing the request had started when the attempt failed</param>
        /// <param name="bodyStreamStart">The original position of a seekable request body stream, or -1</param>
        /// <param name="retryAttempt">The number of retries performed so far</param>
        /// <param name="cancellationToken">The token covering caller cancellation and the request timeout</param>
        /// <returns><c>true</c> if the request may be retried; otherwise <c>false</c></returns>
        internal static bool CanRetry(ArgusRetryPolicy retryPolicy, Exception exception, ArgusRequest request, bool requestSent, long bodyStreamStart, int retryAttempt, CancellationToken cancellationToken)
        {
            if (retryPolicy == null
                || retryAttempt >= retryPolicy.MaxRetries
                || cancellationToken.IsCancellationRequested
                || exception is OperationCanceledException or TimeoutException)
            {
                return false;
            }

            if (requestSent)
            {
                if (!retryPolicy.RetryNonIdempotentRequests && !ArgusRetryPolicy.IsIdempotent(request.Verb))
                {
                    return false;
                }

                if (request.IsStreamed && bodyStreamStart < 0)
                {
                    return false;
                }
            }

            return retryPolicy.ShouldRetry?.Invoke(exception) ?? false;
        }

        /// <summary>
        /// Sends a request with a streaming body using the specified verb
        /// </summary>
        private Task<ArgusResponse> SendStreamAsync(ArgusVerb verb, string route, Stream bodyStream, string contentType, TimeSpan? timeout, CancellationToken cancellationToken)
        {
            var request = new ArgusRequest { Verb = verb, Route = route, BodyStream = bodyStream };

            if (contentType != null)
            {
                request.Headers[ArgusHeaderNames.ContentType] = contentType;
            }

            return this.SendAsync(request, timeout, cancellationToken);
        }

        /// <summary>
        /// Copies query parameters from a read-only dictionary into the request
        /// </summary>
        /// <param name="queryParameters">The source query parameters</param>
        /// <param name="request">The target request</param>
        private static void CopyQueryParameters(IReadOnlyDictionary<string, string> queryParameters, ArgusRequest request)
        {
            if (queryParameters != null)
            {
                foreach (var kvp in queryParameters)
                {
                    request.QueryParameters[kvp.Key] = kvp.Value;
                }
            }
        }

        /// <summary>
        /// Disposes the <see cref="ArgusClient"/>
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and optionally managed resources
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                this.disposed = true;
            }
        }
    }
}
