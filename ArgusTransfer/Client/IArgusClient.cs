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
        /// Sends an <see cref="ArgusRequest"/> over the named pipe and returns the <see cref="ArgusResponse"/>
        /// </summary>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> to send
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The <see cref="ArgusResponse"/> received from the server
        /// </returns>
        Task<ArgusResponse> SendAsync(ArgusRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a GET request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> GetAsync(string route, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a POST request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="body">The optional request body</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> PostAsync(string route, string body = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a PUT request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="body">The optional request body</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> PutAsync(string route, string body = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a PATCH request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="body">The optional request body</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> PatchAsync(string route, string body = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a DELETE request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> DeleteAsync(string route, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a HEAD request to the specified route
        /// </summary>
        /// <param name="route">The route to send the request to</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to signal cancellation</param>
        /// <returns>The <see cref="ArgusResponse"/> received from the server</returns>
        Task<ArgusResponse> HeadAsync(string route, CancellationToken cancellationToken = default);
    }
}
