// -------------------------------------------------------------------------------------------------
//  <copyright file="IArgusClient.cs">
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
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;

    /// <summary>
    /// Defines the contract for a client that sends an <see cref="ArgusRequest"/> and receives
    /// an <see cref="ArgusResponse"/> over a named pipe using the ARGUS/1.0 wire protocol
    /// </summary>
    public interface IArgusClient : IDisposable
    {
        /// <summary>
        /// Gets or sets the default timeout for requests. Defaults to 30 seconds.
        /// </summary>
        TimeSpan DefaultTimeout { get; set; }

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
        Task<ArgusResponse> SendAsync(ArgusRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a GET request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> GetAsync(string route, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a GET request to the specified route with query parameters
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> GetAsync(string route, IReadOnlyDictionary<string, string> queryParameters, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a POST request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> PostAsync(string route, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a POST request to the specified route with query parameters
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> PostAsync(string route, IReadOnlyDictionary<string, string> queryParameters, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a PUT request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> PutAsync(string route, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a PUT request to the specified route with query parameters
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> PutAsync(string route, IReadOnlyDictionary<string, string> queryParameters, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a PATCH request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> PatchAsync(string route, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a PATCH request to the specified route with query parameters
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="body">The optional request body</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> PatchAsync(string route, IReadOnlyDictionary<string, string> queryParameters, string body = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a DELETE request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> DeleteAsync(string route, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a DELETE request to the specified route with query parameters
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> DeleteAsync(string route, IReadOnlyDictionary<string, string> queryParameters, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a HEAD request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> HeadAsync(string route, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a HEAD request to the specified route with query parameters
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="queryParameters">The query parameters to include in the request</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> HeadAsync(string route, IReadOnlyDictionary<string, string> queryParameters, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a POST request with a streaming body to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="bodyStream">The <see cref="Stream"/> containing the request body</param>
        /// <param name="contentType">The optional content type of the body. Defaults to application/octet-stream</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> PostAsync(string route, Stream bodyStream, string contentType = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a PUT request with a streaming body to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="bodyStream">The <see cref="Stream"/> containing the request body</param>
        /// <param name="contentType">The optional content type of the body. Defaults to application/octet-stream</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> PutAsync(string route, Stream bodyStream, string contentType = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a PATCH request with a streaming body to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="bodyStream">The <see cref="Stream"/> containing the request body</param>
        /// <param name="contentType">The optional content type of the body. Defaults to application/octet-stream</param>
        /// <param name="timeout">An optional per-request timeout that overrides <see cref="DefaultTimeout"/></param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> PatchAsync(string route, Stream bodyStream, string contentType = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    }
}
