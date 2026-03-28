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
        /// Gets or sets the default timeout for requests. Defaults to 30 seconds.
        /// </summary>
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

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
        public async Task<ArgusResponse> SendAsync(ArgusRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var effectiveTimeout = timeout ?? this.DefaultTimeout;
            using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var pipeClient = new NamedPipeClientStream(".", this.pipeName, PipeDirection.InOut);

            try
            {
                await pipeClient.ConnectAsync(linkedCts.Token);

                var writer = new StreamWriter(pipeClient, new UTF8Encoding(false)) { AutoFlush = false };
                var reader = new StreamReader(pipeClient, new UTF8Encoding(false));

                await this.requestSerializer.WriteAsync(writer, request, linkedCts.Token);

                var response = await this.responseSerializer.ReadAsync(reader, linkedCts.Token);

                return response;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"The request to pipe '{this.pipeName}' timed out after {effectiveTimeout.TotalSeconds:F1} seconds.");
            }
            catch (IOException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"The request to pipe '{this.pipeName}' timed out after {effectiveTimeout.TotalSeconds:F1} seconds.");
            }
            finally
            {
                await pipeClient.DisposeAsync();
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
        /// Sends a request with a streaming body using the specified verb
        /// </summary>
        private Task<ArgusResponse> SendStreamAsync(ArgusVerb verb, string route, Stream bodyStream, string contentType, TimeSpan? timeout, CancellationToken cancellationToken)
        {
            var request = new ArgusRequest { Verb = verb, Route = route, BodyStream = bodyStream };

            if (contentType != null)
            {
                request.Headers["Content-Type"] = contentType;
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
