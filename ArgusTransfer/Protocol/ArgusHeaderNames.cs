// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusHeaderNames.cs">
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

namespace ArgusTransfer.Protocol
{
    /// <summary>
    /// Defines the standard header names used by the ARGUS/1.0 wire protocol
    /// </summary>
    public static class ArgusHeaderNames
    {
        /// <summary>
        /// The Accept header name
        /// </summary>
        public const string Accept = "Accept";

        /// <summary>
        /// The Content-Type header name
        /// </summary>
        public const string ContentType = "Content-Type";

        /// <summary>
        /// The Content-Length header name
        /// </summary>
        public const string ContentLength = "Content-Length";

        /// <summary>
        /// The X-Correlation-Token header name
        /// </summary>
        public const string CorrelationToken = "X-Correlation-Token";

        /// <summary>
        /// The X-Timestamp header name
        /// </summary>
        public const string Timestamp = "X-Timestamp";
    }
}
