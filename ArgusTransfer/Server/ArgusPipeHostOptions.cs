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
        /// Gets or sets the maximum allowed request body size in bytes. Defaults to 1 MB (1,048,576 bytes).
        /// Requests with a Content-Length exceeding this limit are rejected with a 400 Bad Request response.
        /// </summary>
        public long MaxRequestBodySize { get; set; } = 1_048_576;
    }
}
