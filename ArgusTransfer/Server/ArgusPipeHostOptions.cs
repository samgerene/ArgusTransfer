// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusPipeHostOptions.cs">
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
    using System.IO.Pipes;
    using System.Threading;

    /// <summary>
    /// Configuration options for the <see cref="ArgusPipeHostBackgroundService"/>
    /// </summary>
    public class ArgusPipeHostOptions
    {
        /// <summary>
        /// Gets or sets the name of the named pipe to listen on. Defaults to "argus"
        /// </summary>
        public string PipeName { get; set; } = "argus";

        /// <summary>
        /// Gets or sets the <see cref="PipeSecurity"/> applied to the named pipe on Windows. When
        /// <c>null</c>, the host applies a default ACL granting Authenticated Users
        /// <see cref="PipeAccessRights.ReadWrite"/> and <see cref="PipeAccessRights.Synchronize"/>,
        /// which lets a user-session client connect to a pipe owned by a service running as
        /// LocalSystem. Set this to a custom <see cref="PipeSecurity"/> to lock the pipe down
        /// further (e.g. to a specific group SID). Ignored on non-Windows platforms.
        /// </summary>
        public PipeSecurity PipeSecurity { get; set; }

        /// <summary>
        /// Gets or sets the maximum allowed request body size in bytes. Defaults to 1 MB (1,048,576 bytes).
        /// Requests with a Content-Length exceeding this limit are rejected with a 400 Bad Request response.
        /// </summary>
        public long MaxRequestBodySize { get; set; } = 1_048_576;

        /// <summary>
        /// Gets or sets the maximum time to wait for in-flight requests to complete during shutdown.
        /// Defaults to 30 seconds. After this timeout, remaining requests are cancelled.
        /// </summary>
        public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the maximum time allowed for a single request handler to execute.
        /// Defaults to 60 seconds. Set to <see cref="Timeout.InfiniteTimeSpan"/> to disable the timeout.
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Gets or sets the maximum number of requests that can be processed concurrently.
        /// Defaults to 10. When the limit is reached, new requests receive a 503 Service Unavailable response.
        /// </summary>
        public int MaxConcurrentRequests { get; set; } = 10;
    }
}
